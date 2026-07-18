namespace Edvanz.Application.Dtos.Teacher;

/// <summary>
/// Output DTO representing a teacher's full profile.
/// Used by GetTeacherProfileAsync and returned after InitializeTeacherAsync.
/// </summary>
public class TeacherProfileDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string TeacherCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public int StudentCapacity { get; set; }
    public string? LanguagePreference { get; set; }
    public string? CustomSubject { get; set; }
    public string AccountStatus { get; set; } = null!;
    public bool IsConfigurationCompleted { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The subject(s) this teacher teaches.
    /// </summary>
    public List<SubjectDto> Subjects { get; set; } = new();

    /// <summary>
    /// Selected capacity package Id, if configured. Exposed so the admin edit screen
    /// can preselect the current tier and re-send it (an unchanged value is a no-op;
    /// changing it on a configured teacher is rejected with CapacityChangeRequiresApproval).
    /// </summary>
    public long? StudentCapacityPackageId { get; set; }

    /// <summary>
    /// Selected capacity package name, if configured.
    /// </summary>
    public string? CapacityPackageName { get; set; }

    /// <summary>
    /// Whether the teacher currently has an Active/ExpiringSoon subscription. When false the
    /// teacher is on the free tier (per-module create quotas apply). This is the single flag the
    /// client should key off to show subscription status / prompts.
    /// </summary>
    public bool IsSubscribed { get; set; }

    /// <summary>
    /// Current active subscription summary. Null if no active subscription.
    /// </summary>
    public TeacherSubscriptionDto? ActiveSubscription { get; set; }
}