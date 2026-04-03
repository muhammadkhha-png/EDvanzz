namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Integration interface for the Messaging Module (Module 8) to receive
/// attendance-related events for automated notifications.
/// Step 7.1: Created as a stub — the Messaging Module will provide the real implementation.
///
/// Lives in Application layer (interface only). Implementation will be in Infrastructure
/// when the Messaging Module is built.
///
/// REQ-ATT-028: Absence notifications when student is marked absent.
/// REQ-MSG-004: Automated message triggers based on attendance events.
/// </summary>
public interface IMessagingIntegrationService
{
    /// <summary>
    /// Notifies the messaging system that a student was marked absent.
    /// Called after marking a student as Absent in MarkAttendanceAsync and BulkMarkAttendanceAsync.
    /// The messaging implementation decides whether to send WhatsApp/SMS based on
    /// the teacher's configured message templates and triggers.
    /// </summary>
    /// <param name="teacherId">The teacher who owns the student and session.</param>
    /// <param name="teacherStudentId">The student who was marked absent.</param>
    /// <param name="studentName">The student's display name.</param>
    /// <param name="consecutiveAbsences">Current consecutive absence count after this marking.</param>
    /// <param name="sessionName">The session where the absence was recorded.</param>
    /// <param name="occurrenceDate">The date of the absence.</param>
    Task NotifyStudentAbsenceAsync(
        long teacherId,
        long teacherStudentId,
        string studentName,
        int consecutiveAbsences,
        string sessionName,
        DateTime occurrenceDate);

    /// <summary>
    /// Notifies the messaging system that a student has reached a consecutive absence threshold.
    /// Called when ConsecutiveAbsences crosses configurable thresholds (e.g., 3, 5, 10).
    /// </summary>
    /// <param name="teacherId">The teacher who owns the student.</param>
    /// <param name="teacherStudentId">The student with consecutive absences.</param>
    /// <param name="studentName">The student's display name.</param>
    /// <param name="consecutiveAbsences">The current consecutive count.</param>
    /// <param name="sessionName">The session name.</param>
    Task NotifyConsecutiveAbsenceThresholdAsync(
        long teacherId,
        long teacherStudentId,
        string studentName,
        int consecutiveAbsences,
        string sessionName);
}