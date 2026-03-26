namespace Edvanz.Domain.Enums;

/// <summary>
/// Represents the current status of a teacher's subscription period.
/// REQ-SUB-003: Active, Expiring Soon, or Expired.
/// </summary>
public enum SubscriptionStatus
{
    Active = 1,
    ExpiringSoon = 2,
    Expired = 3
}