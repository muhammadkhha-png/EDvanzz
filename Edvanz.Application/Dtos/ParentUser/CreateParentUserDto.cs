namespace Edvanz.Application.Dtos.ParentUser;

/// <summary>
/// Input DTO for initializing a ParentUser record after user registration.
/// Called by the User module after creating a User with UserType = Parent.
/// </summary>
public class CreateParentUserDto
{
    /// <summary>
    /// The UserId from the User table. Must reference an existing user with UserType = Parent.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Parent's preferred UI language. "en" or "ar".
    /// AAM-FR-02.1: Selected during registration.
    /// </summary>
    public string? LanguagePreference { get; set; }
}