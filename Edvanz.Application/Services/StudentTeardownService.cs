using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;

namespace Edvanz.Application.Services;

/// <inheritdoc cref="IStudentTeardownService" />
/// <remarks>
/// Depends ONLY on <see cref="IUnitOfWork"/>, <see cref="IStringLocalizer{T}"/> and
/// <see cref="IStudentLinkNotifier"/> — deliberately NOT on <c>ITeacherStudentService</c> or
/// <c>IPaymentService</c>, both of which (transitively) depend on this teardown. Keeping the
/// dependency one-way is what makes this safe to call from the student, departure and
/// recycle-bin-purge flows alike.
///
/// All data access goes through named repo methods (CLAUDE.md §3.1) — no raw predicates here.
/// </remarks>
public class StudentTeardownService : IStudentTeardownService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStudentLinkNotifier _linkNotifier;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public StudentTeardownService(
        IUnitOfWork unitOfWork,
        IStudentLinkNotifier linkNotifier,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _linkNotifier = linkNotifier;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<StudentTeardownOutcome> UnassignAndUnlinkAsync(
        long teacherId, long teacherStudentId, long? actingUserId)
    {
        var now = DateTime.UtcNow;

        // ── 1. The roster record leaves its class ──────────────────────────────
        // Guarded by teacher id: a record of another tenant is never touched.
        var student = await _unitOfWork.Students.GetByIdAndTeacherIgnoreFiltersAsync(
            teacherStudentId, teacherId);
        if (student is null)
            return StudentTeardownOutcome.Empty;

        if (student.SessionId.HasValue)
        {
            student.SessionId = null;
            await _unitOfWork.Students.UpdateAsync(student);
        }

        // ── 2. Session assignments stop being active ───────────────────────────
        // LINK-PRESERVING on purpose: DeactivateAssignmentsByStudentAsync is the PURGE
        // variant — it also NULLs StudentSessionAssignment.TeacherStudentId, which would
        // sever the student's attendance history from them. This teardown also runs for a
        // REVERSIBLE recycle-bin delete (restorable), so it only closes the assignment
        // (IsActive=false + UnassignedAt) and keeps the FK, matching what the departure and
        // unassign paths do. The purge path nulls the FK separately, right before the row
        // is hard-deleted.
        var activeAssignment = await _unitOfWork.AttendanceRepo
            .GetActiveAssignmentAsync(teacherStudentId);
        if (activeAssignment is not null)
        {
            activeAssignment.IsActive = false;
            activeAssignment.UnassignedAt = now;
            await _unitOfWork.AttendanceRepo.UpdateAssignmentAsync(activeAssignment);
        }

        var unlinkedStudentUserIds = new List<long>();

        // ── 3. End the STUDENT ACCOUNT link ────────────────────────────────────
        // Exact semantics of TeacherStudentLinkService.RemoveLinkedStudentsAsync
        // (RemovedByTeacher + UnlinkedAt + RemovedByUserId), plus the binding clear from
        // UnbindStudentLinkAsync — the roster record is going away, so nothing may keep
        // pointing at it. Terminal rows are kept for audit (§7.2b).
        var link = await _unitOfWork.Users.GetActiveStudentTeacherLinkByTeacherStudentIdAsync(
            teacherId, teacherStudentId);
        if (link is not null)
        {
            link.LinkStatus = LinkStatus.RemovedByTeacher;
            link.UnlinkedAt = now;
            link.RemovedByUserId = actingUserId;
            link.TeacherStudentId = null;

            await _unitOfWork.Users.UpdateStudentTeacherLinkAsync(link);
            unlinkedStudentUserIds.Add(link.StudentUserId);
        }

        // ── 4. End any Active Method-B parent link ─────────────────────────────
        // Same dangling problem: the parent app would keep rendering a deleted enrolment.
        // ParentChildTeacherLink has no RemovedByUserId column — status + UnlinkedAt only.
        var parentLinks = await _unitOfWork.Users
            .GetActiveParentChildTeacherLinksByTeacherStudentIdAsync(teacherId, teacherStudentId);
        foreach (var parentLink in parentLinks)
        {
            parentLink.LinkStatus = LinkStatus.RemovedByTeacher;
            parentLink.UnlinkedAt = now;
            parentLink.TeacherStudentId = null;

            await _unitOfWork.Users.UpdateParentChildTeacherLinkAsync(parentLink);
        }

        // NOTE (§5.2): no SaveChanges/Commit here — the caller owns the boundary, so this
        // whole teardown joins the same transaction as the delete that triggered it.
        return new StudentTeardownOutcome(unlinkedStudentUserIds);
    }

    /// <inheritdoc />
    public async Task PurgeStudentDependentsAsync(long teacherId, long teacherStudentId)
    {
        // Every table below either BLOCKS the roster record's hard delete (non-nullable or
        // NoAction/Restrict FK that SQL Server will not clean) or would be left dangling.
        // Payment (PaymentService) and attendance (AttendanceService) keep their own existing
        // hooks — this covers the modules that had none.

        // ── Video module (Module 14) ───────────────────────────────────────────
        // VideoAnalytics + VideoWatchEvent: NON-nullable, NoAction  → DELETE
        // VideoScope:                        nullable, NoAction     → DELETE (scope needs a target)
        // StudentVideoExamReport:            scalar (no FK)         → DELETE (no orphan attempts)
        await _unitOfWork.VideoAssetsRepo.PurgeStudentVideoDataAsync(teacherStudentId);

        // VideoUnitScope: nullable, NoAction, and CK_VideoUnitScopes_ExactlyOneTarget forbids
        // a null target → DELETE.
        await _unitOfWork.VideoUnitsRepo.DeleteUnitScopesByStudentAsync(teacherStudentId);

        // ── Online Exam module ─────────────────────────────────────────────────
        // StudentOnlineExamReport: NON-nullable, NoAction → DELETE (answers/options first).
        await _unitOfWork.StudentOnlineExamReportsRepo.PurgeReportsForStudentAsync(teacherStudentId);

        // ── Exams & Homework module (Module 6) ─────────────────────────────────
        // StudentAssignmentObligation: NON-nullable, Restrict → DELETE (audit logs first).
        // AssignmentScope is SetNull and needs no help.
        await _unitOfWork.ExamHomeworkRepo.PurgeStudentAssignmentDataAsync(teacherStudentId);

        // ── Public parent portal ───────────────────────────────────────────────
        // ParentPortalAccess: NON-nullable composite FK (TeacherStudentId, TeacherId), NoAction →
        // DELETE. SQL Server cleans nothing here, so a surviving grant would BLOCK the hard delete
        // and, worse, leave a parent's browser pointed at a roster row that no longer exists.
        await _unitOfWork.ParentPortalAccesses.DeleteForStudentAsync(teacherStudentId);

        // ── Account / parent links ─────────────────────────────────────────────
        // Both FKs are SetNull so they never block, but leaving the app-side graph pointing at
        // a row that is about to vanish is exactly the dangling state this task exists to kill.
        await _unitOfWork.Users.DetachLinksFromPurgedStudentAsync(teacherStudentId);
    }

    /// <inheritdoc />
    public async Task NotifyStudentUnlinkedAsync(long teacherId, StudentTeardownOutcome outcome)
    {
        if (outcome.UnlinkedStudentUserIds.Count == 0)
            return;

        foreach (var studentUserId in outcome.UnlinkedStudentUserIds)
        {
            try
            {
                // Same notification the teacher-side bulk removal sends — from the student's
                // point of view this IS "the teacher removed me".
                await _linkNotifier.NotifyRemovedByTeacherAsync(studentUserId, teacherId);
            }
            catch { /* best-effort: a notification failure must not fail the delete */ }
        }
    }
}
