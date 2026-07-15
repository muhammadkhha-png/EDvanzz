using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Aggregate root for the Online Exam Module. Independent of <c>VideoExam</c> /
/// offline <c>Exam</c> (Q0 decision) — every exam owns its own questions, no
/// bank/pool/random selection, no <c>ExamAttempt</c>, one submission per student.
///
/// Total grade is NEVER stored on this row — it is computed as
/// <c>Σ Questions.Degree</c> at read time (do-not-reintroduce #6). This avoids a
/// second source of truth that could drift from the questions the teacher actually
/// entered.
///
/// FOREIGN-KEY MODELING: fluent-API only, no <c>[ForeignKey]</c> annotations — see
/// <c>EdvanzDbContext.OnModelCreating</c> for the full delete-behavior matrix (§2 of
/// the implementation plan). <see cref="TeacherId"/> intentionally carries no
/// attributes for the same EF Core 10 silent-OnDelete-drop reason documented on
/// <c>VideoAsset</c>.
/// </summary>
public class OnlineExam : BaseEntity
{
    // ══════════════════════════════════════════════
    // OWNERSHIP & TENANT SCOPE
    // ══════════════════════════════════════════════

    /// <summary>Foreign key to the owning Teacher. Every read/write is tenant-scoped on this column.</summary>
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    // ══════════════════════════════════════════════
    // CORE METADATA
    // ══════════════════════════════════════════════

    [MaxLength(250)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string? Instructions { get; set; }

    /// <summary>Availability window start. Availability = StartDateTime…EndDateTime only (§0) — no separate duration field.</summary>
    public DateTime StartDateTime { get; set; }

    /// <summary>Availability window end. <c>CK_OnlineExams_DateOrder</c> enforces Start &lt; End.</summary>
    public DateTime EndDateTime { get; set; }

    /// <summary>Minimum percentage to pass. <c>CK_OnlineExams_PassPercentageRange</c> enforces 0–100.</summary>
    public decimal PassPercentage { get; set; }

    public OnlineExamStatus Status { get; set; } = OnlineExamStatus.Draft;

    /// <summary>Student-facing visibility toggle, independent of <see cref="Status"/>.</summary>
    public bool Visibility { get; set; } = true;

    // ══════════════════════════════════════════════
    // AUDIT
    // ══════════════════════════════════════════════

    /// <summary>The user (Teacher or Assistant) who created this exam.</summary>
    public long CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    /// <summary>The user who last edited this exam. Null until first edit.</summary>
    public long? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token. Guards T8 (edit), T11 (status toggle) — same
    /// pattern as <c>VideoAsset.RowVersion</c> / <c>AssignmentTemplate.RowVersion</c>.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    // ══════════════════════════════════════════════
    // NAVIGATION COLLECTIONS
    // ══════════════════════════════════════════════

    /// <summary>All questions on this exam. Editable only while <see cref="Status"/> = Draft (Q5).</summary>
    public ICollection<OnlineExamQuestion> Questions { get; set; } = new List<OnlineExamQuestion>();

    /// <summary>All scope rows targeting this exam. NoAction-deleted with the exam (leaf-first purge).</summary>
    public ICollection<OnlineExamScope> Scopes { get; set; } = new List<OnlineExamScope>();

    // NOTE: deliberately NO navigation to StudentOnlineExamReport — it is its own
    // aggregate root and must never be loaded through OnlineExam (§1, do-not-reintroduce #3).
}