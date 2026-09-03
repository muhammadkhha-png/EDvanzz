using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Teacher;

/// <summary>
/// Output DTO representing the full teacher configuration state.
/// Returned by GetConfigurationAsync and SaveConfigurationAsync.
/// </summary>
public class TeacherConfigurationDto
{
    public long Id { get; set; }
    public long TeacherId { get; set; }

    // ─── AAM-FR-04.2 ───
    public GenerationMode StudentCodeGenerationMode { get; set; }
    public GenerationLanguage StudentCodeLanguage { get; set; }

    // ─── AAM-FR-04.3 ───
    public GenerationMode SessionNameMode { get; set; }
    public GenerationLanguage SessionNameLanguage { get; set; }

    // ─── AAM-FR-04.4 ───
    public bool IsProratedPaymentEnabled { get; set; }

    /// <summary>
    /// How the app suggests a new student's joining-month amount (REQ-PAY-021/022): ByPercentage
    /// (default, current behaviour) | ByClasses | Manual. Serialized as a string via
    /// <c>JsonStringEnumConverter</c>.
    /// </summary>
    public ProrationMethod ProrationMethod { get; set; } = ProrationMethod.ByPercentage;

    public List<ProratedTierDto> ProratedTiers { get; set; } = new();

    // ─── AAM-FR-04.5 & 04.6 ───
    public int ConsecutiveAbsenceThreshold { get; set; }
    public int ConsecutiveUnpaidThreshold { get; set; }

    // ─── AAM-FR-04.7 ───
    public BarcodeDisplayMode BarcodeDisplayMode { get; set; }

    // ─── AAM-FR-04.8 ───
    public bool StudentVisibilityAttendance { get; set; }
    public bool StudentVisibilityPayment { get; set; }
    public bool StudentVisibilityHomework { get; set; }
    public bool StudentVisibilityExamDefault { get; set; }
    public bool StudentVisibilityVideo { get; set; }

    // ─── AAM-FR-04.9 ───
    public bool ParentVisibilityAttendance { get; set; }
    public bool ParentVisibilityPayment { get; set; }
    public bool ParentVisibilityHomework { get; set; }
    public bool ParentVisibilityExamDefault { get; set; }

    /// <summary>
    /// Default visibility of ONLINE exams in parent accounts — the online twin of
    /// <see cref="ParentVisibilityExamDefault"/>. Default false (opt-in, AAM-BR-10).
    ///
    /// Surfaced 2026-09-02: the flag has always existed on <c>TeacherConfiguration</c> and has
    /// always gated the parent dashboard's online-exam report AND the parent portal's grades
    /// list, but it was missing from BOTH config DTOs — so no teacher could ever turn it on and
    /// online exam results were permanently invisible to parents with no toggle and no error.
    /// </summary>
    public bool ParentVisibilityOnlineExamDefault { get; set; }

    // ─── Device Lock ───
    public bool IsDeviceLockEnabled { get; set; }

    // ─── Parent Portal (public web follow-up page) ───

    /// <summary>
    /// Whether this teacher accepts followers on the PUBLIC parent portal (parent.edvanz.io).
    /// Opt-in, default false. While false the portal answers every access request for this
    /// teacher with the same "pending" placeholder and writes nothing.
    /// </summary>
    public bool ParentPortalEnabled { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public bool? ShowAttendanceHistoryOnAttendanceScreen { get; set; }
    public bool? ShowPaymentInfoOnAttendanceScreen { get; set; }

    /// <summary>
    /// What the retroactive proration reconcile did during THIS save (REQ-PAY-021/022 rev 2 — the
    /// recalculation must be visible): re-priced / kept counts. Null on plain reads and on saves where
    /// the proration config did not change. Additive — older clients ignore it.
    /// </summary>
    public Payment.ProrationReconcileSummary? ProrationReconcile { get; set; }
}