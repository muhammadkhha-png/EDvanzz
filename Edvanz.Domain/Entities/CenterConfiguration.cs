using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Center-wide DEFAULT settings — the template a center manages once and can push onto ALL of its
/// teachers ("apply to all"). Mirrors <see cref="TeacherConfiguration"/> field-for-field so a
/// center-owned teacher can be brought to exact parity with the center's chosen defaults.
///
/// One-to-one with <see cref="Center"/>. FK behavior is configured ENTIRELY in Fluent API
/// (EdvanzDbContext) — NO [ForeignKey] annotations here (BUG-4 rule: an annotation coexisting with a
/// Fluent OnDelete silently drops the delete behavior in EF Core 10). This intentionally differs from
/// <see cref="TeacherConfiguration"/> (which predates that rule); follow the <see cref="Center"/>
/// Fluent-only precedent for anything new.
///
/// This row is a DEFAULT template only — it is NOT attached to any students, so changing it never
/// re-prices anyone. Propagation to teachers happens explicitly via "apply to all", which runs the
/// SAME per-teacher save (including proration reconcile) the teacher settings screen runs.
/// </summary>
public class CenterConfiguration : BaseEntity
{
    /// <summary>Foreign key to the owning <see cref="Center"/> (1:1).</summary>
    public long CenterId { get; set; }
    public Center Center { get; set; } = null!;

    // ─── Student Code Generation ───
    /// <summary>Center default student-code mode. Kept in sync with the authoritative
    /// <see cref="Center.StudentCodeGenerationMode"/> (that column stays the source of truth for the
    /// code generator; this copy exists so the config block is self-contained and propagatable).</summary>
    public GenerationMode StudentCodeGenerationMode { get; set; } = GenerationMode.Auto;
    public GenerationLanguage StudentCodeLanguage { get; set; } = GenerationLanguage.English;

    // ─── Session Name Configuration ───
    public GenerationMode SessionNameMode { get; set; } = GenerationMode.Auto;
    public GenerationLanguage SessionNameLanguage { get; set; } = GenerationLanguage.English;

    // ─── Prorated Payment Configuration ───
    public bool IsProratedPaymentEnabled { get; set; } = false;

    // ─── Alert Thresholds ───
    public int ConsecutiveAbsenceThreshold { get; set; } = 3;
    public int ConsecutiveUnpaidThreshold { get; set; } = 3;

    // ─── Barcode Configuration ───
    public BarcodeDisplayMode BarcodeDisplayMode { get; set; } = BarcodeDisplayMode.InApp;

    // ─── Student Account Visibility ───
    public bool StudentVisibilityAttendance { get; set; } = true;
    public bool StudentVisibilityPayment { get; set; } = true;
    public bool StudentVisibilityHomework { get; set; } = true;
    public bool StudentVisibilityExamDefault { get; set; } = true;
    public bool StudentVisibilityOnlineExamDefault { get; set; } = true;
    public bool StudentVisibilityVideo { get; set; } = true;

    // ─── Parent Account Visibility ───
    public bool ParentVisibilityAttendance { get; set; } = true;
    public bool ParentVisibilityPayment { get; set; } = true;
    public bool ParentVisibilityHomework { get; set; } = true;
    public bool ParentVisibilityExamDefault { get; set; } = false;
    public bool ParentVisibilityOnlineExamDefault { get; set; } = false;
    public bool ParentVisibilityVideo { get; set; } = true;

    // ─── Device Lock ───
    public bool IsDeviceLockEnabled { get; set; } = false;

    // ─── Attendance Screen Enrichment (teacher-facing) ───
    public bool? ShowPaymentInfoOnAttendanceScreen { get; set; } = true;
    public bool? ShowAttendanceHistoryOnAttendanceScreen { get; set; } = true;

    /// <summary>Timestamp of the last configuration update. Null if never modified after creation.</summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<CenterProratedTier> ProratedTiers { get; set; } = new List<CenterProratedTier>();
}
