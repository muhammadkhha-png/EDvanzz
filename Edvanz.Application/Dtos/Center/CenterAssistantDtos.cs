using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Center;

/// <summary>Center creates an assistant login (spans all the center's teachers). Has real credentials
/// (unlike center-owned teachers). Granular per-assistant permissions are a later refinement — for
/// now a center assistant operates the center's teachers like the center owner (role-sufficient).</summary>
public class CreateCenterAssistantDto
{
    public string FullName { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? LanguagePreference { get; set; }
}

public class CenterAssistantListItemDto
{
    public long CenterAssistantId { get; set; }
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public AccountStatus AccountStatus { get; set; }
}

/// <summary>Mirrors <see cref="ResetCenterTeacherPasswordDto"/> for the center's assistants.</summary>
public class ResetCenterAssistantPasswordDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.Required]
    public string ConfirmPassword { get; set; } = null!;
}
