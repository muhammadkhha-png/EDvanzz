using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Lifecycle of a teacher-initiated NEW-subscription request (a brand-new or renewing
/// tutor asking the super admin to activate a Full or Managerial subscription for a chosen
/// number of students). Distinct from <see cref="CapacityRequestStatus"/>, which raises the
/// roster limit on an ALREADY-active subscription.
///
/// One LIVE Pending row per teacher is enforced by the filtered unique index
/// UX_SubscriptionRequests_Teacher_Pending ([Status] = 1) — keep that literal in sync with
/// <see cref="Pending"/> (CapacityIncreaseRequest precedent).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionRequestStatus : byte
{
    /// <summary>Submitted by the teacher; awaiting super-admin review.</summary>
    Pending = 1,

    /// <summary>Approved — a subscription of the requested plan/capacity was activated for the teacher.</summary>
    Approved = 2,

    /// <summary>Rejected by the super admin (RejectionReason carries the why).</summary>
    Rejected = 3,

    /// <summary>Withdrawn by the teacher before resolution.</summary>
    Cancelled = 4
}
