using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Lifecycle of a teacher-initiated student-capacity increase request.
/// One LIVE Pending row per teacher is enforced by the filtered unique index
/// UX_CapacityIncreaseRequests_Teacher_Pending ([Status] = 1) — keep that literal
/// in sync with <see cref="Pending"/> (StudentTeacherLink precedent).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CapacityRequestStatus : byte
{
    /// <summary>Submitted by the teacher; awaiting super-admin review.</summary>
    Pending = 1,

    /// <summary>Approved — Teacher.StudentCapacity was raised to the requested value.</summary>
    Approved = 2,

    /// <summary>Rejected by the super admin (RejectionReason carries the why).</summary>
    Rejected = 3,

    /// <summary>Withdrawn by the teacher before resolution.</summary>
    Cancelled = 4
}
