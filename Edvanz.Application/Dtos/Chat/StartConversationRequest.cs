namespace Edvanz.Application.Dtos.Chat;

/// <summary>
/// Input for POST /api/chat/conversations — opens or retrieves an existing 1:1
/// conversation with the target user. The calling user is resolved from the JWT;
/// never supplied in the request body.
/// </summary>
public class StartConversationRequest
{
    /// <summary>
    /// User.Id of the intended conversation partner.
    /// Must differ from the caller's own User.Id.
    /// </summary>
    public long TargetUserId { get; set; }
}