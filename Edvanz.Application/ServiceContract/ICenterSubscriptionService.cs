using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Center;

namespace Edvanz.Application.ServiceContract;

/// <summary>Center-facing subscription: read the current entitlement/status and submit/cancel a
/// package request (SuperAdmin approves).</summary>
public interface ICenterSubscriptionService
{
    Task<Result<CenterSubscriptionDto>> GetSubscriptionAsync(long centerId);
    Task<Result<string>> SubmitRequestAsync(long centerId, long userId, SubmitCenterSubscriptionRequestDto dto);
    Task<Result<string>> CancelRequestAsync(long centerId, long userId);
}
