using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Tracks a teacher's subscription periods. One teacher has many subscription records over time.
/// REQ-SUB-001: Active subscription required for all system access.
/// REQ-SUB-002: Monthly subscription model.
/// REQ-SUB-003: Records start date, end date, days remaining, and current status.
/// REQ-ADM-012/016: Super admin can manually activate, extend, or override subscriptions.
/// </summary>
public class TeacherSubscription : BaseEntity
{
    /// <summary>
    /// Foreign key to the Teacher this subscription belongs to.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Current status of this subscription period: Active, ExpiringSoon, or Expired.
    /// REQ-SUB-003: Displayed in the teacher's account settings at all times.
    /// </summary>
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Active;

    /// <summary>
    /// Date the subscription period begins.
    /// REQ-SUB-004: Begins on the date of successful payment confirmation.
    /// BR-SUB-003: Early renewal starts from the current end date, not payment date.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Date the subscription period ends.
    /// REQ-SUB-004: Exactly one calendar month from start date.
    /// REQ-ADM-015: Super admin can set a custom end date.
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// The user who created this subscription record.
    /// REQ-ADM-016: All subscription changes by super admin are recorded.
    /// Null if system-generated (e.g., auto-renewal).
    /// </summary>
    [ForeignKey(nameof(CreatedByUser))]
    public long? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}