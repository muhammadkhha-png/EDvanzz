namespace Edvanz.Application.Dtos.Teacher;

/// <summary>
/// Output DTO for a teacher's subscription record.
/// REQ-SUB-003: Displays start date, end date, days remaining, and status.
/// </summary>
public class TeacherSubscriptionDto
{
    public long Id { get; set; }
    public string SubscriptionStatus { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Computed: number of days remaining in the current subscription period.
    /// Zero or negative if expired.
    /// </summary>
    public int DaysRemaining { get; set; }
}