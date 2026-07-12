using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Exams;

// ════════════════════════════════════════════════════════════════════════════
// EXAM HOME (upcoming / past)
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Exam Home payload — paginated upcoming and past exam cards (one card per session-date).</summary>
public class ExamHomeDto
{
    public PaginatedResponse<List<ExamHomeCardDto>> Upcoming { get; set; } = new();
    public PaginatedResponse<List<ExamHomeCardDto>> Past { get; set; } = new();
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

    /// <summary>Total students assigned to this exam occurrence (same value as <see cref="AssignedCount"/>,
    /// exposed under the stats naming used on the opened-exam view).</summary>
    public int TotalStudents { get; set; }

    public int AttendedCount { get; set; }
    public int MissedCount { get; set; }

    /// <summary>Students with no attendance recorded yet (neither attended nor absent).</summary>
    public int PendingCount { get; set; }

    public bool IsPast { get; set; }

    /// <summary>How the exam was assigned to its recipients: "Sessions" or "Groups".</summary>
    public string SelectionMode { get; set; } = "Sessions";

    /// <summary>Sessions the exam was assigned to (populated when assigned by sessions).</summary>
    public List<SessionRefDto> AssignedSessions { get; set; } = new();

    /// <summary>Groups the exam was assigned to, each expanded to its member sessions (when assigned by groups).</summary>
    public List<GroupRefDto> AssignedGroups { get; set; } = new();
}

/// <summary>A session reference (id + name).</summary>
public class SessionRefDto
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
}

/// <summary>A group reference (id + name) with its member sessions.</summary>
public class GroupRefDto
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public List<SessionRefDto> Sessions { get; set; } = new();
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

    /// <summary>
    /// Count of DISTINCT students across the whole exam. Unlike <see cref="ExamStatsDto.TotalStudents"/>
    /// on <see cref="GlobalStats"/> — which counts obligation rows (student-session pairs) — this counts
    /// each student once even if they sit the exam in more than one of its sessions.
    /// </summary>
    public int DistinctStudentCount { get; set; }

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

    /// <summary>
    /// True once any attendance has been recorded for this session's exam (at least one student
    /// Attended or DidNotAttend). Drives the UI's "Take attendance" vs "Edit attendance" label.
    /// </summary>
    public bool AttendanceTaken { get; set; }

    /// <summary>
    /// True once any grade has been entered for this session's exam. Drives the UI's "Take grades"
    /// vs "Edit grades" label.
    /// </summary>
    public bool GradesTaken { get; set; }

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
