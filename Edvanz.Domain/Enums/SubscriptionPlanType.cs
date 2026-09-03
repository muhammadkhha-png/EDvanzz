using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// The kind of subscription a teacher holds.
///
/// A subscription's PERIOD (StartDate/EndDate/IsCurrent) and its derived status
/// (Active/ExpiringSoon/Expired) are independent of this plan type — a Managerial
/// subscription is activated, extended, and expires exactly like a Full one. The
/// only behavioural difference is the roster gate:
///
///   - <see cref="Full"/>           — the standard subscription; students may link their
///                                    account to the teacher.
///   - <see cref="Managerial"/>     — an activated subscription under which the teacher works
///                                    NORMALLY (roster students + bulk import allowed) EXCEPT that
///                                    no student ACCOUNT and no PARENT account may be linked to them:
///                                    the student-account link flow (student link request, teacher
///                                    accept, teacher bind) AND parent-to-child linking are blocked
///                                    while it is the current, active subscription. Used for accounts
///                                    that manage a roster but expose no app access to student/parent
///                                    accounts.
///   - <see cref="ManagerialPlus"/> — everything Managerial allows PLUS the public parent
///                                    follow-up page (parent portal). Student accounts and in-app
///                                    parent accounts stay blocked exactly like Managerial; only the
///                                    portal chokepoints treat it as allowed. Display name is
///                                    "Managerial + Parents" / «إداري + أولياء الأمور» — the enum
///                                    identifier is the stable wire value, clients localize labels.
///
/// The plan → feature mapping is centralized in SubscriptionPlanCapabilities (Domain.Helpers);
/// gate sites must consult it rather than comparing plan values inline.
///
/// Stored as tinyint. Existing rows are backfilled to <see cref="Full"/> by migration,
/// so a missing/zero value is treated as Full (never Managerial) by the gate — the
/// block is applied ONLY on an explicit Managerial/ManagerialPlus value.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionPlanType : byte
{
    /// <summary>Standard subscription — students and parents may be linked.</summary>
    Full = 1,

    /// <summary>Managerial subscription — no students or parents may be linked.</summary>
    Managerial = 2,

    /// <summary>Managerial + Parents — Managerial rules, but the parent follow-up page is included.</summary>
    ManagerialPlus = 3
}
