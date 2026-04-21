using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Helpers;

/// <summary>
/// Single source of truth for deriving SubscriptionStatus from a TeacherSubscription row.
///
/// Context (Critique C-6 / D-08):
///   The SubscriptionStatus column was removed from TeacherSubscription. Status is
///   derived at read time from (IsCurrent, StartDate, EndDate). This helper centralizes
///   that derivation so every place that used to read the column now calls ONE method.
///
/// Why a helper (not a property on the entity):
///   - The derivation depends on "now" (UtcNow), which is not deterministic on an entity.
///   - Unit tests need to inject a fixed "now" for date-math scenarios.
///   - EF Core cannot translate instance-method calls in LINQ expressions — so where
///     the derivation needs to run inside an IQueryable, use
///     BuildStatusExpression() which returns an Expression&lt;Func&lt;TeacherSubscription, SubscriptionStatus&gt;&gt;
///     that EF Core CAN translate to SQL CASE.
///
/// Rules (authoritative, matching v1.1 analysis §1.1 FR-SUB-004):
///   - IsCurrent = false → Expired (treated as historical / non-active)
///   - now &lt; StartDate  → Expired (future-dated subscription — not yet active; EC-21)
///   - now &gt;= EndDate  → Expired
///   - EndDate - now &lt;= 5 days → ExpiringSoon
///   - otherwise → Active
/// </summary>
public static class SubscriptionStatusCalculator
{
    /// <summary>
    /// Threshold (in days) for the "ExpiringSoon" status.
    /// REQ-SUB-003 + REQ-SUB-005: 5-day alert window.
    /// </summary>
    public const int ExpiringSoonThresholdDays = 5;

    /// <summary>
    /// Derives the status of a subscription at the given moment.
    /// Pass DateTime.UtcNow in production; pass a fixed time in tests.
    /// </summary>
    /// <param name="subscription">The subscription row. If null → Expired.</param>
    /// <param name="nowUtc">The reference moment (typically DateTime.UtcNow).</param>
    /// <returns>The derived SubscriptionStatus.</returns>
    public static SubscriptionStatus Derive(TeacherSubscription? subscription, DateTime nowUtc)
    {
        if (subscription is null)
            return SubscriptionStatus.Expired;

        // Historical (non-current) rows are treated as Expired for display purposes.
        // Callers that need to distinguish historical from actually-expired should filter
        // by IsCurrent at the query level.
        if (!subscription.IsCurrent)
            return SubscriptionStatus.Expired;

        // Future-dated: super admin can pre-create a subscription starting tomorrow.
        // Until StartDate is reached, the teacher is not active (EC-21).
        if (nowUtc < subscription.StartDate)
            return SubscriptionStatus.Expired;

        if (nowUtc >= subscription.EndDate)
            return SubscriptionStatus.Expired;

        var daysRemaining = (subscription.EndDate - nowUtc).TotalDays;
        if (daysRemaining <= ExpiringSoonThresholdDays)
            return SubscriptionStatus.ExpiringSoon;

        return SubscriptionStatus.Active;
    }

    /// <summary>
    /// Computes remaining days for display. Zero or negative are clamped to zero.
    /// </summary>
    public static int DeriveDaysRemaining(TeacherSubscription? subscription, DateTime nowUtc)
    {
        if (subscription is null || !subscription.IsCurrent)
            return 0;

        var remaining = (subscription.EndDate.Date - nowUtc.Date).Days;
        return Math.Max(0, remaining);
    }
}
