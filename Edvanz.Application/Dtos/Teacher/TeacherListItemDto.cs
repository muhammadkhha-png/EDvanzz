using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Teacher;

/// <summary>
/// Lightweight DTO for teacher list views (Super Admin dashboard).
/// REQ-ADM-026: Shows name, username, student count, subscription status,
/// subscription end date, account status, and last login.
/// Does not include full configuration or subject details.
/// </summary>
public class TeacherListItemDto
{
    public long Id { get; set; }
    public long UserId { get; set; }

    public string FullName { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string TeacherCode { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public int StudentCapacity { get; set; }

    /// <summary>Number of students CREATED (active roster) under this teacher.</summary>
    public int StudentCount { get; set; }

    /// <summary>Number of students who have an ACTIVE account link (connected) to this teacher.</summary>
    public int LinkedStudentCount { get; set; }

    public string AccountStatus { get; set; } = null!;
    public bool IsConfigurationCompleted { get; set; }
    public string? SubscriptionStatus { get; set; }

    /// <summary>
    /// Plan type of the teacher's latest subscription: Full or Managerial.
    /// Null when the teacher has never had a subscription. Serialized as a string.
    /// </summary>
    public SubscriptionPlanType? PlanType { get; set; }

    public DateTime? SubscriptionEndDate { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp of the teacher's most recent successful login, or null if the
    /// teacher has never logged in. Sourced from User.LastLoginAt (REQ-ADM-026).
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// UTC "last seen" — the teacher's most recent authenticated request, stamped by
    /// SessionActivitySlidingMiddleware (±5-minute throttle). Null until the account's
    /// first request after the LastActivityAt column shipped. Activity Monitor column.
    /// </summary>
    public DateTime? LastActivityAt { get; set; }

    /// <summary>
    /// Number of assistant accounts under this teacher — same population as
    /// GET /api/assistant/all?teacherId={Id}, so the Activity Monitor's collapsed row
    /// count always matches what expanding the row loads.
    /// </summary>
    public int AssistantCount { get; set; }

    /// <summary>
    /// The owning Center's id when this teacher is center-owned (operated by a Center account with no
    /// login of its own); null for a standalone teacher. Additive — lets the SuperAdmin list identify
    /// and filter center-owned teachers.
    /// </summary>
    public long? CenterId { get; set; }

    /// <summary>
    /// The Managerial/Full plan of a CENTER-OWNED teacher (which has no individual TeacherSubscription,
    /// so the <see cref="PlanType"/>/<see cref="SubscriptionStatus"/> above are null). Null for standalone.
    /// </summary>
    public SubscriptionPlanType? CenterPlanType { get; set; }
}