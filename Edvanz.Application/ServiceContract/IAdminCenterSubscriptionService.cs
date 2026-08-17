using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Center;

namespace Edvanz.Application.ServiceContract;

/// <summary>SuperAdmin management of center subscriptions (the quota package): activate/override,
/// approve/reject center requests, and edit per-slot pricing. Mirrors IAdminSubscriptionService.</summary>
public interface IAdminCenterSubscriptionService
{
    Task<Result<string>> ActivateAsync(long adminUserId, ActivateCenterSubscriptionDto dto);
    Task<Result<List<CenterSubscriptionRequestQueueItemDto>>> GetPendingRequestsAsync();
    Task<Result<string>> ApproveRequestAsync(long adminUserId, long requestId, ApproveCenterSubscriptionRequestDto dto);
    Task<Result<string>> RejectRequestAsync(long adminUserId, long requestId, RejectCenterSubscriptionRequestDto dto);
    Task<Result<CenterPricingDto>> GetPricingAsync();
    Task<Result<CenterPricingDto>> UpdatePricingAsync(long adminUserId, CenterPricingDto dto);
}
