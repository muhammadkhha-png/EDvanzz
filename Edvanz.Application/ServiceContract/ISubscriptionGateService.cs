namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Business-layer gate for the free-tier model. Answers "is this teacher subscribed?" and, for
/// unsubscribed teachers, whether they may still create another item in a given module under its
/// configured quota (see the ModuleQuota table / ModuleQuotaKeys).
/// </summary>
public interface ISubscriptionGateService
{
    /// <summary>
    /// True when the teacher has an Active or ExpiringSoon subscription (i.e. quotas are lifted).
    /// False for expired / never-subscribed teachers, who are held to the free-tier caps.
    /// </summary>
    Task<bool> HasActiveSubscriptionAsync(long teacherId);

    /// <summary>
    /// True when the teacher's CURRENT subscription is a Managerial plan AND is still
    /// active (Active or ExpiringSoon). A managerial subscription forbids any student or
    /// parent account from being linked to the teacher and forbids direct roster additions,
    /// so every student/parent-linking chokepoint calls this and returns Forbidden when true.
    /// False for Full subscriptions, expired/never-subscribed teachers, and (defensively)
    /// any row whose plan type is not explicitly Managerial.
    /// </summary>
    Task<bool> IsManagerialAsync(long teacherId);

    /// <summary>
    /// The teacher's live plan entitlements, resolved from the current-subscription projection
    /// (center-owned teachers redirect through <c>Teacher.CenterPlanType</c> in that projection)
    /// via <see cref="Domain.Helpers.SubscriptionPlanCapabilities"/> — the single plan → feature
    /// map. Restrictions apply ONLY while the plan is Active/ExpiringSoon; an expired or missing
    /// subscription returns everything-allowed (free-tier behavior, where module quotas gate
    /// creation instead). Every plan-feature chokepoint reads THIS, never the plan value inline.
    /// </summary>
    Task<SubscriptionPlanEntitlements> GetPlanEntitlementsAsync(long teacherId);

    /// <summary>
    /// Whether the teacher may create another item in <paramref name="moduleKey"/>:
    /// subscribed → always true; otherwise the current count (from <paramref name="currentCountFactory"/>)
    /// must be below the module's free-tier limit. The count factory is only invoked when needed
    /// (skipped for subscribers and for subscriber-only modules whose limit is 0).
    /// </summary>
    Task<bool> CanCreateAsync(long teacherId, string moduleKey, Func<Task<int>> currentCountFactory);
}

/// <summary>
/// A teacher's live plan entitlements (see <see cref="ISubscriptionGateService.GetPlanEntitlementsAsync"/>).
/// <see cref="PlanType"/> is the current subscription's plan when one exists (of any status), else null.
/// The two flags already account for the plan's ACTIVE state: a restriction only shows as false while
/// the restricting plan is Active/ExpiringSoon.
/// </summary>
/// <param name="PlanType">Current plan, or null when the teacher has no subscription row.</param>
/// <param name="StudentAccountsAllowed">May student app accounts / in-app parent accounts be linked?</param>
/// <param name="ParentFollowUpAllowed">May the public parent follow-up page (portal) be used?</param>
public readonly record struct SubscriptionPlanEntitlements(
    Domain.Enums.SubscriptionPlanType? PlanType,
    bool StudentAccountsAllowed,
    bool ParentFollowUpAllowed)
{
    /// <summary>The unrestricted default: no subscription (or an inactive one) restricts nothing.</summary>
    public static SubscriptionPlanEntitlements Unrestricted(Domain.Enums.SubscriptionPlanType? planType = null) =>
        new(planType, StudentAccountsAllowed: true, ParentFollowUpAllowed: true);
}
