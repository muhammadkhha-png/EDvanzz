namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Intent-revealing contract for exam- and homework-triggered outbound messaging.
/// Part of Option-B: per-module notifier interfaces over IMessageDispatcher.
///
/// Caller: ExamHomeworkService (post-commit, after SaveChangesAsync succeeds).
/// Engine: IMessageDispatcher.DispatchAsync.
///
/// REQ-MSG-021: Automated triggers for ExamGradeRecorded, GradeBelowThreshold,
///              and ExamNotAttended.
/// REQ-MSG-022: Automated triggers for HomeworkMarkedDone, HomeworkMarkedNotDone,
///              and HomeworkGradeRecorded.
/// </summary>
public interface IExamHomeworkNotifier
{
    /// <summary>
    /// Fires when an exam grade is entered for a student
    /// (ObligationStatus transitions to <c>AttendedWithGrade</c>).
    /// Dispatches <see cref="TriggerEventType.ExamGradeRecorded"/>.
    /// Also dispatches <see cref="TriggerEventType.GradeBelowThreshold"/>
    /// (gated against the trigger's ThresholdValue in Phase 2 of the dispatcher).
    /// REQ-MSG-021.
    /// </summary>
    /// <param name="teacherId">Resolved from JWT — never from route or body.</param>
    /// <param name="teacherStudentId">TeacherStudent.Id of the student.</param>
    /// <param name="sessionId">Session scope for trigger matching (null = All scope).</param>
    /// <param name="sessionGroupId">Session-group scope for trigger matching (null = not group-scoped).</param>
    /// <param name="examName">Name of the exam assignment for the message template.</param>
    /// <param name="grade">The grade value entered.</param>
    /// <param name="maxGrade">Maximum possible grade (nullable — not all exams define a max).</param>
    Task OnExamGradeRecordedAsync(
        long teacherId,
        long teacherStudentId,
        long? sessionId,
        long? sessionGroupId,
        string examName,
        decimal grade,
        decimal? maxGrade);

    /// <summary>
    /// Fires when a student's exam status is set to <c>DidNotAttend</c>.
    /// Dispatches <see cref="TriggerEventType.ExamNotAttended"/>.
    /// REQ-MSG-021.
    /// </summary>
    Task OnExamNotAttendedAsync(
        long teacherId,
        long teacherStudentId,
        long? sessionId,
        long? sessionGroupId,
        string examName);

    /// <summary>
    /// Fires when a homework assignment is marked Done or Not Done.
    /// Dispatches <see cref="TriggerEventType.HomeworkMarkedDone"/> when <paramref name="isDone"/> is <c>true</c>,
    /// or <see cref="TriggerEventType.HomeworkMarkedNotDone"/> when <c>false</c>.
    /// REQ-MSG-022.
    /// </summary>
    /// <param name="isDone"><c>true</c> for Done/DoneWithoutGrade; <c>false</c> for NotDone.</param>
    Task OnHomeworkStatusRecordedAsync(
        long teacherId,
        long teacherStudentId,
        long? sessionId,
        long? sessionGroupId,
        string homeworkName,
        bool isDone);

    /// <summary>
    /// Fires when a grade is entered for a graded homework assignment
    /// (ObligationStatus transitions to <c>DoneWithGrade</c>).
    /// Dispatches <see cref="TriggerEventType.HomeworkGradeRecorded"/>.
    /// REQ-MSG-022.
    /// </summary>
    Task OnHomeworkGradeRecordedAsync(
        long teacherId,
        long teacherStudentId,
        long? sessionId,
        long? sessionGroupId,
        string homeworkName,
        decimal grade,
        decimal? maxGrade);
}
