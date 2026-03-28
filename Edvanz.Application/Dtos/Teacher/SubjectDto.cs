namespace Edvanz.Application.Dtos.Teacher;

/// <summary>
/// Output DTO for a subject from the ministry-defined subjects list.
/// Used in registration dropdowns and teacher profile display.
/// </summary>
public class SubjectDto
{
    public long Id { get; set; }
    public string NameEn { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public int DisplayOrder { get; set; }
}