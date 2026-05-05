using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.ExamHomework;

/// <summary>
/// One row in the Assignment Tracking View list (REQ-EXH-030).
/// Wraps the Domain projection <c>TrackingViewRow</c> for safe Application-layer exposure.
/// </summary>
public class TrackingViewRowDto
{
    public long ObligationId { get; set; }
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public ObligationStatus Status { get; set; }
    public decimal? GradeValue { get; set; }
    public bool IsGradeEntered { get; set; }
    public bool IsBelowPassing { get; set; }
    public bool MarkedByScan { get; set; }
    public DateTime? ScannedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Type-ahead picker row for manual status entry by name (REQ-EXH-023).
/// </summary>
public class StudentPickerRowDto
{
    public long ObligationId { get; set; }
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public ObligationStatus CurrentStatus { get; set; }
}

/// <summary>
/// One eligible student in the add-students-to-template picker (REQ-EXH-035).
/// </summary>
public class EligibleStudentDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string? SessionName { get; set; }
}

/// <summary>
/// Result of the stop-recurrence operation (REQ-EXH-012).
/// </summary>
public class StopRecurrenceResultDto
{
    public long TemplateId { get; set; }
    public DateTime StoppedAt { get; set; }
    public long? LastOccurrenceId { get; set; }
    public int? LastOccurrenceNumber { get; set; }
    public DateTime? LastOccurrenceDueDate { get; set; }
}

/// <summary>
/// Result of adding scope rows (REQ-EXH-035).
/// </summary>
public class AddScopesResultDto
{
    public int ScopesAdded { get; set; }
    public int ObligationsCreated { get; set; }
    public long? NextOccurrenceId { get; set; }
    public DateTime? NextOccurrenceDueDate { get; set; }
}

/// <summary>
/// Result of adding individual students to a template (REQ-EXH-035).
/// </summary>
public class AddStudentsResultDto
{
    public int ScopesAdded { get; set; }
    public int ObligationsCreated { get; set; }

    /// <summary>Students that were already part of the next occurrence — no-op.</summary>
    public List<long> Skipped { get; set; } = new();

    public long? NextOccurrenceId { get; set; }
}

/// <summary>
/// Result of removing a student from a template (REQ-EXH-036).
/// </summary>
public class RemoveStudentResultDto
{
    public int ScopeRowsRemoved { get; set; }
    public int ObligationsDeleted { get; set; }
    public int OccurrencesAffected { get; set; }
    public int RecordsWithDataDeleted { get; set; }
}

/// <summary>
/// Result of the bulk status update (REQ-EXH-025).
/// </summary>
public class BulkStatusResultDto
{
    public long OccurrenceId { get; set; }
    public int RowsUpdated { get; set; }
    public ObligationStatus NewStatus { get; set; }
}

/// <summary>
/// Result of the barcode scan (REQ-EXH-026, NFR-002).
/// Always 200, even on idempotent re-scan.
/// </summary>
public class ScanResultDto
{
    public long ObligationId { get; set; }
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public ObligationStatus Status { get; set; }

    /// <summary>True when the row was already in a non-Pending state (another scan won).</summary>
    public bool AlreadyProcessed { get; set; }

    public DateTime ScannedAt { get; set; }
}

/// <summary>
/// Wrapper for the Grade Entry View — paginated list plus per-occurrence header config.
/// REQ-EXH-026-A/C: Frontend reads MaxGrade once and labels every row's grade input.
/// </summary>
public class GradeEntryViewDto
{
    public long OccurrenceId { get; set; }

    /// <summary>From occurrence's MaxGradeSnapshot — REQ-EXH-026-C.</summary>
    public decimal? MaxGrade { get; set; }

    /// <summary>From occurrence's PassingThresholdSnapshot.</summary>
    public decimal? PassingThreshold { get; set; }

    public PaginatedResponse<List<TrackingViewRowDto>> Students { get; set; } = null!;
}

/// <summary>
/// One audit-log entry for an obligation (REQ-EXH-028).
/// </summary>
public class ObligationAuditEntryDto
{
    public long Id { get; set; }
    public ObligationStatus OldStatus { get; set; }
    public ObligationStatus NewStatus { get; set; }
    public decimal? OldGradeValue { get; set; }
    public decimal? NewGradeValue { get; set; }
    public decimal? MaxGradeSnapshot { get; set; }
    public decimal? PassingThresholdSnapshot { get; set; }
    public string? ChangeReason { get; set; }
    public long ChangedByUserId { get; set; }
    public string? ChangedByUserName { get; set; }
    public DateTime ChangedAt { get; set; }
}
