using Edvanz.Domain.Constants;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Helpers;

/// <summary>
/// The ONE place a subscription's monthly value in EGP is derived from a plan.
///
/// WHY IT EXISTS: the formula is plan-aware and easy to get subtly wrong. Full is
/// <c>LinkedStudentCapacity × the per-student rate</c> (BR-SUB-009); the two managerial plans
/// renew at a FLAT monthly price and must never be shown a per-student figure they would not be
/// charged. That distinction already had to be fixed once inside
/// <c>SubscriptionService.ComputeRenewalPriceAsync</c>, where it lived as a private method — so the
/// admin console, which sums the same value across every active subscription, could not reuse it
/// without copying it. A copied money formula is a copy that drifts.
///
/// This helper is deliberately PURE: the caller supplies the pricing row's three rates, so the
/// per-teacher path can load them once and the console can load them once for the whole platform
/// rather than issuing one settings read per teacher.
/// </summary>
public static class SubscriptionPricing
{
    /// <summary>
    /// The monthly value of a subscription on <paramref name="planType"/>, at the supplied rates.
    /// Returns 0 when the inputs cannot produce an honest number (no plan, an unconfigured rate, or
    /// a capacity outside the supported range) — 0 reads as "we cannot price this", never as free.
    /// </summary>
    /// <param name="planType">The plan on the subscription row. Null ⇒ no subscription ⇒ 0.</param>
    /// <param name="linkedStudentCapacity">
    /// The teacher's student-app-account capacity — the number the Full plan is priced on.
    /// Ignored by the managerial plans.
    /// </param>
    /// <param name="pricePerStudentEGP">Full-plan rate per linked student account.</param>
    /// <param name="managerialMonthlyEGP">Flat monthly price of the Managerial plan.</param>
    /// <param name="managerialPlusMonthlyEGP">Flat monthly price of the Managerial + Parents plan.</param>
    public static decimal MonthlyValueEGP(
        SubscriptionPlanType? planType,
        int linkedStudentCapacity,
        decimal pricePerStudentEGP,
        decimal managerialMonthlyEGP,
        decimal managerialPlusMonthlyEGP)
    {
        if (planType is null) return 0m;

        // The two managerial plans renew at their FLAT monthly price — capacity × rate is a
        // Full-plan formula only.
        if (planType == SubscriptionPlanType.Managerial) return managerialMonthlyEGP;
        if (planType == SubscriptionPlanType.ManagerialPlus) return managerialPlusMonthlyEGP;

        if (linkedStudentCapacity <= 0 ||
            linkedStudentCapacity > SubscriptionConstants.MaxStudentCapacity) return 0m;

        if (pricePerStudentEGP <= 0m) return 0m;

        return linkedStudentCapacity * pricePerStudentEGP;
    }
}
