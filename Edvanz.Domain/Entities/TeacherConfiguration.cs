using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Stores all configurable settings for a Teacher's account.
/// AAM-FR-04: Created with system defaults when the teacher account is created.
/// AAM-NFR-05: Configuration flow is skippable; defaults apply automatically.
/// AAM-BR-04: Defaults are documented in the system configuration reference.
/// One-to-one relationship with Teacher.
/// </summary>
public class TeacherConfiguration : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    // ─── AAM-FR-04.2: Student Code Generation ───

    /// <summary>
    /// Whether student codes are auto-generated or manually entered by the teacher.
    /// Default: Auto.
    /// </summary>
    public GenerationMode StudentCodeGenerationMode { get; set; } = GenerationMode.Auto;

    /// <summary>
    /// Language for auto-generated student codes (Arabic or English).
    /// Only applicable when StudentCodeGenerationMode is Auto.
    /// Default: English.
    /// </summary>
    public GenerationLanguage StudentCodeLanguage { get; set; } = GenerationLanguage.English;

    // ─── AAM-FR-04.3: Session Name Configuration ───

    /// <summary>
    /// Whether session names are auto-generated or manually entered by the teacher.
    /// Default: Auto.
    /// </summary>
    public GenerationMode SessionNameMode { get; set; } = GenerationMode.Auto;

    /// <summary>
    /// Language for auto-generated session names (Arabic or English).
    /// Only applicable when SessionNameMode is Auto.
    /// Default: English.
    /// </summary>
    public GenerationLanguage SessionNameLanguage { get; set; } = GenerationLanguage.English;

    // ─── AAM-FR-04.4: Prorated Payment Configuration ───

    /// <summary>
    /// Master toggle for the prorated payment feature.
    /// When disabled, all students are charged the full amount regardless of join date.
    /// Default: false (disabled).
    /// </summary>
    public bool IsProratedPaymentEnabled { get; set; } = false;

    // ─── AAM-FR-04.5: Consecutive Absence Alert ───

    /// <summary>
    /// Number of consecutive absences before an alert is triggered.
    /// Default: 3 sessions.
    /// </summary>
    public int ConsecutiveAbsenceThreshold { get; set; } = 3;

    // ─── AAM-FR-04.6: Consecutive Unpaid Sessions Alert ───

    /// <summary>
    /// Number of consecutive unpaid sessions before a payment alert is triggered.
    /// Default: 3 sessions.
    /// </summary>
    public int ConsecutiveUnpaidThreshold { get; set; } = 3;

    // ─── AAM-FR-04.7: Barcode Configuration ───

    /// <summary>
    /// Whether student barcodes are displayed in-app or restricted to hard-copy only.
    /// Default: InApp.
    /// </summary>
    public BarcodeDisplayMode BarcodeDisplayMode { get; set; } = BarcodeDisplayMode.InApp;

    // ─── AAM-FR-04.8: Student Account Visibility ───

    /// <summary>
    /// Whether students can see the Attendance Track module.
    /// Default: true (visible).
    /// </summary>
    public bool StudentVisibilityAttendance { get; set; } = true;

    /// <summary>
    /// Whether students can see the Payment Track module.
    /// Default: true (visible).
    /// </summary>
    public bool StudentVisibilityPayment { get; set; } = true;

    /// <summary>
    /// Whether students can see the Homework Track module.
    /// Default: true (visible).
    /// </summary>
    public bool StudentVisibilityHomework { get; set; } = true;

    /// <summary>
    /// Default visibility of offline exams in student accounts.
    /// Product decision (2026-07-18): offline exams are VISIBLE by default on the student side
    /// (overrides the original AAM-BR-10 "hidden by default"); a teacher can still hide them per
    /// account by turning this off. Per-exam overrides remain a future ExamVisibility module.
    /// Default: true (visible).
    /// </summary>
    public bool StudentVisibilityExamDefault { get; set; } = true;
    public bool StudentVisibilityOnlineExamDefault { get; set; } = true;

    /// <summary>
    /// Whether students can see the Videos module.
    /// Default: true (visible).
    /// </summary>
    public bool StudentVisibilityVideo { get; set; } = true;


    // ─── AAM-FR-04.9: Parent Account Visibility ───

    /// <summary>
    /// Whether parents can see the Attendance Track module.
    /// Default: true (visible).
    /// </summary>
    public bool ParentVisibilityAttendance { get; set; } = true;

    /// <summary>
    /// Whether parents can see the Payment Track module.
    /// Default: true (visible).
    /// </summary>
    public bool ParentVisibilityPayment { get; set; } = true;

    /// <summary>
    /// Whether parents can see the Homework Track module.
    /// Default: true (visible).
    /// </summary>
    public bool ParentVisibilityHomework { get; set; } = true;

    /// <summary>
    /// Default visibility for newly created exams (offline / homework-track) in parent accounts.
    /// Product decision (Phase 2, parent parity): flipped to VISIBLE by default to match the
    /// student-side flip on StudentVisibilityExamDefault (2026-07-18) — a teacher can still hide
    /// it per account by turning this off. Per-exam overrides remain a future ExamVisibility
    /// module. Supersedes the original AAM-BR-10 "hidden by default".
    /// NOTE: this C# default only governs newly-created configuration rows (set explicitly in
    /// TeacherService.InitializeTeacherAsync's seed block) — it has no effect on rows that
    /// already exist in the database.
    /// Default: true (visible).
    /// </summary>
    public bool ParentVisibilityExamDefault { get; set; } = true;

    /// <summary>
    /// Whether parents can see the Videos module.
    /// Added Phase 2 (parent parity) to mirror StudentVisibilityVideo.
    /// Default: true (visible).
    /// </summary>
    public bool ParentVisibilityVideo { get; set; } = true;

    /// <summary>
    /// Default visibility of online exams in parent accounts.
    /// Added Phase 2 (parent parity) to mirror StudentVisibilityOnlineExamDefault.
    /// Default: true (visible).
    /// </summary>
    public bool ParentVisibilityOnlineExamDefault { get; set; } = true;

    /// <summary>
    /// Timestamp of the last configuration update. Null if never modified after initial creation.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<TeacherProratedTier> ProratedTiers { get; set; } = new List<TeacherProratedTier>();
}