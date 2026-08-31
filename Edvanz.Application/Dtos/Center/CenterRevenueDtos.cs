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

/// <summary>
/// One ACTIVE session's recurrence SCHEDULE (not a materialized occurrence) belonging to one of the
/// center's active teachers, carrying the owning teacher's identity. The union of these across the
/// center powers the front-desk attendance picker's teacher-home-style week strip: the client runs the
/// SAME recurrence logic the teacher home uses (day-of-week / biweekly / monthly, bounded by
/// start/end) to decide which classes fall on the selected day, then groups them per teacher. Mirrors
/// the teacher <c>SessionDto</c> schedule fields so the client can reuse its existing mapper.
/// </summary>
public class CenterTeacherScheduleDto
{
    public long TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string TeacherCode { get; set; } = string.Empty;
    public long SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    /// <summary>Recurrence type (serialized as string: "Weekly"/"BiWeekly"/"Monthly").</summary>
    public OccurrenceType OccurrenceType { get; set; }
    /// <summary>Weekly/biweekly selected weekdays (app day-index list), or null for monthly.</summary>
    public List<int>? SelectedDays { get; set; }
    /// <summary>Day-of-month for monthly recurrence, or null.</summary>
    public byte? MonthlyDayOfMonth { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    /// <summary>Scheduled start time of the class (time of day).</summary>
    public TimeSpan StartTime { get; set; }
    /// <summary>Class duration in minutes (client derives the end-time chip).</summary>
    public short DurationMinutes { get; set; }
    /// <summary>Live roster count for the session (same source as the teacher-home card).</summary>
    public int StudentCount { get; set; }
    /// <summary>Whether the session's end date has passed (always false in the active set; kept for parity).</summary>
    public bool IsExpired { get; set; }
}
