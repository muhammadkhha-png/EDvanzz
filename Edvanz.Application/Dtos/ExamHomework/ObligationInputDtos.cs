using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.ExamHomework;

/// <summary>
/// Input for the Exam manual status entry endpoint (REQ-EXH-023, REQ-EXH-028).
/// Restricted by Option B routing: only valid Exam states (Attended, AttendedWithGrade, DidNotAttend)
/// are allowed; the service rejects homework states with 400 InvalidStatusForExam.
/// </summary>
public class UpdateExamStatusDto
{
    /// <summary>
    /// One of: Attended, AttendedWithGrade, DidNotAttend.
    /// </summary>
    public ObligationStatus Status { get; set; }

    /// <summary>
    /// Required iff Status = AttendedWithGrade. Must be 0 ≤ value ≤ MaxGradeSnapshot.
    /// Must be null for Attended and DidNotAttend.
    /// </summary>
    public decimal? GradeValue { get; set; }

    /// <summary>Required — concurrency token from the previous read.</summary>
    public byte[] RowVersion { get; set; } = null!;
}

/// <summary>
/// Input for the Homework manual status entry endpoint (REQ-EXH-023, REQ-EXH-028).
/// Allowed states depend on the occurrence's TrackingModeSnapshot:
///   - CompletionOnly: Done, NotDone
///   - Graded: DoneWithGrade, DoneWithoutGrade, NotDone
/// </summary>
public class UpdateHomeworkStatusDto
{
    public ObligationStatus Status { get; set; }

    /// <summary>
    /// Required iff Status = DoneWithGrade. Null for all other states.
    /// </summary>
    public decimal? GradeValue { get; set; }

    public byte[] RowVersion { get; set; } = null!;
}

/// <summary>
/// Input for status entry by student code (REQ-EXH-024).
/// Wraps the per-type DTOs — the service routes based on assignment type and rejects
/// status values that don't match.
/// </summary>
public class UpdateStatusByCodeDto
{
    /// <summary>Student code (e.g., "S2024-031"). Resolved to TeacherStudentId in the service.</summary>
    public string StudentCode { get; set; } = null!;

    public ObligationStatus Status { get; set; }
    public decimal? GradeValue { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

/// <summary>
/// Single item in a bulk status update (REQ-EXH-025).
/// </summary>
public class BulkStatusItemDto
{
    public long ObligationId { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

/// <summary>
/// Input for the bulk multi-select status entry (REQ-EXH-025).
/// All items receive the same Status (and GradeValue if applicable).
/// All items must belong to the same occurrence and the JWT teacher.
/// </summary>
public class BulkUpdateStatusDto
{
    /// <summary>Shared status applied to every item.</summary>
    public ObligationStatus Status { get; set; }

    /// <summary>Shared grade — required iff Status is a *WithGrade variant; null otherwise.</summary>
    public decimal? GradeValue { get; set; }

    /// <summary>Max 1000 items per batch — guards transaction time.</summary>
    public List<BulkStatusItemDto> Items { get; set; } = new();
}

/// <summary>
/// Input for the barcode scan idempotent atomic mark (REQ-EXH-026, NFR-002).
/// scannedAt is server-stamped — no client-supplied timestamp.
/// </summary>
public class ScanBarcodeDto
{
    /// <summary>Raw barcode value as captured by the scanner.</summary>
    public string Barcode { get; set; } = null!;
}

/// <summary>
/// Input for entering or amending a grade for an obligation already in the
/// grade-pending state (REQ-EXH-026-D / 026-E, REQ-EXH-027).
/// </summary>
public class EnterGradeDto
{
    public decimal GradeValue { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
