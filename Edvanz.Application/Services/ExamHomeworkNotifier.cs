using Edvanz.Application.Dtos.DispatcherDtos;
using Edvanz.Application.Dtos.MessageResolver;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities.Messaging;
using Microsoft.Extensions.Logging;

namespace Edvanz.Application.Services;

/// <summary>
/// Translates exam and homework domain events into IMessageDispatcher calls.
/// Part of Option-B: per-module notifier over IMessageDispatcher.
///
/// Design rules:
/// 1. Never throws — all dispatch failures are caught, logged, and swallowed.
///    The originating write already committed before this runs.
/// 2. Called post-commit only (after SaveChangesAsync succeeds in ExamHomeworkService).
/// 3. GradeBelowThreshold is dispatched alongside ExamGradeRecorded for every grade;
///    Phase 2 adds the ThresholdValue gate inside the dispatcher so only grades
///    below the configured threshold actually generate a message.
/// 4. Scope: sessionId / sessionGroupId passed through from the caller.
///    If neither is available (AllStudents scope), both are null and the dispatcher
///    matches TriggerScope.All — documented limitation pending Phase 5 scope mapping.
///
/// REQ-MSG-021: ExamGradeRecorded, GradeBelowThreshold, ExamNotAttended.
/// REQ-MSG-022: HomeworkMarkedDone, HomeworkMarkedNotDone, HomeworkGradeRecorded.
/// </summary>
public sealed class ExamHomeworkNotifier : IExamHomeworkNotifier
{
    private readonly IMessageDispatcher _dispatcher;
    private readonly ILogger<ExamHomeworkNotifier> _logger;

    public ExamHomeworkNotifier(
        IMessageDispatcher dispatcher,
        ILogger<ExamHomeworkNotifier> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    // ── EXAM ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task OnExamGradeRecordedAsync(
        long teacherId,
        long teacherStudentId,
        long? sessionId,
        long? sessionGroupId,
        string examName,
        decimal grade,
        decimal? maxGrade)
    {
        var context = new MessageResolveContext
        {
            Date = DateTime.UtcNow,
            ExamName = examName,
            ExamGrade = grade,
            ExamMaxGrade = maxGrade
        };

        var studentIds = new List<long> { teacherStudentId };

        // 1. ExamGradeRecorded — always fires when a grade is entered.
        await SafeDispatchAsync(new DispatchRequest
        {
            TeacherId = teacherId,
            EventType = TriggerEventType.ExamGradeRecorded,
            StudentIds = studentIds,
            SessionId = sessionId,
            SessionGroupId = sessionGroupId,
            SharedContext = context
        });

        // 2. GradeBelowThreshold — dispatched for every grade;
        //    Phase 2 gate in the dispatcher will filter out students
        //    whose grade is NOT below the configured ThresholdValue.
        await SafeDispatchAsync(new DispatchRequest
        {
            TeacherId = teacherId,
            EventType = TriggerEventType.GradeBelowThreshold,
            StudentIds = studentIds,
            SessionId = sessionId,
            SessionGroupId = sessionGroupId,
            SharedContext = context
        });
    }

    /// <inheritdoc />
    public Task OnExamNotAttendedAsync(
        long teacherId,
        long teacherStudentId,
        long? sessionId,
        long? sessionGroupId,
        string examName)
    {
        var request = new DispatchRequest
        {
            TeacherId = teacherId,
            EventType = TriggerEventType.ExamNotAttended,
            StudentIds = new List<long> { teacherStudentId },
            SessionId = sessionId,
            SessionGroupId = sessionGroupId,
            SharedContext = new MessageResolveContext
            {
                Date = DateTime.UtcNow,
                ExamName = examName
            }
        };

        return SafeDispatchAsync(request);
    }

    // ── HOMEWORK ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task OnHomeworkStatusRecordedAsync(
        long teacherId,
        long teacherStudentId,
        long? sessionId,
        long? sessionGroupId,
        string homeworkName,
        bool isDone)
    {
        // isDone maps to ObligationStatus.Done / DoneWithoutGrade
        // !isDone maps to ObligationStatus.NotDone
        var eventType = isDone
            ? TriggerEventType.HomeworkMarkedDone
            : TriggerEventType.HomeworkMarkedNotDone;

        var request = new DispatchRequest
        {
            TeacherId = teacherId,
            EventType = eventType,
            StudentIds = new List<long> { teacherStudentId },
            SessionId = sessionId,
            SessionGroupId = sessionGroupId,
            SharedContext = new MessageResolveContext
            {
                Date = DateTime.UtcNow,
                HomeworkName = homeworkName,
                HomeworkStatus = isDone ? "Done" : "Not Done"
            }
        };

        return SafeDispatchAsync(request);
    }

    /// <inheritdoc />
    public Task OnHomeworkGradeRecordedAsync(
        long teacherId,
        long teacherStudentId,
        long? sessionId,
        long? sessionGroupId,
        string homeworkName,
        decimal grade,
        decimal? maxGrade)
    {
        // ExamGrade / ExamMaxGrade are the grade fields on MessageResolveContext —
        // they serve double duty for homework grades (no separate field on the context).
        var request = new DispatchRequest
        {
            TeacherId = teacherId,
            EventType = TriggerEventType.HomeworkGradeRecorded,
            StudentIds = new List<long> { teacherStudentId },
            SessionId = sessionId,
            SessionGroupId = sessionGroupId,
            SharedContext = new MessageResolveContext
            {
                Date = DateTime.UtcNow,
                HomeworkName = homeworkName,
                HomeworkStatus = "Done",
                ExamGrade = grade,        // reused field — see comment above
                ExamMaxGrade = maxGrade
            }
        };

        return SafeDispatchAsync(request);
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
            _logger.LogError(ex,
                "[ExamHomeworkNotifier] Dispatch failed for event {Event} " +
                "(Teacher={TeacherId}, Student={StudentId}). " +
                "Assignment write is committed; notification was not delivered.",
                request.EventType, request.TeacherId,
                request.StudentIds.FirstOrDefault());
        }
    }
}
