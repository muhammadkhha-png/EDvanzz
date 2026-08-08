namespace Edvanz.Application.Dtos.ParentUser;

/// <summary>
/// Input DTO for linking a Teacher to a Method B child (manual profile).
/// AAM-FR-06.3 Method B: Same three credentials as AAM-FR-05.5.
/// </summary>
public class LinkTeacherToChildDto
{
    /// <summary>
    /// The Teacher's unique 8-digit Teacher Code.
    /// AAM-FR-05.5 credential #1.
    /// </summary>
    public string TeacherCode { get; set; } = null!;

    /// <summary>
    /// The student's unique code as assigned by the Teacher.
    /// AAM-FR-05.5 credential #2.
    /// </summary>
    public string StudentCode { get; set; } = null!;

   
}