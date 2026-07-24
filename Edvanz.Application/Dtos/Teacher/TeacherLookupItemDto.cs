namespace Edvanz.Application.Dtos.Teacher;

/// <summary>
/// Output shape for GET /api/teacher/lookup — Id + FullName only, for populating
/// select/dropdown controls. Deliberately minimal: no username, code, status,
/// or subscription data (use TeacherListItemDto/the paginated list endpoint for that).
/// </summary>
public class TeacherLookupItemDto
{
    public long Id { get; set; }
    public string FullName { get; set; } = null!;
}