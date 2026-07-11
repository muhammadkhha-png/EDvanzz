namespace Edvanz.Application.Dtos.Exams;

/// <summary>
/// Batch grade save — distinct per-student grades committed together ("Submit (N) students" /
/// "Saved (N) changes"). Each item carries its own grade and concurrency token.
/// </summary>
public class BatchGradeDto
{
    public List<GradeItemDto> Items { get; set; } = new();
}

/// <summary>One student's grade within a batch save.</summary>
public class GradeItemDto
{
    public long ObligationId { get; set; }

    /// <summary>
    /// The grade to record: 0 ≤ grade ≤ the exam's max. Send <c>null</c> to CLEAR a previously entered
    /// grade — the student reverts from AttendedWithGrade to Attended (attendance is preserved). A null
    /// on an already-ungraded or absent row is a harmless no-op.
    /// </summary>
    public decimal? Grade { get; set; }

    /// <summary>Concurrency token from the last read of this student's row (base64 is also accepted by binding).</summary>
    public byte[] RowVersion { get; set; } = null!;
}

/// <summary>
/// Result of a batch grade save. Valid rows are applied; invalid rows are reported per-item with a
/// stable code (the whole batch still returns 200 — check <see cref="AllSucceeded"/> / per-item
/// <see cref="BatchGradeItemResultDto.Success"/>). A concurrency conflict fails the whole batch (409).
/// </summary>
public class BatchGradeResultDto
{
    public int UpdatedCount { get; set; }
    public bool AllSucceeded { get; set; }
    public List<BatchGradeItemResultDto> Items { get; set; } = new();
}

public class BatchGradeItemResultDto
{
    public long ObligationId { get; set; }
    public bool Success { get; set; }

    /// <summary>Stable failure code when <see cref="Success"/> is false (e.g. "GradeExceedsMax").</summary>
    public string? Code { get; set; }

    /// <summary>New status name on success (e.g. "AttendedWithGrade").</summary>
    public string? Status { get; set; }
    public decimal? Grade { get; set; }

    /// <summary>Fresh base64 concurrency token after save — use it for the next edit of this row.</summary>
    public string? RowVersion { get; set; }
}
