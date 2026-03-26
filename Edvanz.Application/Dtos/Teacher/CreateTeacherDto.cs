namespace Edvanz.Application.Dtos.Teacher;

/// <summary>
/// Input DTO for initializing a Teacher record after user registration.
/// Captures AAM-FR-03.3 through 03.5 registration requirements.
/// </summary>
public class CreateTeacherDto
{
    /// <summary>
    /// The UserId from the User table. Must reference an existing user with UserType = Teacher.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Selected ministry-defined subject Id(s). At least one is required unless a custom subject is provided.
    /// AAM-FR-03.5: Teacher selects the subject they teach.
    /// </summary>
    public List<long> SubjectIds { get; set; } = new();

    /// <summary>
    /// Optional free-text subject name for subjects not in the ministry list.
    /// AAM-FR-03.5: Custom subject field for unlisted subjects.
    /// </summary>
    public string? CustomSubject { get; set; }

    /// <summary>
    /// Teacher's preferred UI language. "en" or "ar".
    /// AAM-FR-02.1: Selected during registration.
    /// </summary>
    public string? LanguagePreference { get; set; }

    /// <summary>
    /// The UserId of the account creating this teacher (super admin).
    /// REQ-ADM-005/006: Only super admin creates teacher accounts.
    /// Null if self-registration is enabled.
    /// </summary>
    public long? CreatedByUserId { get; set; }

    /// <summary>
    /// Initial student capacity. Defaults to 500 per REQ-STU-002.
    /// REQ-ADM-006: Set by super admin during creation.
    /// </summary>
    public int StudentCapacity { get; set; } = 500;
}