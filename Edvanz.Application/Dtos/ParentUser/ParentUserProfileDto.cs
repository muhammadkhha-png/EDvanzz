namespace Edvanz.Application.Dtos.ParentUser;

/// <summary>
/// Output DTO representing a parent user's full profile.
/// </summary>
public class ParentUserProfileDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? LanguagePreference { get; set; }
    public string AccountStatus { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Number of children linked to this parent account.
    /// AAM-FR-06.4: Parent can manage multiple children.
    /// </summary>
    public int ChildCount { get; set; }
}