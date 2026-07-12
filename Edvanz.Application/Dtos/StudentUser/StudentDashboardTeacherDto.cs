namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// Output DTO representing a single Teacher entry on the Student's dashboard.
/// One entry per teacher — the LATEST link row for that teacher, so the student
/// sees the current state of their request (Pending / Active / Rejected / ...).
/// AAM-FR-05.6: Displays the Teacher's full name and subject name.
/// AAM-FR-05.7: Each Teacher is displayed distinctly.
/// </summary>
public class StudentDashboardTeacherDto
{
    /// <summary>
    /// The StudentTeacherLink Id — used for unlink operations.
    /// </summary>
    public long LinkId { get; set; }

    /// <summary>
    /// Link lifecycle state as a string (Pending, Active, Rejected, Unlinked,
    /// RemovedByTeacher, CancelledByStudent). This is how the student KNOWS
    /// whether their request was accepted or rejected.
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>UTC timestamp the student submitted the link request.</summary>
    public DateTime? RequestedAt { get; set; }

    /// <summary>UTC timestamp the teacher accepted or rejected the request.</summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>
    /// Teacher's unique 8-digit code. Displayed on the dashboard for reference.
    /// </summary>
    public string TeacherCode { get; set; } = null!;

    /// <summary>
    /// Teacher's full name from the User table.
    /// AAM-FR-05.6: Displayed upon successful linking.
    /// </summary>
    public string TeacherFullName { get; set; } = null!;

    /// <summary>
    /// Subject name taught by the teacher.
    /// AAM-FR-05.6: Displayed alongside the teacher's name.
    /// Shows the first ministry-defined subject or the custom subject.
    /// </summary>
    public string SubjectName { get; set; } = null!;

    /// <summary>
    /// When the link became Active (the teacher accepted the request).
    /// Meaningful only for Active/Unlinked/RemovedByTeacher entries.
    /// </summary>
    public DateTime LinkedAt { get; set; }

    /// <summary>
    /// Whether the teacher's student record for this student still exists.
    /// False if the teacher deleted the student record after linking.
    /// </summary>
    public bool IsEnrollmentActive { get; set; }

    // ─── Visibility flags from TeacherConfiguration (AAM-FR-04.8 / AAM-FR-05.8) ───

    /// <summary>
    /// Whether this teacher allows the student to see the Attendance Track.
    /// </summary>
    public bool VisibilityAttendance { get; set; }

    /// <summary>
    /// Whether this teacher allows the student to see the Payment Track.
    /// </summary>
    public bool VisibilityPayment { get; set; }

    /// <summary>
    /// Whether this teacher allows the student to see the Homework Track.
    /// </summary>
    public bool VisibilityHomework { get; set; }

    /// <summary>
    /// Default exam visibility for this teacher.
    /// AAM-BR-10: Per-exam visibility defaults to hidden unless explicitly enabled.
    /// </summary>
    public bool VisibilityExamDefault { get; set; }
}