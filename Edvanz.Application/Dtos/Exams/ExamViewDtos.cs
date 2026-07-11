using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Exams;

// ════════════════════════════════════════════════════════════════════════════
// EXAM HOME (upcoming / past)
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Exam Home payload — upcoming and past exam cards (one card per session-date).</summary>
public class ExamHomeDto
{
    public List<ExamHomeCardDto> Upcoming { get; set; } = new();
    public List<ExamHomeCardDto> Past { get; set; } = new();
}

/// <summary>
/// One exam card. A during-session or separate exam that spans several sessions produces one card
/// per session-date, so each is independently upcoming or past.
/// </summary>
public class ExamHomeCardDto
{
    public long ExamId { get; set; }
    public long OccurrenceId { get; set; }
    public string Name { get; set; } = null!;
    public ExamDeliveryType? DeliveryType { get; set; }
    public long? SessionId { get; set; }
    public string? SessionName { get; set; }
    public DateTime Date { get; set; }
    public int AssignedCount { get; set; }
    public int AttendedCount { get; set; }
    public int MissedCount { get; set; }
    public bool IsPast { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
// OPENED EXAM (grouped by session + stats)
// ════════════════════════════════════════════════════════════════════════════

/// <summary>The opened-exam view: header + global statistics + per-session groups.</summary>
public class ExamViewDto
{
    public long ExamId { get; set; }
    public string Name { get; set; } = null!;
    public ExamDeliveryType? DeliveryType { get; set; }
    public decimal? MaxGrade { get; set; }
    public decimal? SuccessScore { get; set; }

    /// <summary>Statistics across every student in every session of the exam.</summary>
    public ExamStatsDto GlobalStats { get; set; } = new();

    public List<ExamSessionViewDto> Sessions { get; set; } = new();
}

/// <summary>Grade + attendance statistics. Averages/high/low are over students with a grade entered.</summary>
public class ExamStatsDto
{
    public int TotalStudents { get; set; }
    public int GradedCount { get; set; }
    public decimal? Average { get; set; }
    public decimal? Highest { get; set; }
    public decimal? Lowest { get; set; }
    public int AttendedCount { get; set; }
    public int MissedCount { get; set; }
    public int PendingCount { get; set; }
    public int BelowPassingCount { get; set; }
}

/// <summary>One session within the exam, with its own statistics and student roster.</summary>
public class ExamSessionViewDto
{
    public long SessionId { get; set; }
    public string? SessionName { get; set; }
    public long OccurrenceId { get; set; }
    public DateTime Date { get; set; }
    public ExamStatsDto Stats { get; set; } = new();
    public List<ExamStudentRowDto> Students { get; set; } = new();
}

/// <summary>One student row in an exam session (attendance + grade + concurrency token).</summary>
public class ExamStudentRowDto
{
    public long ObligationId { get; set; }
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;

    /// <summary>Attendance/grade status name (e.g. "Attended", "AttendedWithGrade", "DidNotAttend", "Pending").</summary>
    public string Status { get; set; } = null!;
    public bool Attended { get; set; }
    public decimal? Grade { get; set; }
    public bool IsGradeEntered { get; set; }
    public bool IsBelowPassing { get; set; }

    /// <summary>Base64 concurrency token — echo it back when saving this student's grade.</summary>
    public string RowVersion { get; set; } = null!;
}

/// <summary>Paged roster for a single session within an exam (the drill-in / large-session view).</summary>
public class ExamSessionRosterDto
{
    public long ExamId { get; set; }
    public long SessionId { get; set; }
    public string? SessionName { get; set; }
    public long OccurrenceId { get; set; }
    public DateTime Date { get; set; }
    public decimal? MaxGrade { get; set; }
    public decimal? SuccessScore { get; set; }
    public PaginatedResponse<List<ExamStudentRowDto>> Students { get; set; } = null!;
}
