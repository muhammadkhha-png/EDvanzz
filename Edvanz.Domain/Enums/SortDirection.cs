using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Sort direction for list queries. Shared across all modules.
/// Used by PaginatedRequest, StudentListRequest, and repository query builders.
/// 
/// Lives in the Domain layer because it is referenced by repository interfaces
/// (e.g., ITeacherStudentRepo.BuildStudentListQuery) which must not depend on Application.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SortDirection
{
    Asc,
    Desc
}