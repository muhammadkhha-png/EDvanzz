namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// Input DTO for initializing a StudentUser record after user registration.
/// Called by the User module after creating a User with UserType = Student.
/// Captures AAM-FR-05.1 through 05.3 registration requirements.
/// </summary>
public class CreateStudentUserDto
{
    /// <summary>
    /// The UserId from the User table. Must reference an existing user with UserType = Student.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Student's preferred UI language. "en" or "ar".
    /// AAM-FR-02.1: Selected during registration.
    /// </summary>
    public string? LanguagePreference { get; set; }
}