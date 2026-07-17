using Edvanz.Domain.Enums;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Keeps during-session exam obligations in sync with the session's attendance. Called by the
/// Attendance module after attendance is recorded/changed, and by the Exams module at create/edit
/// time to back-fill an exam from attendance already taken. The runtime sync methods
/// (<see cref="SyncFromSessionOccurrenceAsync"/>, <see cref="SyncManyFromSessionOccurrenceAsync"/>,
/// <see cref="ReconcileExamsForSessionOccurrenceAsync"/>) are best-effort — a sync failure must never
/// break attendance marking. <see cref="BackfillExamOccurrenceAsync"/> is the exception: it runs
/// inside the exam create/update transaction and THROWS so a failure rolls the exam back rather than
/// shipping a half-synced exam.
/// </summary>
public interface IExamAttendanceSyncService
{
    /// <summary>Sync one student's attendance into any during-session exam linked to the occurrence.</summary>
    Task SyncFromSessionOccurrenceAsync(
        long teacherId, long sessionOccurrenceId, long teacherStudentId,
        AttendanceStatus status, long actingUserId);

    /// <summary>Bulk variant — sync many students who were marked with the same status.</summary>
    Task SyncManyFromSessionOccurrenceAsync(
        long teacherId, long sessionOccurrenceId, IReadOnlyCollection<long> teacherStudentIds,
        AttendanceStatus status, long actingUserId);

    /// <summary>
    /// Reconcile every during-session exam anchored to <paramref name="sessionOccurrenceId"/> against
    /// the occurrence's CURRENT attendance records — the robust, idempotent primitive for any attendance
    /// MUTATION (edit, add, delete, bulk, offline sync): present → Attended (grades preserved), absent →
    /// DidNotAttend (grade cleared), and any student who was previously synced from this occurrence but
    /// whose record is now gone (deleted, or the class re-marked) → back to Pending. Best-effort. Call
    /// it AFTER the attendance change is committed so it reads the new truth. Reads records rather than
    /// trusting a caller-supplied set, so it can never diverge from the session (unlike a blind push).
    /// </summary>
    Task ReconcileExamsForSessionOccurrenceAsync(
        long teacherId, long sessionOccurrenceId, long actingUserId);

    /// <summary>
    /// Initialize a newly created during-session exam occurrence from the attendance already recorded
    /// on its linked session occurrence (present → Attended, absent → DidNotAttend). Runs INSIDE the
    /// exam create/update transaction and THROWS on failure (strict, not best-effort) so the exam is
    /// either fully in sync with the class or not created at all.
    /// </summary>
    Task BackfillExamOccurrenceAsync(
        long teacherId, long examOccurrenceId, long sessionOccurrenceId, long actingUserId);
}
