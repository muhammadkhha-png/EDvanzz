using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Entities;

/// <summary>
/// One parent-portal GRANT: permission for a single browser/device to follow ONE
/// <see cref="TeacherStudent"/> roster record under ONE teacher, read-only, from the public
/// portal at parent.edvanz.io (a PHP page that calls this API server-to-server).
///
/// The parent identifies the student by typing the teacher's public 8-digit code plus the
/// teacher's per-roster student code (and, optionally, their phone). Because
/// <see cref="TeacherStudent.StudentCode"/> is a SEQUENTIAL counter (A1, A2 … Z999) and
/// <see cref="Teacher.TeacherCode"/> is public, the codes alone are NOT a credential — access
/// is always a grant the teacher approves. It is auto-approved (<see cref="AutoApproved"/>)
/// only when the typed phone matches the roster record's parent phone.
///
/// TENANT INTEGRITY: <see cref="TeacherId"/> is denormalized from the roster record and
/// participates in the composite FK <c>(TeacherStudentId, TeacherId)</c> →
/// <c>TeacherStudents(Id, TeacherId)</c> (CLAUDE.md §4.4, VideoScope is the reference
/// implementation), so a row whose teacher does not match its student's teacher cannot exist.
///
/// PRIVACY: the portal's raw device id is NEVER stored — only <see cref="DeviceHash"/>, its
/// SHA-256 hex. Same for the caller IP (<see cref="RequestIpHash"/>).
/// </summary>
public class ParentPortalAccess : BaseEntity
{
    /// <summary>
    /// Owning teacher — denormalized from <see cref="TeacherStudent.TeacherId"/> so every
    /// teacher-scoped query (inbox, summary) is served without joining back to TeacherStudents.
    /// Kept in sync at the DB level by the composite FK described in the class remarks.
    /// </summary>
    public long TeacherId { get; set; }

    /// <summary>The roster record this grant follows. One grant = one student, never a set.</summary>
    public long TeacherStudentId { get; set; }

    /// <summary>The followed roster record.</summary>
    public TeacherStudent TeacherStudent { get; set; } = null!;

    /// <summary>
    /// SHA-256 hex (64 chars, lowercase) of the portal-supplied device id. The RAW id is never
    /// stored: a leaked database row must not let anyone impersonate a parent's browser, and the
    /// portal proves possession by sending the raw id on every call (hashed server-side and
    /// compared against this column).
    /// </summary>
    public string DeviceHash { get; set; } = null!;

    /// <summary>Grant lifecycle — see <see cref="ParentPortalAccessStatus"/>.</summary>
    public ParentPortalAccessStatus Status { get; set; } = ParentPortalAccessStatus.Pending;

    /// <summary>
    /// The NORMALIZED Egyptian mobile number the parent typed (11 digits, leading 0), or null
    /// when they skipped it. Kept for the teacher's inbox (shown MASKED) and for the
    /// auto-approval audit trail — it is never used as an authentication credential on its own.
    /// </summary>
    public string? ClaimedPhone { get; set; }

    /// <summary>
    /// True when the grant skipped the approval queue because <see cref="ClaimedPhone"/> matched
    /// the roster record's <see cref="TeacherStudent.ParentPhoneNumber"/>. Surfaced to the
    /// teacher so an auto-approved follower is distinguishable from one they approved by hand.
    /// </summary>
    public bool AutoApproved { get; set; }

    /// <summary>UTC timestamp the parent submitted the request.</summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>UTC timestamp the teacher approved or rejected the request. Null while Pending.</summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>
    /// <c>User.Id</c> of the teacher/assistant who approved, rejected or revoked this grant.
    /// PLAIN audit column — deliberately NO foreign key, mirroring
    /// <see cref="StudentTeacherLink.RespondedByUserId"/> (§7.2b "End-of-link audit").
    /// Null on an auto-approved grant and on a parent-initiated self-revoke.
    /// </summary>
    public long? RespondedByUserId { get; set; }

    /// <summary>
    /// UTC timestamp of the last authenticated read this device made. Refreshed out-of-band by
    /// <c>TouchLastSeenAsync</c> (ExecuteUpdate, no tracking) so it never joins a read's change
    /// tracker. Drives the teacher's "last opened" column.
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    /// <summary>
    /// SHA-256 hex of the caller IP the portal forwarded (<c>X-Portal-Client-IP</c>). Audit only —
    /// never compared, never exposed. Null when the portal did not forward one.
    /// </summary>
    public string? RequestIpHash { get; set; }

    /// <summary>Raw User-Agent of the requesting browser, truncated to 256 chars. Audit only.</summary>
    public string? UserAgent { get; set; }
}
