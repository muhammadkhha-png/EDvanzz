using Edvanz.Domain.Enums;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Keeps during-session exam obligations in sync with the session's attendance. Called by the
/// Attendance module after attendance is recorded, and by the Exams module at create time to
/// back-fill an exam from attendance already taken. All methods are best-effort — a sync failure
/// must never break attendance marking or exam creation.
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
    /// Initialize a newly created during-session exam occurrence from the attendance already recorded
    /// on its linked session occurrence (present → Attended, absent → DidNotAttend).
    /// </summary>
    Task BackfillExamOccurrenceAsync(
        long teacherId, long examOccurrenceId, long sessionOccurrenceId, long actingUserId);
}
