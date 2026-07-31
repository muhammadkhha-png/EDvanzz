using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

// ════════════════════════════════════════════════════════════════════════════
// STUDENT MODULE (MODULE 1) — REPOSITORY PROJECTION TYPES
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Aggregate assignment counts for the "Assign Students" chip strip, scoped to a
/// student search term. All/Unassigned are scalars; per-session counts cover only
/// sessions that have at least one matching student (the service left-joins these
/// onto the full session catalog so zero-match sessions still render as chips).
/// REQ-SES-021 / REQ-SES-NFR-009: live assigned-student counts.
/// </summary>
public sealed class StudentAssignmentCounts
{
    public int CountAll { get; set; }
    public int CountUnassigned { get; set; }
    public IReadOnlyList<SessionAssignedCountRow> PerSession { get; set; } = new List<SessionAssignedCountRow>();
}

/// <summary>One session's matching assigned-student count.</summary>
public sealed class SessionAssignedCountRow
{
    public long SessionId { get; set; }
    public int AssignedCount { get; set; }
}

/// <summary>Lightweight session identity (Id + name) for chip labels and list badges.</summary>
public sealed class SessionNameRow
{
    public long Id { get; set; }
    public string SessionName { get; set; } = null!;
}

/// <summary>Session identity plus its owning group id — for expanding a group into its member sessions.</summary>
public sealed class GroupSessionRow
{
    public long GroupId { get; set; }
    public long Id { get; set; }
    public string SessionName { get; set; } = null!;
}
/// <summary>
/// Compact session summary (Id + name + occurrence + payment + amount) projected
/// without loading the full Session entity. Feeds the assigned-session card on the
/// student profile screen. REQ-SES-002 / REQ-SES-007 / REQ-SES-010 / REQ-SES-011.
/// </summary>
public sealed class SessionSummaryRow
{
    public long Id { get; set; }
    public string SessionName { get; set; } = null!;
    public OccurrenceType OccurrenceType { get; set; }
    public PaymentType PaymentType { get; set; }
    public decimal SessionAmount { get; set; }
}
/// <summary>
/// A resolved scope target (session or session-group) with its display name and
/// current active-student count. Backs <c>GET /api/video-units/{unitId}</c> so the
/// scope picker shows who a target actually reaches, not just its raw id.
/// StudentCount reflects <c>TeacherStudent</c> rows only (global !IsDeleted filter
/// applies automatically) — a student assigned to the target session, or to any
/// session inside the target group.
/// </summary>
public sealed class ScopeTargetSummaryRow
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public int StudentCount { get; set; }
}