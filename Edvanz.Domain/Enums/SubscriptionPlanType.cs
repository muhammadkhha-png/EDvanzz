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
///   - <see cref="Full"/>       — the standard subscription; students may link their
///                                account to the teacher.
///   - <see cref="Managerial"/> — an activated subscription under which the teacher works
///                                NORMALLY (roster students, bulk import, parent links all
///                                allowed) EXCEPT that no student ACCOUNT may be linked to
///                                them: the student-account link flow (student link request,
///                                teacher accept, teacher bind) is blocked while it is the
///                                current, active subscription. Used for accounts that manage
///                                a roster but expose no app access to student accounts.
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
