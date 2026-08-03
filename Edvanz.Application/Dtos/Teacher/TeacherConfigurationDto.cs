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
    public bool ParentVisibilityVideo { get; set; }
    public bool ParentVisibilityOnlineExamDefault { get; set; }

    public DateTime? UpdatedAt { get; set; }
}