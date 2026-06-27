using Edvanz.Application.Dtos.DispatcherDtos;
using Edvanz.Application.Dtos.MessageResolver;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities.Messaging;
using Microsoft.Extensions.Logging;

namespace Edvanz.Application.Services;

/// <summary>
/// Translates attendance domain events into IMessageDispatcher calls.
/// Part of Option-B: per-module notifier over IMessageDispatcher.
///
/// Design rules:
/// 1. Never throws — all dispatch failures are caught, logged, and swallowed.
///    The originating attendance write already committed before this runs.
/// 2. Called post-commit only (ownsTransaction guard in AttendanceService).
///    This guarantees Hangfire jobs are never enqueued for rolled-back data.
/// 3. Single student: delegates to the bulk overload so all threshold/scope
///    logic lives in one place.
/// 4. ConsecutiveAbsenceAlert is dispatched for every absence; the dispatcher
///    applies the ThresholdValue gate (Phase 2). Until Phase 2 lands, it
///    over-sends — do not create ConsecutiveAbsenceAlert triggers in test data.
/// </summary>
public sealed class AttendanceNotifier : IAttendanceNotifier
{
    private readonly IMessageDispatcher _dispatcher;
    private readonly ILogger<AttendanceNotifier> _logger;

    public AttendanceNotifier(
        IMessageDispatcher dispatcher,
        ILogger<AttendanceNotifier> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    // ── PRESENT ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task OnStudentAttendedAsync(
        long teacherId,
        long teacherStudentId,
        long sessionId,
        string sessionName,
        DateTime occurrenceDate)
    {
        var request = new DispatchRequest
        {
            TeacherId = teacherId,
            EventType = TriggerEventType.StudentAttended,
            StudentIds = new List<long> { teacherStudentId },
            SessionId = sessionId,
            SharedContext = new MessageResolveContext
            {
                SessionName = sessionName,
                Date = occurrenceDate,
                AttendanceStatus = "Attended"
            }
        };

        return SafeDispatchAsync(request);
    }

    // ── ABSENT — SINGLE (delegates to bulk) ──────────────────────────────────

    /// <inheritdoc />
    public Task OnStudentAbsentAsync(
        long teacherId,
        long teacherStudentId,
        long sessionId,
        string sessionName,
        DateTime occurrenceDate,
        int consecutiveAbsences)
        => OnStudentsAbsentAsync(
            teacherId,
            sessionId,
            sessionName,
            occurrenceDate,
            new[] { (teacherStudentId, consecutiveAbsences) });

    // ── ABSENT — BULK ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task OnStudentsAbsentAsync(
        long teacherId,
        long sessionId,
        string sessionName,
        DateTime occurrenceDate,
        IReadOnlyCollection<(long teacherStudentId, int consecutiveAbsences)> students)
    {
        if (students.Count == 0) return;

        var studentIds = students.Select(s => s.teacherStudentId).ToList();

        // Per-student context because each student may have a different
        // ConsecutiveAbsences count after the bulk mark.
        var perStudentContext = students.ToDictionary(
            s => s.teacherStudentId,
            s => new MessageResolveContext
            {
                SessionName = sessionName,
                Date = occurrenceDate,
                AttendanceStatus = "Absent",
                AbsenceCount = s.consecutiveAbsences
            });

        // 1. StudentAbsent — always dispatched for every absent student.
        await SafeDispatchAsync(new DispatchRequest
        {
            TeacherId = teacherId,
            EventType = TriggerEventType.StudentAbsent,
            StudentIds = studentIds,
            SessionId = sessionId,
            PerStudentContext = perStudentContext
        });

        // 2. ConsecutiveAbsenceAlert — dispatched for all students now;
        //    Phase 2 adds the per-student ThresholdValue gate inside the dispatcher
        //    so only students whose AbsenceCount >= ThresholdValue receive the message.
        await SafeDispatchAsync(new DispatchRequest
        {
            TeacherId = teacherId,
            EventType = TriggerEventType.ConsecutiveAbsenceAlert,
            StudentIds = studentIds,
            SessionId = sessionId,
            PerStudentContext = perStudentContext  // reuse — same data, same context
        });
    }

    // ── SAFE WRAPPER ──────────────────────────────────────────────────────────

    private async Task SafeDispatchAsync(DispatchRequest request)
    {
        try
        {
            await _dispatcher.DispatchAsync(request);
        }
        catch (Exception ex)
        {
            // Log but never propagate — the attendance write already committed.
            _logger.LogError(ex,
                "[AttendanceNotifier] Dispatch failed for event {Event} " +
                "(Teacher={TeacherId}, Session={SessionId}, Students={Count}). " +
                "Attendance is committed; notification was not delivered.",
                request.EventType, request.TeacherId,
                request.SessionId, request.StudentIds.Count);
        }
    }
}
