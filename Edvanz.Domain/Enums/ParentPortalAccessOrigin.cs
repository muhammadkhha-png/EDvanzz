using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// WHY a <see cref="Entities.ParentPortalAccess"/> grant became Active — the teacher must be able
/// to tell "I tapped approve" apart from "the app let them in on its own", and for the latter,
/// which rule did it.
///
/// Null on a grant that is not (yet) Active, and on LEGACY rows created before this column shipped
/// (2026-09-02). A client seeing null on an Active row should fall back to
/// <see cref="Entities.ParentPortalAccess.AutoApproved"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ParentPortalAccessOrigin : byte
{
    /// <summary>
    /// The typed phone matched the student's <c>ParentPhoneNumber</c> on the teacher's roster —
    /// the teacher had already written that number down, so the app honoured it. This is the ONLY
    /// origin that also sets <c>AutoApproved = true</c>.
    /// </summary>
    RosterPhone = 1,

    /// <summary>A teacher or an assistant explicitly approved the request from the inbox.</summary>
    TeacherApproved = 2,

    /// <summary>
    /// The typed phone already had an ACTIVE grant on this same student from another device, so a
    /// teacher had vetted that number before and the parent got straight back in.
    ///
    /// This is what makes access follow the PHONE rather than the browser: clearing cookies,
    /// switching browser or buying a new phone no longer sends an already-approved parent back to
    /// the inbox. It is also why revocation must clear EVERY grant sharing that (student, phone) —
    /// see <c>IParentPortalAccessRepo.RevokeByStudentAndPhoneAsync</c>.
    /// </summary>
    TrustedPhone = 3
}
