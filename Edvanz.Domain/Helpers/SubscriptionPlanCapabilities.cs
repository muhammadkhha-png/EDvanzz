using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Helpers;

/// <summary>
/// SINGLE source of truth for the plan → feature mapping. Every subscription-plan gate
/// (student-account linking, in-app parent-account linking, the public parent follow-up page)
/// must consult these predicates instead of comparing <see cref="SubscriptionPlanType"/> values
/// inline, so adding a plan is a one-file change here plus its pricing.
///
/// Semantics are RESTRICTION-shaped on purpose: each predicate answers "does this plan, while
/// ACTIVE, forbid the feature?". Whether the plan is actually active (Active/ExpiringSoon) is the
/// caller's concern (SubscriptionGateService) — an expired or missing subscription imposes no
/// plan restriction at all (free-tier behavior, where the module quotas gate instead).
/// </summary>
public static class SubscriptionPlanCapabilities
{
    /// <summary>
    /// True when the plan forbids STUDENT app accounts and in-app PARENT accounts being linked
    /// to the teacher (student link request / teacher accept / teacher bind / parent-child link).
    /// Managerial and ManagerialPlus both forbid them; Full (or any legacy/zero value) allows.
    /// </summary>
    public static bool BlocksStudentAndParentAccounts(SubscriptionPlanType planType) =>
        planType == SubscriptionPlanType.Managerial || planType == SubscriptionPlanType.ManagerialPlus;

    /// <summary>
    /// True when the plan forbids the public parent follow-up page (parent portal): only plain
    /// Managerial does. Full and ManagerialPlus include the portal; a legacy/zero value allows.
    /// </summary>
    public static bool BlocksParentFollowUp(SubscriptionPlanType planType) =>
        planType == SubscriptionPlanType.Managerial;
}
