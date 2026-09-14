using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Output DTO for GET /api/subscription/status — the single backend-driven display contract for
/// every subscription indicator (side-menu badge, home banner, subscription-page status card).
/// ALL presentation logic (whether to warn, what to say, which call-to-action) is decided here so
/// the client renders it verbatim and holds no thresholds or business rules.
/// </summary>
public class SubscriptionStatusDto
{
    /// <summary>True when the teacher has a subscription row (of any status). False for a brand-new tutor.</summary>
    public bool HasSubscription { get; set; }

    /// <summary>Plan of the current subscription, or null when there is none.</summary>
    public SubscriptionPlanType? PlanType { get; set; }

    /// <summary>Derived status (Active / ExpiringSoon / Expired), or null when there is no subscription.</summary>
    public SubscriptionStatus? Status { get; set; }

    /// <summary>Days until expiry. 0 when expired or when there is no subscription.</summary>
    public int DaysRemaining { get; set; }

    /// <summary>Current period end (UTC), or null when there is no subscription.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>What the next renewal would cost (capacity × per-student rate). 0 when not applicable.</summary>
    public decimal RenewalAmountEGP { get; set; }

    /// <summary>
    /// How much attention the UI should draw: "none" (fine), "warning" (expiring soon, ≤5 days),
    /// or "critical" (expired or no subscription). Drives the banner/badge color and visibility —
    /// show a banner whenever this is not "none".
    /// </summary>
    public string AttentionLevel { get; set; } = "none";

    /// <summary>The call-to-action the button should perform: "none", "subscribe" (no plan yet), or "renew".</summary>
    public string CtaType { get; set; } = "none";

    /// <summary>Localized one-line message for the indicator/banner (already in the request's language).</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>True when the teacher already has a Pending subscription request under review (UI shows that instead of a new CTA).</summary>
    public bool HasPendingRequest { get; set; }

    /// <summary>Support/team WhatsApp number for the "contact us" button (E.164 or local, as configured). Null/empty if not configured.</summary>
    public string? WhatsAppNumber { get; set; }

    /// <summary>
    /// The plan's live feature entitlements — the app's ONLY source for plan-based screen gating
    /// (locked parent follow-up / student-links screens). Computed server-side from the single
    /// plan → feature map so clients never hardcode plan semantics. Additive: absent on older
    /// servers, in which case clients must fail OPEN (the server still enforces on action).
    /// </summary>
    public SubscriptionFeaturesDto Features { get; set; } = new();
}

/// <summary>
/// Live plan entitlements for the signed-in teacher (see <see cref="SubscriptionStatusDto.Features"/>).
/// Both default to true — no/expired subscription restricts nothing (free-tier behavior; module
/// quotas gate creation separately).
/// </summary>
public class SubscriptionFeaturesDto
{
    /// <summary>May student app accounts (and in-app parent accounts) be linked? False under Managerial / ManagerialPlus.</summary>
    public bool StudentAccountsAllowed { get; set; } = true;

    /// <summary>May the public parent follow-up page be used? False under plain Managerial only.</summary>
    public bool ParentFollowUpAllowed { get; set; } = true;

    /// <summary>
    /// May the teacher add "Books &amp; fees" (مذكرات ومصاريف) items? The module is SUBSCRIBER-ONLY
    /// (free-tier quota 0), so this is true exactly while a subscription is live.
    ///
    /// <para>UNLIKE its two siblings above this is a QUOTA gate, not a plan-CAPABILITY gate: it is
    /// false with no subscription and false once one expires. Both server build sites assign it
    /// explicitly; the <c>true</c> initializer exists only so a CLIENT reading an older server's
    /// response fails OPEN, per this block's documented contract (the server still enforces on the
    /// create action). Do not "simplify" it to an unconditional true.</para>
    ///
    /// <para>The app must gate the Books &amp; fees card on THIS field — never on
    /// <see cref="SubscriptionStatusDto.HasSubscription"/> plus its own plan reasoning, which is
    /// exactly the hardcoding this block exists to prevent.</para>
    /// </summary>
    public bool ExtrasAllowed { get; set; } = true;

    /// <summary>
    /// How many student APP ACCOUNTS this teacher may have linked at once
    /// (<c>Teacher.LinkedStudentCapacity</c>) — the limit the subscription price is based on.
    /// Distinct from the students-in-the-account quota. Additive: 0 on older servers.
    /// </summary>
    public int LinkedStudentCapacity { get; set; }

    /// <summary>
    /// Live count of seats in use: links that are Active AND bound to a student record.
    /// An accepted-but-unbound connection uses no seat. Compare with
    /// <see cref="LinkedStudentCapacity"/> to render "X of Y app accounts used".
    /// </summary>
    public int LinkedStudentsUsed { get; set; }
}
