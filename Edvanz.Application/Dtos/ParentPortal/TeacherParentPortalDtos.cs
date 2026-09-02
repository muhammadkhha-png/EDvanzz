using System.ComponentModel.DataAnnotations;
using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.ParentPortal;

// ══════════════════════════════════════════════════════════════════════════
// TEACHER SIDE of the parent portal (api/teacher/parent-portal)
//
// PRIVACY: phone numbers are ALWAYS masked here (010•••••678). The teacher needs to recognize a
// parent they know, not to harvest a contact list — and an assistant with Student/Edit reaches
// these endpoints too.
// ══════════════════════════════════════════════════════════════════════════

/// <summary>One row in the teacher's pending parent-request inbox.</summary>
public class ParentPortalRequestListItemDto
{
    /// <summary>Grant id — the value passed to approve / reject / bulk.</summary>
    public long Id { get; set; }

    /// <summary>The roster record the parent asked to follow.</summary>
    public long TeacherStudentId { get; set; }

    /// <summary>Student's name, or null when the roster record has since been deleted.</summary>
    public string? StudentName { get; set; }

    /// <summary>Student's roster code, or null when the roster record has since been deleted.</summary>
    public string? StudentCode { get; set; }

    /// <summary>The number the parent typed, MASKED (010•••••678). Null when they skipped it.</summary>
    public string? ClaimedPhoneMasked { get; set; }

    /// <summary>
    /// True when the typed number matches the student's parent phone on file. On a PENDING row it
    /// is always false (a match would have auto-approved) — it is the teacher's cue that this
    /// parent could not be verified automatically.
    /// </summary>
    public bool PhoneMatchesRoster { get; set; }

    public DateTime RequestedAt { get; set; }

    /// <summary>Serialized as a string ("Pending"). Always Pending in the inbox; present for symmetry with the followers list.</summary>
    public ParentPortalAccessStatus Status { get; set; }
}

/// <summary>One row in the "who follows this student?" panel.</summary>
public class ParentPortalFollowerListItemDto
{
    public long Id { get; set; }
    public long TeacherStudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }

    /// <summary>Masked (010•••••678) or null.</summary>
    public string? ClaimedPhoneMasked { get; set; }

    /// <summary>Serialized as a string: "Active" or "Pending".</summary>
    public ParentPortalAccessStatus Status { get; set; }

    /// <summary>True when the grant skipped the inbox because the phone matched the roster.</summary>
    public bool AutoApproved { get; set; }

    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    /// <summary>Last time this follower opened the portal. Null if never since being approved.</summary>
    public DateTime? LastSeenAt { get; set; }
}

/// <summary>Body of <c>POST api/teacher/parent-portal/requests/bulk</c>.</summary>
public class ParentPortalBulkActionDto
{
    /// <summary>Grant ids to act on. Ids outside the caller's tenant are silently ignored, never an error.</summary>
    [Required]
    public List<long> Ids { get; set; } = new();

    /// <summary>"approve" or "reject" (case-insensitive). Anything else is a 400.</summary>
    [Required]
    public string Action { get; set; } = string.Empty;
}

/// <summary>
/// Outcome of a bulk approve/reject. <see cref="ProcessedIds"/> and <see cref="SkippedIds"/>
/// together account for every id the client sent that was not a duplicate or non-positive.
/// </summary>
public class ParentPortalBulkResultDto
{
    /// <summary>Rows actually transitioned — always equals <c>ProcessedIds.Count</c>.</summary>
    public int Affected { get; set; }

    /// <summary>
    /// The ids that actually transitioned. The client MUST clear its list from THIS, not from
    /// <see cref="Affected"/>: acting on the count alone assumes everything sent succeeded and
    /// would show the teacher silently-skipped requests as approved.
    /// </summary>
    public List<long> ProcessedIds { get; set; } = new();

    /// <summary>Ids that were skipped because they were not pending, not this teacher's, or their student has been removed.</summary>
    public List<long> SkippedIds { get; set; } = new();
}

/// <summary>Counters for the teacher's parent-portal settings screen.</summary>
public class ParentPortalSummaryDto
{
    /// <summary>Requests waiting for a decision.</summary>
    public int PendingCount { get; set; }

    /// <summary>Distinct students with at least one ACTIVE follower.</summary>
    public int FollowedStudentsCount { get; set; }

    /// <summary>
    /// Active roster records with no parent phone on file. Those students can never be
    /// auto-approved, so every request for them lands in the inbox — the number is the teacher's
    /// nudge to complete their roster.
    /// </summary>
    public int StudentsMissingParentPhone { get; set; }

    /// <summary>Whether the teacher currently accepts portal followers (<c>TeacherConfiguration.ParentPortalEnabled</c>).</summary>
    public bool PortalEnabled { get; set; }
}
