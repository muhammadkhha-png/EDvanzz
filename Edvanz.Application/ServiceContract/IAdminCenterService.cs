using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Center;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// SuperAdmin-facing provisioning of Center accounts: create the login + Center row + code, list,
/// read, and deactivate. Center subscriptions (the quota package) are handled by the admin
/// center-subscription service.
/// </summary>
public interface IAdminCenterService
{
    Task<Result<CenterListItemDto>> CreateCenterAsync(long adminUserId, CreateCenterDto dto);
    Task<Result<List<CenterListItemDto>>> GetCentersAsync();
    Task<Result<CenterListItemDto>> GetCenterByIdAsync(long centerId);
    Task<Result<string>> DeactivateCenterAsync(long adminUserId, long centerId);
    Task<Result<string>> ReactivateCenterAsync(long adminUserId, long centerId);
}
