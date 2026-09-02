using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Lifecycle status of a <see cref="Entities.ParentPortalAccess"/> grant — the permission a
/// parent's browser (device) has to follow ONE roster student under ONE teacher through the
/// public parent portal (parent.edvanz.io).
///
/// The numeric values DELIBERATELY MIRROR <see cref="LinkStatus"/> so the two request/approval
/// lifecycles read the same way across the codebase (Active = 1, Pending = 3, Rejected = 4).
/// Value 5 is <see cref="Revoked"/> here, matching <c>LinkStatus.RemovedByTeacher</c>'s "the
/// access was ended by the other side" meaning.
///
/// Terminal rows (Rejected, Revoked) are KEPT for audit; the DB enforces at most ONE live
/// (Pending or Active) row per (TeacherStudentId, DeviceHash) via the filtered unique index
/// <c>UX_PPA_Student_Device_Live</c> — keep its <c>[Status] IN (1,3)</c> literals in sync with
/// these numeric values.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ParentPortalAccessStatus : byte
{
    /// <summary>Grant is live — this device may read the student's shared data.</summary>
    Active = 1,

    /// <summary>Request submitted, awaiting the teacher's decision.</summary>
    Pending = 3,

    /// <summary>The teacher rejected the request.</summary>
    Rejected = 4,

    /// <summary>
    /// Access ended after it was granted — either the teacher revoked it, or the parent
    /// removed it themselves from the portal. Distinguish by <see cref="Entities.ParentPortalAccess.RespondedByUserId"/>:
    /// non-null = a teacher/assistant ended it, null = the parent did.
    /// </summary>
    Revoked = 5
}
