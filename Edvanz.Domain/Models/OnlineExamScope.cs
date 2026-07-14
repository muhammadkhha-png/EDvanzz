using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Entities;

/// <summary>
/// A single targeting rule on an <see cref="OnlineExam"/>. Mirrors <see cref="VideoScope"/>
/// minus the individual-student branch — Session or SessionGroup only (§1).
///
/// FOREIGN-KEY MODELING: two nullable target FKs replace the polymorphic-FK pattern.
/// Two CHECK constraints in <c>EdvanzDbContext.OnModelCreating</c> enforce:
/// <list type="number">
///   <item>Exactly one of the two target FKs is non-null on every row.</item>
///   <item><see cref="ScopeType"/> matches the populated FK.</item>
/// </list>
///
/// COMPOSITE TENANT SCOPE: <see cref="TeacherId"/> is denormalized from the parent
/// <see cref="OnlineExam"/> and participates in a composite FK
/// <c>(OnlineExamId, TeacherId)</c> declared in the fluent API — identical pattern to
/// <c>VideoScope</c> → <c>VideoAsset</c>. A row whose <c>TeacherId</c> doesn't match
/// its parent's <c>TeacherId</c> cannot be inserted.
/// </summary>
public class OnlineExamScope : BaseEntity
{
    public long OnlineExamId { get; set; }
    public OnlineExam OnlineExam { get; set; } = null!;

    /// <summary>
    /// Denormalized from <see cref="OnlineExam.TeacherId"/>. Participates in the
    /// composite FK to <c>OnlineExams(Id, TeacherId)</c> — do NOT add a second
    /// explicit <c>HasOne(s =&gt; s.Teacher)</c> fluent declaration (EF Core 10 merges
    /// it into the composite one and drops the OnDelete clause — same gotcha as
    /// <c>VideoScope</c>).
    /// </summary>
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    public OnlineExamScopeType ScopeType { get; set; }

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