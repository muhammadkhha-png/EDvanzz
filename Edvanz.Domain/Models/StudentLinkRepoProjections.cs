namespace Edvanz.Domain.Interfaces;

// ════════════════════════════════════════════════════════════════════════════
// STUDENT-TEACHER LINK (REQUEST/APPROVAL FLOW) — REPOSITORY PROJECTION TYPES
// ════════════════════════════════════════════════════════════════════════════
//
// Query projections returned by the IUserRepo link-management methods for the
// teacher-side screens (pending requests inbox, linked-students list). Same
// convention as VideoRepoProjections: these are NOT DTOs — repos return these,
// services map them to client-facing DTOs.
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Projection row for one pending link request on the teacher's inbox.
/// Combines the student-typed request snapshot with the requesting account's
/// identity so the teacher can recognize who is asking before accepting.
/// </summary>
public sealed class TeacherLinkRequestRow
{
    public long LinkId { get; set; }

    /// <summary>Name the student typed when sending the request.</summary>
    public string? RequestedStudentName { get; set; }

    /// <summary>Optional teacher-assigned student code the student claims to have.</summary>
    public string? RequestedStudentCode { get; set; }

    public DateTime? RequestedAt { get; set; }

    // Requesting account identity (StudentUser + User)
    public string StudentAccountCode { get; set; } = null!;
    public string StudentFullName { get; set; } = null!;
    public string? StudentPhoneNumber { get; set; }
}

/// <summary>
/// Projection row for one Active link on the teacher's linked-students list.
/// Includes both the account identity and the bound roster (TeacherStudent)
/// record so the list can show which enrollment the account maps to.
/// </summary>
public sealed class TeacherLinkedStudentRow
{
    public long LinkId { get; set; }
    public DateTime LinkedAt { get; set; }

    // Linked account identity (StudentUser + User)
    public string StudentAccountCode { get; set; } = null!;
    public string StudentFullName { get; set; } = null!;
    public string? StudentPhoneNumber { get; set; }

    // Bound roster record (null if the teacher deleted it after linking)
    public long? TeacherStudentId { get; set; }
    public string? RosterStudentName { get; set; }
    public string? RosterStudentCode { get; set; }
}