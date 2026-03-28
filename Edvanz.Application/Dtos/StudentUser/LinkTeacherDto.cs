namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// Input DTO for linking a Teacher to the Student's dashboard.
/// AAM-FR-05.5: All three credentials are required to establish the link.
/// </summary>
public class LinkTeacherDto
{
    /// <summary>
    /// The Teacher's unique 8-digit Teacher Code.
    /// AAM-FR-05.5 credential #1.
    /// AAM-FR-03.3: 8-digit numeric code, unique and immutable.
    /// </summary>
    public string TeacherCode { get; set; } = null!;

    /// <summary>
    /// The student's unique code as assigned by the Teacher.
    /// AAM-FR-05.5 credential #2.
    /// This is the TeacherStudent.StudentCode (teacher-scoped), NOT the StudentAccountCode.
    /// </summary>
    public string StudentCode { get; set; } = null!;

    /// <summary>
    /// The hash/token generated for that student under that specific Teacher.
    /// AAM-FR-05.5 credential #3.
    /// This is the TeacherStudent.HashedToken value.
    /// </summary>
    public string HashedToken { get; set; } = null!;
}