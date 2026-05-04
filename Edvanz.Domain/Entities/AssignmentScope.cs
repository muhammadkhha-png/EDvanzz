using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents a single targeting rule on an <see cref="AssignmentTemplate"/>. A scope row
/// resolves to one or more students at occurrence-generation time:
/// <list type="bullet">
///   <item>An IndividualStudent scope resolves to exactly one student.</item>
///   <item>A Session scope resolves to all students currently assigned to that session.</item>
///   <item>A SessionGroup scope resolves to all students assigned to any session in the group.</item>
/// </list>
///
/// REQ-EXH-002: Three target scope kinds — individual student, session, or session group.
/// REQ-EXH-003: A template may combine multiple scopes; deduplication happens at write time
/// in the service layer, with a unique composite index on
/// <c>StudentAssignmentObligations(OccurrenceId, TeacherStudentId)</c> as the safety net.
///
/// FOREIGN KEY MODELING:
/// Three nullable FKs (TeacherStudentId, SessionId, SessionGroupId) replace the
/// polymorphic-FK pattern from the original ERD. A CHECK constraint configured in
/// <c>EdvanzDbContext.OnModelCreating</c> enforces that exactly one is non-null and
/// matches <see cref="ScopeType"/>. This gives full referential integrity, indexable
/// joins, and clean EF Core fluent-API mapping.
/// </summary>
public class AssignmentScope : BaseEntity
{
    // ══════════════════════════════════════════════
    // TEMPLATE LINKAGE & TENANT SCOPE
    // ══════════════════════════════════════════════

    /// <summary>
    /// The template this scope row belongs to. Cascade-deleted with the template.
    /// </summary>
    [ForeignKey(nameof(Template))]
    public long TemplateId { get; set; }
    public AssignmentTemplate Template { get; set; } = null!;

    /// <summary>
    /// Foreign key to the owning Teacher. Denormalized from
    /// <see cref="AssignmentTemplate.TeacherId"/> for tenant-scoped composite indexes
    /// without cross-table joins. Same pattern as <c>SessionOccurrence.TeacherId</c>.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    // ══════════════════════════════════════════════
    // SCOPE DISCRIMINATOR & TARGETS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Identifies which of the three nullable FKs below is populated on this row.
    /// Enforced by a CHECK constraint in fluent API configuration.
    /// </summary>
    public AssignmentScopeType ScopeType { get; set; }

    /// <summary>
    /// Foreign key to a specific student. Non-null only when
    /// <see cref="ScopeType"/> = <see cref="AssignmentScopeType.IndividualStudent"/>.
    /// REQ-EXH-002: Targeting a specific individual student.
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long? TeacherStudentId { get; set; }
    public TeacherStudent? TeacherStudent { get; set; }

    /// <summary>
    /// Foreign key to a specific session. Non-null only when
    /// <see cref="ScopeType"/> = <see cref="AssignmentScopeType.Session"/>.
    /// REQ-EXH-002: Targeting all students currently in a session.
    /// </summary>
    [ForeignKey(nameof(Session))]
    public long? SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>
    /// Foreign key to a specific session group. Non-null only when
    /// <see cref="ScopeType"/> = <see cref="AssignmentScopeType.SessionGroup"/>.
    /// REQ-EXH-002: Targeting all students across the sessions in a group.
    /// </summary>
    [ForeignKey(nameof(SessionGroup))]
    public long? SessionGroupId { get; set; }
    public SessionGroup? SessionGroup { get; set; }
}
