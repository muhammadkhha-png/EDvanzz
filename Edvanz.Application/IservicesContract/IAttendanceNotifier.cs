using Edvanz.Application.Dtos.DispatcherDtos;

namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Intent-revealing contract for attendance-triggered outbound messaging.
/// Part of Option-B: per-module notifier interfaces over IMessageDispatcher.
///
/// Caller: AttendanceService (post-commit, after ownsTransaction guard).
/// Engine: IMessageDispatcher.DispatchAsync — finds the active trigger,
///         resolves template, and enqueues one Hangfire job per recipient.
///
/// REQ-MSG-019: Automated triggers for StudentAttended, StudentAbsent,
///              and ConsecutiveAbsenceAlert events.
/// </summary>
public interface IAttendanceNotifier
{
    /// <summary>
    /// Fires when a single student is marked <c>Present</c> (or CrossSessionPresent).
    /// Dispatches a <see cref="TriggerEventType.StudentAttended"/> event.
    /// REQ-MSG-019.
    /// </summary>
    /// <param name="teacherId">Resolved from JWT — never from route or body.</param>
    /// <param name="teacherStudentId">TeacherStudent.Id of the marked student.</param>
    /// <param name="sessionId">Session in which attendance was marked.</param>
    /// <param name="sessionName">Human-readable session name for the message template.</param>
    /// <param name="occurrenceDate">Date of the attendance occurrence.</param>
    Task OnStudentAttendedAsync(
        long teacherId,
        long teacherStudentId,
        long sessionId,
        string sessionName,
        DateTime occurrenceDate);

    /// <summary>
    /// Fires when a single student is marked <c>Absent</c>.
    /// Dispatches both <see cref="TriggerEventType.StudentAbsent"/> and
    /// <see cref="TriggerEventType.ConsecutiveAbsenceAlert"/> (the latter is gated
    /// against the trigger's ThresholdValue in Phase 2 of the dispatcher).
    /// REQ-MSG-019.
    /// </summary>
    /// <param name="consecutiveAbsences">
    /// The post-mark recalculated consecutive absence count for this student.
    /// Used by the dispatcher to evaluate the threshold gate (Phase 2).
    /// </param>
    Task OnStudentAbsentAsync(
        long teacherId,
        long teacherStudentId,
        long sessionId,
        string sessionName,
        DateTime occurrenceDate,
        int consecutiveAbsences);

    /// <summary>
    /// Bulk overload — fires when multiple students are marked <c>Absent</c> in one operation.
    /// Issues a single <see cref="DispatchRequest"/> per event type so the dispatcher
    /// fans out per-student (BR-MSG-003) instead of N individual dispatch calls.
    /// Each tuple carries that student's own post-mark consecutive absence count.
    /// REQ-MSG-019.
    /// </summary>
    Task OnStudentsAbsentAsync(
        long teacherId,
        long sessionId,
        string sessionName,
        DateTime occurrenceDate,
        IReadOnlyCollection<(long teacherStudentId, int consecutiveAbsences)> students);
}