namespace Edvanz.Application.ServiceContract;

/// <summary>
/// What a teardown pass actually ended, so the caller can fire the post-commit
/// notifications AFTER its own transaction has committed (CLAUDE.md §5.1).
/// </summary>
/// <param name="UnlinkedStudentUserIds">
/// StudentUser ids whose ACTIVE account link to the teacher was ended by this pass.
/// Empty when the roster record had no linked student account.
/// </param>
public sealed record StudentTeardownOutcome(IReadOnlyList<long> UnlinkedStudentUserIds)
{
    /// <summary>Nothing was linked — nothing to notify.</summary>
    public static readonly StudentTeardownOutcome Empty = new(Array.Empty<long>());
}

/// <summary>
/// Shared teardown for a teacher's roster record (<c>TeacherStudent</c>): the cleanup
/// that must happen when a student is soft-deleted (recycle bin) or hard-deleted (purge).
///
/// WHY THIS EXISTS AS ITS OWN SERVICE (and not on <c>ITeacherStudentService</c>):
/// <c>TeacherStudentService</c> already injects <c>IPaymentService</c>, and the departure /
/// payment flows need the same teardown. Putting it here — depending ONLY on
/// <see cref="Edvanz.Domain.Interfaces.IUnitOfWork"/>, <c>IStringLocalizer</c> and
/// <see cref="IStudentLinkNotifier"/> — keeps the dependency graph acyclic: this service
/// must NEVER take <c>ITeacherStudentService</c> or <c>IPaymentService</c>.
///
/// TRANSACTION OWNERSHIP (CLAUDE.md §5.2): none of these methods call
/// <c>SaveChangesAsync</c>/<c>CommitAsync</c>. The CALLER owns the commit boundary and is
/// responsible for calling <see cref="NotifyStudentUnlinkedAsync"/> after it commits.
/// </summary>
public interface IStudentTeardownService
{
    /// <summary>
    /// Detaches a roster record from everything that would keep showing it as an ACTIVE
    /// enrolment after the teacher deleted it:
    /// <list type="number">
    ///   <item>nulls <c>TeacherStudent.SessionId</c> (the record is no longer in a class);</item>
    ///   <item>deactivates the student's <c>StudentSessionAssignment</c> rows;</item>
    ///   <item>ENDS the student ACCOUNT link (<c>StudentTeacherLink</c>) —
    ///         <c>RemovedByTeacher</c> + <c>UnlinkedAt</c> + <c>RemovedByUserId</c>, binding
    ///         cleared. Without this the student app keeps listing the teacher forever and the
    ///         filtered unique index (<c>[LinkStatus] IN (1,3)</c>) blocks a new link request
    ///         for the same pair;</item>
    ///   <item>ends any Active Method-B <c>ParentChildTeacherLink</c> the same way.</item>
    /// </list>
    /// Idempotent: re-running finds no Active link and no active assignment, and changes nothing.
    /// </summary>
    /// <param name="teacherId">Owning teacher (tenant), always from the JWT — never a route/body id.</param>
    /// <param name="teacherStudentId">The roster record being torn down.</param>
    /// <param name="actingUserId">User.Id that ended the link (audit column). Null for background jobs.</param>
    /// <returns>Who to notify once the caller's transaction commits.</returns>
    Task<StudentTeardownOutcome> UnassignAndUnlinkAsync(
        long teacherId, long teacherStudentId, long? actingUserId);

    /// <summary>
    /// Deletes/detaches every row that BLOCKS the hard delete of a roster record, so a student
    /// with video, online-exam or exam/homework history can be permanently deleted instead of
    /// 500-ing on an FK violation. See the implementation for the exact per-table policy.
    /// Call INSIDE the purge transaction, before <c>Students.DeleteAsync</c>. Idempotent.
    /// </summary>
    Task PurgeStudentDependentsAsync(long teacherId, long teacherStudentId);

    /// <summary>
    /// Post-commit, best-effort notification fan-out for the students whose link was ended by
    /// <see cref="UnassignAndUnlinkAsync"/>. Never throws — a notification failure must not
    /// fail (or roll back) the delete. Mirrors
    /// <c>TeacherStudentLinkService.RemoveLinkedStudentsAsync</c>.
    /// </summary>
    Task NotifyStudentUnlinkedAsync(long teacherId, StudentTeardownOutcome outcome);
}
