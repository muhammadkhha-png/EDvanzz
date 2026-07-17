using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements the attendance→exam sync. Uses atomic <c>ExecuteUpdate</c> writes. The runtime sync/
/// reconcile methods swallow their own errors so they can never destabilize the attendance flow that
/// calls them (mirrors the notifier's self-contained-safe contract); <see cref="BackfillExamOccurrenceAsync"/>
/// is strict and throws (it runs inside the exam create/update transaction). Grades are preserved on
/// "present" (already-graded rows are skipped) and cleared on "absent".
/// </summary>
public class ExamAttendanceSyncService : IExamAttendanceSyncService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExamAttendanceSyncService> _logger;

    public ExamAttendanceSyncService(IUnitOfWork unitOfWork, ILogger<ExamAttendanceSyncService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task SyncFromSessionOccurrenceAsync(
        long teacherId, long sessionOccurrenceId, long teacherStudentId,
        AttendanceStatus status, long actingUserId)
        => SyncManyFromSessionOccurrenceAsync(
            teacherId, sessionOccurrenceId, new[] { teacherStudentId }, status, actingUserId);

    /// <inheritdoc />
    public async Task SyncManyFromSessionOccurrenceAsync(
        long teacherId, long sessionOccurrenceId, IReadOnlyCollection<long> teacherStudentIds,
        AttendanceStatus status, long actingUserId)
    {
        if (teacherStudentIds is null || teacherStudentIds.Count == 0) return;

        var mapped = MapStatus(status);
        if (mapped is null) return; // Held / unmapped — no exam change.
        var (newStatus, clearGrade, skipGraded) = mapped.Value;

        try
        {
            var occurrenceIds = await _unitOfWork.ExamHomeworkRepo
                .GetExamOccurrenceIdsBySessionOccurrenceAsync(teacherId, sessionOccurrenceId);
            if (occurrenceIds.Count == 0) return;

            DateTime utcNow = DateTime.UtcNow;
            foreach (var occurrenceId in occurrenceIds)
            {
                await _unitOfWork.ExamHomeworkRepo.SetExamAttendanceByOccurrenceAsync(
                    teacherId, occurrenceId, teacherStudentIds,
                    newStatus, clearGrade, skipGraded, utcNow, actingUserId);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: an exam-sync failure must never break attendance marking.
            _logger.LogError(ex,
                "Exam attendance sync failed for teacher {TeacherId}, sessionOccurrence {SessionOccurrenceId}",
                teacherId, sessionOccurrenceId);
        }
    }

    /// <inheritdoc />
    public async Task ReconcileExamsForSessionOccurrenceAsync(
        long teacherId, long sessionOccurrenceId, long actingUserId)
    {
        try
        {
            var examOccurrenceIds = await _unitOfWork.ExamHomeworkRepo
                .GetExamOccurrenceIdsBySessionOccurrenceAsync(teacherId, sessionOccurrenceId);
            if (examOccurrenceIds.Count == 0) return; // no during-session exam anchored here — nothing to do.

            var records = await _unitOfWork.AttendanceRepo.GetRecordsByOccurrenceAsync(sessionOccurrenceId);

            var present = records
                .Where(r => (r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.CrossSessionPresent)
                            && r.TeacherStudentId.HasValue)
                .Select(r => r.TeacherStudentId!.Value).Distinct().ToList();

            var absent = records
                .Where(r => r.Status == AttendanceStatus.Absent && r.TeacherStudentId.HasValue)
                .Select(r => r.TeacherStudentId!.Value).Distinct().ToList();

            // Students still carrying a present/absent record. Anyone previously synced from this
            // occurrence but NOT in this set (their record was deleted or the class was re-marked)
            // is reverted to Pending below, so the exam can never keep a stale mark.
            var marked = present.Concat(absent).Distinct().ToList();

            DateTime utcNow = DateTime.UtcNow;
            foreach (var examOccurrenceId in examOccurrenceIds)
            {
                if (present.Count > 0)
                    await _unitOfWork.ExamHomeworkRepo.SetExamAttendanceByOccurrenceAsync(
                        teacherId, examOccurrenceId, present,
                        ObligationStatus.Attended, clearGrade: false, skipGraded: true, utcNow, actingUserId);

                if (absent.Count > 0)
                    await _unitOfWork.ExamHomeworkRepo.SetExamAttendanceByOccurrenceAsync(
                        teacherId, examOccurrenceId, absent,
                        ObligationStatus.DidNotAttend, clearGrade: true, skipGraded: false, utcNow, actingUserId);

                // Revert obligations whose backing attendance record is gone (present/absent set no
                // longer contains them) back to unmarked — only touches attendance-derived statuses.
                await _unitOfWork.ExamHomeworkRepo.RevertExamAttendanceForUnmarkedAsync(
                    teacherId, examOccurrenceId, marked, utcNow, actingUserId);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: an exam-sync failure must never break the attendance write that triggered it.
            _logger.LogError(ex,
                "Exam attendance reconcile failed for teacher {TeacherId}, sessionOccurrence {SessionOccurrenceId}",
                teacherId, sessionOccurrenceId);
        }
    }

    /// <inheritdoc />
    public async Task BackfillExamOccurrenceAsync(
        long teacherId, long examOccurrenceId, long sessionOccurrenceId, long actingUserId)
    {
        // Strict (no try/catch): runs inside the exam create/update transaction, so a failure must
        // propagate and roll the exam back rather than silently ship a half-synced exam.
        var records = await _unitOfWork.AttendanceRepo.GetRecordsByOccurrenceAsync(sessionOccurrenceId);

        var present = records
            .Where(r => (r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.CrossSessionPresent)
                        && r.TeacherStudentId.HasValue)
            .Select(r => r.TeacherStudentId!.Value).Distinct().ToList();

        var absent = records
            .Where(r => r.Status == AttendanceStatus.Absent && r.TeacherStudentId.HasValue)
            .Select(r => r.TeacherStudentId!.Value).Distinct().ToList();

        DateTime utcNow = DateTime.UtcNow;

        if (present.Count > 0)
            await _unitOfWork.ExamHomeworkRepo.SetExamAttendanceByOccurrenceAsync(
                teacherId, examOccurrenceId, present,
                ObligationStatus.Attended, clearGrade: false, skipGraded: true, utcNow, actingUserId);

        if (absent.Count > 0)
            await _unitOfWork.ExamHomeworkRepo.SetExamAttendanceByOccurrenceAsync(
                teacherId, examOccurrenceId, absent,
                ObligationStatus.DidNotAttend, clearGrade: true, skipGraded: false, utcNow, actingUserId);
    }

    /// <summary>Maps a session attendance status to the exam obligation change to apply, or null for no change.</summary>
    private static (ObligationStatus NewStatus, bool ClearGrade, bool SkipGraded)? MapStatus(AttendanceStatus status) =>
        status switch
        {
            AttendanceStatus.Present or AttendanceStatus.CrossSessionPresent
                => (ObligationStatus.Attended, false, true),   // present: don't disturb already-graded rows
            AttendanceStatus.Absent
                => (ObligationStatus.DidNotAttend, true, false), // absent: clear any grade
            _ => null,                                           // Held / other: no exam change
        };
}
