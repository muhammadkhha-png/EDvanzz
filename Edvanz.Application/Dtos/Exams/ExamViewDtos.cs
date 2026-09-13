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

    /// <summary>
    /// How many papers (PDF / photos) are attached to this exam, so the card can show a
    /// paper-clip without a second request. 0 when none have been uploaded.
    /// </summary>
    public int AttachmentsCount { get; set; }

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

    /// <summary>
    /// The exam's description / notes. Returned so the EDIT form can hydrate it — without
    /// this the form opened the field blank, and a structural save then wrote that blank
    /// back over the teacher's text.
    /// </summary>
    public string? Notes { get; set; }
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

    /// <summary>
    /// The exam's papers (PDF / photos) students review afterwards. Empty until the teacher
    /// uploads one.
    /// </summary>
    public List<ExamAttachmentDto> Attachments { get; set; } = new();

    /// <summary>When and whether those papers are visible to students.</summary>
    public ExamAttachmentReleaseDto AttachmentRelease { get; set; } = new();
}

/// <summary>
/// One paper attached to an offline exam. Shape mirrors <c>VideoAttachmentDto</c> so both
/// file lists render through the same client widget.
/// </summary>
public class ExamAttachmentDto
{
    /// <summary>The registry id (<c>FileObject.PublicId</c>) — the token in the gated URL.</summary>
    public Guid Id { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSizeBytes { get; set; }

    /// <summary>Stable gated URL (<c>/api/files/{id}</c>) — re-checks access on every fetch.</summary>
    public string ReadUrl { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// The state of an exam's paper-release gate, for the teacher's switch.
/// </summary>
public class ExamAttachmentReleaseDto
{
    /// <summary>
    /// When the paper opens to students on its own: the last class to sit the exam, plus the
    /// teacher's configured delay. Null while the exam has no occurrences.
    /// </summary>
    public DateTime? ReleaseAt { get; set; }

    /// <summary>
    /// The teacher's override — <c>true</c> released early, <c>false</c> held back,
    /// <c>null</c> following the schedule.
    /// </summary>
    public bool? Override { get; set; }

    /// <summary>
    /// What students see RIGHT NOW: <c>Override ?? (utcNow &gt;= ReleaseAt)</c>. The single
    /// value the switch should render, so the client never re-derives the rule.
    /// </summary>
    public bool VisibleToStudents { get; set; }

    /// <summary>The teacher's configured delay, so the UI can explain the date it shows.</summary>
    public int ReleaseDelayHours { get; set; }

    /// <summary>
    /// Names of the exam's classes that have NOT sat it yet. Populated so releasing early can
    /// warn by name instead of in the abstract; empty once every class has sat the exam.
    /// </summary>
    public List<string> SessionsYetToSit { get; set; } = new();
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
