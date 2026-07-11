namespace Edvanz.Application.Dtos.Exams;

/// <summary>
/// Batch manual attendance marking for a SEPARATE-time exam occurrence (present/absent). Rejected for
/// during-session exams, whose attendance comes from the session.
/// </summary>
public class ExamAttendanceDto
{
    public long OccurrenceId { get; set; }
    public List<ExamAttendanceItemDto> Items { get; set; } = new();
}

public class ExamAttendanceItemDto
{
    public long ObligationId { get; set; }
    public bool Present { get; set; }
}

public class ExamAttendanceResultDto
{
    public int UpdatedCount { get; set; }
    public bool AllSucceeded { get; set; }
    public List<ExamAttendanceItemResultDto> Items { get; set; } = new();
}

public class ExamAttendanceItemResultDto
{
    public long ObligationId { get; set; }
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? Status { get; set; }
    public string? RowVersion { get; set; }
}

/// <summary>Scan a student's QR/barcode (resolved as the student code) to mark them attended for an exam occurrence.</summary>
public class ExamScanDto
{
    public long OccurrenceId { get; set; }
    public string Code { get; set; } = null!;
}

public class ExamScanResultDto
{
    public long ObligationId { get; set; }
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string Status { get; set; } = null!;

    /// <summary>True when the student was already marked attended (idempotent re-scan).</summary>
    public bool AlreadyProcessed { get; set; }
}
