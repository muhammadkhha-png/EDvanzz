namespace Edvanz.Application.Dtos.ParentUser;

/// <summary>
/// Input DTO for updating a parent user's profile settings.
/// AAM-FR-02.3: Language preference changeable from settings.
/// FullName, PhoneNumber, Password updates go through the User module.
/// </summary>
public class UpdateParentUserProfileDto
{
    /// <summary>
    /// Updated UI language preference. "en" or "ar".
    /// </summary>
    public string? LanguagePreference { get; set; }
}