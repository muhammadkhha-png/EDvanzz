namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// Output DTO representing a student user's full profile.
/// Used by GetStudentUserProfileAsync and returned after InitializeStudentUserAsync.
/// </summary>
public class StudentUserProfileDto
{
    /// <summary>
    /// The StudentUser record Id (not the UserId).
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The underlying User record Id.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Unique, immutable student account code.
    /// AAM-FR-05.3: Auto-generated during registration.
    /// </summary>
    public string StudentAccountCode { get; set; } = null!;

    /// <summary>
    /// Student's full name from the User table.
    /// </summary>
    public string FullName { get; set; } = null!;

    /// <summary>
    /// Student's email from the User table.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Student's phone number from the User table.
    /// AAM-FR-05.2: Mandatory during registration.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Preferred UI language. "en" or "ar".
    /// </summary>
    public string? LanguagePreference { get; set; }

    /// <summary>
    /// Current account status: Active, Inactive, or Suspended.
    /// </summary>
    public string AccountStatus { get; set; } = null!;

    /// <summary>
    /// Whether this is the student's first login (no teachers linked yet).
    /// AAM-FR-05.4: Dashboard is empty with a prompt to add a Teacher.
    /// </summary>
    public bool IsFirstLogin { get; set; }

    /// <summary>
    /// When the student account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Number of teachers currently linked to this student.
    /// AAM-FR-05.7: Student can add multiple teachers.
    /// </summary>
    public int LinkedTeacherCount { get; set; }
}