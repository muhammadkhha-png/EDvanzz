namespace Edvanz.Application.Dtos.Teacher;

/// <summary>
/// Minimal teacher information exposed to students when adding a teacher.
/// AAM-FR-05.6: Displays Teacher's full name and subject name on student dashboard.
/// Does not expose internal IDs, configuration, or subscription data.
/// </summary>
public class TeacherPublicInfoDto
{
    public string TeacherCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string SubjectName { get; set; } = null!;
}