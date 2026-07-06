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