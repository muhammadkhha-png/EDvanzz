namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// Input DTO for updating a student user's profile settings.
/// AAM-FR-02.3: Language preference changeable from account settings.
/// 
/// Note: FullName, PhoneNumber, Email, and Password updates are handled
/// by the User module — they live on the shared User record, not here.
/// </summary>
public class UpdateStudentUserProfileDto
{
    /// <summary>
    /// Updated UI language preference. "en" or "ar".
    /// AAM-FR-02.3: Changeable at any time from settings.
    /// </summary>
    public string? LanguagePreference { get; set; }
}