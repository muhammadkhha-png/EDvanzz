using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Center;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Center-facing self-service: overview, and management of the center's teacher PROFILES (which have
/// no login of their own). The center operates each teacher's data by "acting as" it (the acting-as
/// resolvers), not through this service.
/// </summary>
public interface ICenterService
{
    Task<Result<CenterOverviewDto>> GetOverviewAsync(long centerId);

    /// <summary>Center's own settings (revenue-share %, student-code mode) — center-controlled.</summary>
    Task<Result<CenterSettingsDto>> GetSettingsAsync(long centerId);
    Task<Result<CenterSettingsDto>> UpdateSettingsAsync(long centerId, UpdateCenterSettingsDto dto);

    Task<Result<List<CenterTeacherListItemDto>>> GetTeachersAsync(long centerId);
    Task<Result<CenterTeacherListItemDto>> CreateTeacherAsync(long centerId, long actingUserId, CreateCenterTeacherDto dto);
    Task<Result<CenterTeacherListItemDto>> UpdateTeacherAsync(long centerId, long teacherId, UpdateCenterTeacherDto dto);
    Task<Result<string>> DeactivateTeacherAsync(long centerId, long teacherId);
    Task<Result<string>> ReactivateTeacherAsync(long centerId, long teacherId);

    /// <summary>Center-wide exact-match code resolve — returns one candidate per teacher using the
    /// code (0/1/many) so a shared code can be disambiguated at the front desk.</summary>
    Task<Result<List<CenterStudentResolveCandidateDto>>> ResolveStudentByCodeAsync(long centerId, string? code);
}
