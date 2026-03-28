namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// Output DTO representing a single Teacher entry on the Student's dashboard.
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
    /// When the student linked this teacher to their dashboard.
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