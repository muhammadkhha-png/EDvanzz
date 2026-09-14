using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Entities;

/// <summary>
/// A single targeting rule on a <see cref="PaymentEvent"/> ("Books &amp; fees" item) — the answer to
/// "who should buy this?". Mirrors <see cref="OnlineExamScope"/> plus an <c>AllStudents</c> branch.
///
/// WHY THIS EXISTS: <see cref="PaymentEvent.TargetScopeIds"/> stores a comma-joined list of
/// ALREADY-RESOLVED student ids, so it structurally cannot answer "by session" / "by group", and a
/// multi-scope create used to collapse <see cref="PaymentEvent.TargetScopeType"/> to
/// <c>IndividualStudents</c>. These rows keep the ORIGINAL intent, which is what the tracking
/// breakdowns and the per-item auto-include rule both read.
///
/// NO INDIVIDUAL-STUDENT BRANCH, DELIBERATELY. Individually targeted students are already fully
/// recorded by their <see cref="EventStudentObligation"/> rows, and a <c>TeacherStudentId</c> FK here
/// would need purge handling the CHECK constraint forbids (it cannot be nulled — see the identical
/// reasoning on <see cref="VideoScope"/>, whose individual branch was removed). So this table carries
/// Session | SessionGroup | AllStudents only.
///
/// FOREIGN-KEY MODELING: two nullable target FKs replace the polymorphic-FK pattern. ONE CHECK
/// constraint in <c>EdvanzDbContext.OnModelCreating</c> enforces both shape rules at once —
/// <see cref="ScopeType"/> matches the populated FK, AND <c>AllStudents</c> carries no target. It is
/// deliberately not split into the two constraints <see cref="OnlineExamScope"/> uses: an
/// "exactly one target is non-null" check would reject every <c>AllStudents</c> row.
///
/// COMPOSITE TENANT SCOPE: <see cref="TeacherId"/> is denormalized from the parent
/// <see cref="PaymentEvent"/> and participates in a composite FK <c>(PaymentEventId, TeacherId)</c>
/// declared in the fluent API. A row whose <c>TeacherId</c> doesn't match its parent's cannot be
/// inserted.
///
/// HARD DELETE ON SESSION/GROUP DELETE: both target FKs are <c>NoAction</c> and the CHECK constraint
/// forbids nulling them, so <c>SessionService.DeleteSessionAsync</c> / the group-delete path must
/// DELETE these rows before the hard delete — exactly as they already do for
/// <c>VideoScope</c> / <c>VideoUnitScope</c> / <c>OnlineExamScope</c>. A surviving row would fail the
/// delete with a 409.
/// </summary>
public class PaymentEventScope : BaseEntity
{
    public long PaymentEventId { get; set; }
    public PaymentEvent PaymentEvent { get; set; } = null!;

    /// <summary>
    /// Denormalized from <see cref="PaymentEvent.TeacherId"/>. Participates in the composite FK to
    /// <c>PaymentEvents(Id, TeacherId)</c> — do NOT add a second explicit
    /// <c>HasOne(s =&gt; s.Teacher)</c> fluent declaration (EF Core 10 merges it into the composite
    /// one and drops the OnDelete clause — same gotcha as <c>VideoScope</c>).
    /// </summary>
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Session | SessionGroup | AllStudents. <c>IndividualStudents</c> is never persisted here —
    /// see the class remarks.
    /// </summary>
    public EventTargetScopeType ScopeType { get; set; }

    /// <summary>Non-null only when <see cref="ScopeType"/> = Session.</summary>
    public long? SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>Non-null only when <see cref="ScopeType"/> = SessionGroup.</summary>
    public long? SessionGroupId { get; set; }
    public SessionGroup? SessionGroup { get; set; }

    /// <summary>The user (Teacher or Assistant) who added this scope row.</summary>
    public long AssignedByUserId { get; set; }
    public User AssignedByUser { get; set; } = null!;

    /// <summary>Server-side UTC timestamp of when this scope row was added.</summary>
    public DateTime AssignedAt { get; set; }
}
