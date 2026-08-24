using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Center;

/// <summary>Per-teacher revenue row for a month: real collection AND expected, each with the center's cut.</summary>
public class CenterRevenueRowDto
{
    public long TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string TeacherCode { get; set; } = string.Empty;
    public SubscriptionPlanType? PlanType { get; set; }
    /// <summary>Effective share % applied = override ?? center default.</summary>
    public decimal SharePercent { get; set; }
    public decimal Collected { get; set; }
    public decimal Expected { get; set; }
    public decimal CutOnCollected { get; set; }
    public decimal CutOnExpected { get; set; }
}

/// <summary>Center revenue report for a month: per-teacher rows + center totals.</summary>
public class CenterRevenueReportDto
{
    /// <summary>The reported month as "YYYY-MM".</summary>
    public string Month { get; set; } = string.Empty;
    public decimal DefaultSharePercent { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalExpected { get; set; }
    public decimal TotalCutOnCollected { get; set; }
    public decimal TotalCutOnExpected { get; set; }
    public List<CenterRevenueRowDto> Teachers { get; set; } = new();
}

/// <summary>A center-wide code-resolve candidate (one per teacher that uses the code).</summary>
public class CenterStudentResolveCandidateDto
{
    public long TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string TeacherCode { get; set; } = string.Empty;
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public string? StudentPhoneNumber { get; set; }

    /// <summary>The student's assigned session (null if unassigned).</summary>
    public long? SessionId { get; set; }
    public string? SessionName { get; set; }

    /// <summary>
    /// The id of that session's occurrence for TODAY (teacher-local), or null when there is no class
    /// scheduled today. When present, a front-desk scan can jump straight into the take-attendance form
    /// for this session/occurrence; when null the app tells the operator there's no class today.
    /// </summary>
    public long? TodaySessionOccurrenceId { get; set; }
}

/// <summary>
/// One of TODAY's scheduled class occurrences across the center's ACTIVE teachers — the
/// session-first pick list for front-desk attendance scanning: the operator chooses the class
/// being held, then scans that class's students continuously. "Today" is each teacher's local
/// date, mirroring <see cref="CenterStudentResolveCandidateDto.TodaySessionOccurrenceId"/>.
/// </summary>
public class CenterTodaySessionDto
{
    public long TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string TeacherCode { get; set; } = string.Empty;
    public long SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public long SessionOccurrenceId { get; set; }
    /// <summary>The occurrence's date (teacher-local calendar day).</summary>
    public DateTime OccurrenceDate { get; set; }
    /// <summary>Scheduled start time of the class (time of day).</summary>
    public TimeSpan StartTime { get; set; }
    /// <summary>Scheduled end time = StartTime + the session's duration.</summary>
    public TimeSpan EndTime { get; set; }
    /// <summary>Occurrence lifecycle status (serialized as string, e.g. "Pending").</summary>
    public OccurrenceStatus Status { get; set; }
}
