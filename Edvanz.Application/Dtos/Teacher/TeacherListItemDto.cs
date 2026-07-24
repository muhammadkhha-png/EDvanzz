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
    public string AccountStatus { get; set; } = null!;
    public bool IsConfigurationCompleted { get; set; }
    public string? SubscriptionStatus { get; set; }
    public DateTime? SubscriptionEndDate { get; set; }
    public DateTime CreatedAt { get; set; }
}