using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Center;

namespace Edvanz.Application.ServiceContract;

/// <summary>Center management of its assistants (logins that span all the center's teachers).</summary>
public interface ICenterAssistantService
{
    Task<Result<CenterAssistantListItemDto>> CreateAsync(long centerId, long actingUserId, CreateCenterAssistantDto dto);
    Task<Result<List<CenterAssistantListItemDto>>> GetAssistantsAsync(long centerId);
    Task<Result<string>> DeactivateAsync(long centerId, long centerAssistantId);
    Task<Result<string>> ReactivateAsync(long centerId, long centerAssistantId);
}
