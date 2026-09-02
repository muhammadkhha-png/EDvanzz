using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Teacher;

/// <summary>
/// Input DTO for saving or updating all teacher configuration settings.
/// Maps to AAM-FR-04.2 through AAM-FR-04.9.
/// All fields have defaults matching system defaults (AAM-BR-04).
/// </summary>
public class UpdateTeacherConfigurationDto
{
    // ─── AAM-FR-04.1: Student Capacity Package ───

    /// <summary>
    /// Selected capacity package tier Id. Nullable during initial skip.
    /// </summary>
    public long? StudentCapacityPackageId { get; set; }

    // ─── AAM-FR-04.2: Student Code Generation ───

    public GenerationMode StudentCodeGenerationMode { get; set; } = GenerationMode.Auto;
    public GenerationLanguage StudentCodeLanguage { get; set; } = GenerationLanguage.English;

    // ─── AAM-FR-04.3: Session name Configuration ───

    public GenerationMode SessionNameMode { get; set; } = GenerationMode.Auto;
    public GenerationLanguage SessionNameLanguage { get; set; } = GenerationLanguage.English;

    // ─── AAM-FR-04.4: Prorated Payment Configuration ───

    public bool IsProratedPaymentEnabled { get; set; } = false;

    /// <summary>
    /// How the app suggests a new student's joining-month amount (REQ-PAY-021/022): ByPercentage
    /// (default — the current behaviour, applied to every existing account) | ByClasses | Manual.
    /// Accepts a string via <c>JsonStringEnumConverter</c>. Omitted → ByPercentage.
    /// </summary>
    public ProrationMethod ProrationMethod { get; set; } = ProrationMethod.ByPercentage;

    public List<ProratedTierDto> ProratedTiers { get; set; } = new();

    // ─── AAM-FR-04.5 & 04.6: Alert Thresholds ───

    public int ConsecutiveAbsenceThreshold { get; set; } = 3;
    public int ConsecutiveUnpaidThreshold { get; set; } = 3;

    // ─── AAM-FR-04.7: Barcode Configuration ───

    public BarcodeDisplayMode BarcodeDisplayMode { get; set; } = BarcodeDisplayMode.InApp;

    // ─── AAM-FR-04.8: Student Visibility ───

    public bool StudentVisibilityAttendance { get; set; } = true;
    public bool StudentVisibilityPayment { get; set; } = true;
    public bool StudentVisibilityHomework { get; set; } = true;
    public bool StudentVisibilityExamDefault { get; set; } = true;
    public bool StudentVisibilityVideo { get; set; } = true;

    // ─── AAM-FR-04.9: Parent Visibility ───

    public bool ParentVisibilityAttendance { get; set; } = true;
    public bool ParentVisibilityPayment { get; set; } = true;
    public bool ParentVisibilityHomework { get; set; } = true;
    public bool ParentVisibilityExamDefault { get; set; } = false;

    // ─── Device Lock ───

    /// <summary>
    /// When true, each linked student is bound to the first device they use to open this
    /// teacher and can only open the teacher from that device afterwards (per teacher).
    /// Default: false.
    /// </summary>
    public bool IsDeviceLockEnabled { get; set; } = false;
    public bool? ShowPaymentInfoOnAttendanceScreen { get; set; }
    public bool? ShowAttendanceHistoryOnAttendanceScreen { get; set; }
}

/// <summary>
/// Represents a single prorated payment tier within the configuration.
/// AAM-FR-04.4: Up to 3 tiers, each with a threshold range and fraction rate.
/// </summary>
public class ProratedTierDto
{
    public int TierNumber { get; set; }
    public int ThresholdDayStart { get; set; }
    public int ThresholdDayEnd { get; set; }
    public decimal FractionRate { get; set; }
}