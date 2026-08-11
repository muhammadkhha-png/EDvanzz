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
///   - <see cref="Full"/>       — the standard subscription; the teacher may have
///                                students and parents linked to them.
///   - <see cref="Managerial"/> — an activated subscription that BLOCKS any student
///                                or parent account from being linked to the teacher
///                                (and blocks direct roster additions) while it is the
///                                current, active subscription. Used for accounts that
///                                only manage/oversee and never teach students directly.
///
/// Stored as tinyint. Existing rows are backfilled to <see cref="Full"/> by migration,
/// so a missing/zero value is treated as Full (never Managerial) by the gate — the
/// block is applied ONLY on an explicit Managerial value.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionPlanType : byte
{
    /// <summary>Standard subscription — students and parents may be linked.</summary>
    Full = 1,

    /// <summary>Managerial subscription — no students or parents may be linked.</summary>
    Managerial = 2
}
