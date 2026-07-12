using System.ComponentModel.DataAnnotations;

namespace Edvanz.Application.Dtos.TeacherLinks;

// ════════════════════════════════════════════════════════════════════════════
// TEACHER-SIDE DTOs for the student-teacher link request/approval flow.
// Consumed by TeacherStudentLinksController (route api/teacher/student-links).
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The teacher's shareable linking code (GET my-code).
/// Students type this code when sending a link request.
/// </summary>
public class TeacherCodeDto
{
    /// <summary>The teacher's unique, immutable 8-digit code (AAM-FR-03.3).</summary>
    public string TeacherCode { get; set; } = null!;
}

/// <summary>
/// One pending link request on the teacher's inbox.
/// Combines what the student typed with their account identity, plus a
/// suggested roster match when the typed student code resolves to one.
/// </summary>
public class TeacherLinkRequestListItemDto
{
    /// <summary>Link row id — pass to the accept/reject endpoints.</summary>
    public long LinkId { get; set; }

    /// <summary>Name the student typed when sending the request.</summary>
    public string? RequestedStudentName { get; set; }

    /// <summary>Teacher-assigned student code the student claims to have (optional).</summary>
    public string? RequestedStudentCode { get; set; }

    /// <summary>UTC timestamp the request was submitted.</summary>
    public DateTime? RequestedAt { get; set; }

    /// <summary>The requesting account's platform-wide student account code.</summary>
    public string StudentAccountCode { get; set; } = null!;

    /// <summary>The requesting account's registered full name.</summary>
    public string StudentFullName { get; set; } = null!;

    /// <summary>The requesting account's registered phone number, if any.</summary>
    public string? StudentPhoneNumber { get; set; }

    /// <summary>
    /// Roster record matched from <see cref="RequestedStudentCode"/>, or null when
    /// no code was supplied / no match exists. The teacher can accept with this
    /// suggestion or select a different roster record explicitly.
    /// </summary>
    public RosterStudentSuggestionDto? SuggestedMatch { get; set; }
}

/// <summary>
/// A TeacherStudent roster record suggested as the accept-time binding target.
/// </summary>
public class RosterStudentSuggestionDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;

    /// <summary>
    /// True when another student account already holds an Active link to this
    /// roster record — accepting with it will be rejected (one account per record).
    /// </summary>
    public bool IsAlreadyLinked { get; set; }
}

/// <summary>
/// Accept-request body. <see cref="TeacherStudentId"/> is optional: when omitted,
/// the server auto-matches by the request's RequestedStudentCode; if neither
/// resolves to a roster record the accept fails with 422 — every Active link must
/// be bound to a TeacherStudent record (all module data hangs off that FK).
/// </summary>
public class AcceptLinkRequestDto
{
    /// <summary>Explicit roster record to bind the link to (overrides auto-match).</summary>
    public long? TeacherStudentId { get; set; }
}

/// <summary>
/// One Active link on the teacher's linked-students screen.
/// </summary>
public class LinkedStudentListItemDto
{
    /// <summary>Link row id — pass to the bulk remove endpoint.</summary>
    public long LinkId { get; set; }

    /// <summary>UTC timestamp the link became Active.</summary>
    public DateTime LinkedAt { get; set; }

    public string StudentAccountCode { get; set; } = null!;
    public string StudentFullName { get; set; } = null!;
    public string? StudentPhoneNumber { get; set; }

    /// <summary>Bound roster record id — null if the teacher deleted the record.</summary>
    public long? TeacherStudentId { get; set; }
    public string? RosterStudentName { get; set; }
    public string? RosterStudentCode { get; set; }
}

/// <summary>
/// Bulk remove body — one or many link ids (the user story allows removing
/// a single linked student or several at once).
/// </summary>
public class RemoveLinkedStudentsDto
{
    [Required]
    [MinLength(1)]
    public List<long> LinkIds { get; set; } = new();
}

/// <summary>
/// Bulk remove outcome: how many links were removed and which requested ids
/// were skipped (not found, not owned by this teacher, or not Active).
/// </summary>
public class RemoveLinkedStudentsResultDto
{
    public int RemovedCount { get; set; }
    public List<long> SkippedLinkIds { get; set; } = new();
}