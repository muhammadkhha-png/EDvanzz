using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Projection for one exam occurrence on the Exam Home screen (one card = one session-date).
/// </summary>
public sealed class ExamHomeOccurrenceRow
{
    public long OccurrenceId { get; set; }
    public long TemplateId { get; set; }
    public string ExamName { get; set; } = null!;
    public ExamDeliveryType? DeliveryType { get; set; }
    public long? SessionId { get; set; }
    public string? SessionName { get; set; }
    /// <summary>The linked session occurrence for during-session exams (null for separate-time exams).</summary>
    public long? SessionOccurrenceId { get; set; }
    public DateTime DueDate { get; set; }
}

/// <summary>
/// Projection describing one exam occurrence's session + snapshotted grading config.
/// Used by the opened-exam view to build the per-session grouping and stats.
/// </summary>
public sealed class ExamOccurrenceInfoRow
{
    public long OccurrenceId { get; set; }
    public long? SessionId { get; set; }
    public string? SessionName { get; set; }
    public DateTime DueDate { get; set; }
    public decimal? MaxGradeSnapshot { get; set; }
    public decimal? PassingThresholdSnapshot { get; set; }
}

/// <summary>
/// One student's row within an exam, carrying occurrence linkage so the service can group by
/// session and compute per-session / global statistics without extra queries.
/// </summary>
public sealed class ExamRosterRow
{
    public long OccurrenceId { get; set; }
    public long ObligationId { get; set; }
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public ObligationStatus Status { get; set; }
    public decimal? GradeValue { get; set; }
    public bool IsGradeEntered { get; set; }
    public byte[] ObligationRowVersion { get; set; } = null!;
}
