using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Entities;

/// <summary>
/// A Center account — the multi-teacher tenancy tier that sits ABOVE the Teacher.
/// A center owns several <see cref="Teacher"/> records (each keeping its own independent
/// students / payments / sessions / exams / videos), can have several
/// <see cref="CenterAssistant"/>s, holds ONE <see cref="CenterSubscription"/> priced by how
/// many Full vs Managerial teachers it carries, and takes a percentage of each teacher's
/// revenue (<see cref="DefaultRevenueSharePercent"/>, overridable per teacher via
/// <see cref="Teacher.RevenueSharePercentOverride"/>).
///
/// The center's login (role <c>Center</c>) — and its assistants — operate every teacher by
/// "acting as" one at a time (the <c>X-Acting-Teacher-Id</c> header, validated against center
/// membership). Center-owned teachers have NO usable login of their own (their identity
/// <see cref="Teacher.UserId"/> row is created with <c>User.IsActive = false</c>).
///
/// FK behavior is configured ENTIRELY in Fluent API (EdvanzDbContext) — no [ForeignKey]
/// annotations here, per the BUG-4 rule.
/// </summary>
public class Center : BaseEntity
{
    /// <summary>Foreign key to the login <see cref="User"/> (UserType = Center). 1:1.</summary>
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Display name of the center (e.g. "Al-Nour Learning Center").</summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Unique, immutable, 8-digit shareable code auto-generated on creation
    /// (mirrors <see cref="Teacher.TeacherCode"/>).
    /// </summary>
    public string CenterCode { get; set; } = null!;

    /// <summary>
    /// Default percentage (0–100) of each teacher's revenue the center takes. Applied to every
    /// teacher unless that teacher carries a <see cref="Teacher.RevenueSharePercentOverride"/>.
    /// Stored as decimal(5,2) via Fluent.
    /// </summary>
    public decimal DefaultRevenueSharePercent { get; set; }

    /// <summary>Center's preferred UI language ("en" / "ar").</summary>
    public string? LanguagePreference { get; set; }

    /// <summary>
    /// Center-wide DEFAULT student-code mode for its teachers. When <see cref="GenerationMode.Auto"/>,
    /// generated codes are unique across ALL the center's teachers (so codes never collide within the
    /// center); a teacher may override this for itself via <see cref="Teacher.StudentCodeModeOverride"/>.
    /// Manual codes are per-teacher only and may legitimately repeat across teachers (disambiguated by
    /// the front-desk picker).
    /// </summary>
    public GenerationMode StudentCodeGenerationMode { get; set; } = GenerationMode.Auto;

    /// <summary>Current account status: Active, Inactive, or Suspended.</summary>
    public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;

    /// <summary>Timestamp of account deactivation. Null while active.</summary>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>Soft-delete timestamp. Null if not deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>The user (typically the super admin) who created this center account.</summary>
    public long? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    // ── Navigation ──────────────────────────────────────────────
    public ICollection<Teacher> Teachers { get; set; } = new List<Teacher>();
    public ICollection<CenterAssistant> CenterAssistants { get; set; } = new List<CenterAssistant>();
    public ICollection<CenterSubscription> Subscriptions { get; set; } = new List<CenterSubscription>();
}
