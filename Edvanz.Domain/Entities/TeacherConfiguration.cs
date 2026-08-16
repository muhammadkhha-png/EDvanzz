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
    /// Default visibility for newly created exams in parent accounts.
    /// AAM-BR-10: Per-exam visibility defaults to hidden unless explicitly enabled.
    /// Per-exam overrides are stored in a separate ExamVisibility table (future module).
    /// Default: false (hidden per AAM-BR-10).
    /// </summary>
    public bool ParentVisibilityExamDefault { get; set; } = false;

    /// <summary>
    /// Default visibility for ONLINE exams in parent accounts.
    /// Mirrors the offline/online split already established on the student side
    /// (<see cref="StudentVisibilityExamDefault"/> / <see cref="StudentVisibilityOnlineExamDefault"/>).
    /// Same conservative default as <see cref="ParentVisibilityExamDefault"/> — parents only see
    /// online exam results once the teacher explicitly opts in.
    /// Default: false (hidden).
    /// </summary>
    public bool ParentVisibilityOnlineExamDefault { get; set; } = false;

    /// <summary>
    /// Whether parents can see the Videos module.
    /// Default: true (visible) — parity with Attendance/Payment/Homework.
    /// </summary>
    public bool ParentVisibilityVideo { get; set; } = true;

    // ─── Device Lock ───

    /// <summary>
    /// When enabled, each of this teacher's linked students is bound to the first
    /// device they use to open the teacher (stored on <see cref="StudentTeacherLink.LockedDeviceId"/>)
    /// and can only open the teacher from that device thereafter. The teacher (or an
    /// assistant) resets a student's device to allow re-registration on a new phone.
    /// Binding is per (student, teacher), so a student may use a different device per teacher.
    /// Default: false (no device restriction).
    /// </summary>
    public bool IsDeviceLockEnabled { get; set; } = false;

    // â”€â”€â”€ Attendance Screen Enrichment (teacher-facing, distinct from Student/ParentVisibility*) â”€â”€â”€

    /// <summary>
    /// Whether the Take/Edit Attendance student list (GET .../sessions/{sessionId}/students)
    /// includes each student's payment/debt snapshot (unpaid-last-month flag, unpaid months
    /// count, outstanding amount, unpaid month labels). Judged through the current cutoff month
    /// per CLAUDE.md Â§7.4. When false, the payment lookup is skipped entirely (no extra query).
    /// Default: true.
    /// </summary>
    public bool? ShowPaymentInfoOnAttendanceScreen { get; set; } = true;

    /// <summary>
    /// Whether the Take/Edit Attendance student list includes each student's course-scoped
    /// absence count (current active StudentSessionAssignment only â€” distinct from the lifetime
    /// StudentAbsenceCounter.TotalAbsences) and current-calendar-month absence count. Does NOT
    /// gate WasAbsentLastSession/LastAbsenceDate/LastAbsenceSessionName, which stay unconditional
    /// (REQ-ATT-028/029/060 absence-alert warning, unrelated to this display preference).
    /// Default: true.
    /// </summary>
    public bool? ShowAttendanceHistoryOnAttendanceScreen { get; set; } = true;

    /// <summary>
    /// Timestamp of the last configuration update. Null if never modified after initial creation.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<TeacherProratedTier> ProratedTiers { get; set; } = new List<TeacherProratedTier>();
}