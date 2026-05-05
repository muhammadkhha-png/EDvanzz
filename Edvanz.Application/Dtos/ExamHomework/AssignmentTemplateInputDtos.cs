using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.ExamHomework;

/// <summary>
/// Single scope target for create/add operations.
/// REQ-EXH-002: Three scope kinds — IndividualStudent, Session, SessionGroup.
/// REQ-EXH-003: Service layer deduplicates students resolved across multiple scopes.
/// Service-layer validation enforces that exactly one of the nullable IDs matches
/// <see cref="ScopeType"/>.
/// </summary>
public class ScopeInputDto
{
    /// <summary>
    /// Discriminator for which target FK is populated.
    /// </summary>
    public AssignmentScopeType ScopeType { get; set; }

    /// <summary>
    /// Required when <see cref="ScopeType"/> = <c>IndividualStudent</c>; null otherwise.
    /// </summary>
    public long? TeacherStudentId { get; set; }

    /// <summary>
    /// Required when <see cref="ScopeType"/> = <c>Session</c>; null otherwise.
    /// </summary>
    public long? SessionId { get; set; }

    /// <summary>
    /// Required when <see cref="ScopeType"/> = <c>SessionGroup</c>; null otherwise.
    /// </summary>
    public long? SessionGroupId { get; set; }
}

/// <summary>
/// Input for creating a new assignment template.
/// REQ-EXH-001 through REQ-EXH-010, REQ-EXH-014/015, REQ-EXH-020/021.
/// teacherId and createdByUserId are NOT in this DTO — both come from the JWT.
/// </summary>
public class CreateAssignmentTemplateDto
{
    /// <summary>REQ-EXH-001: Exam or Homework.</summary>
    public AssignmentType AssignmentType { get; set; }

    /// <summary>REQ-EXH-004: English name (required, max 200 chars).</summary>
    public string Name { get; set; } = null!;

    /// <summary>REQ-EXH-004: Arabic name (required, max 200 chars).</summary>
    public string NameAr { get; set; } = null!;

    /// <summary>REQ-EXH-006: Optional notes (max 2000 chars).</summary>
    public string? Notes { get; set; }

    /// <summary>REQ-EXH-005: Mandatory assignment date (date-only, must be today or future).</summary>
    public DateTime AssignmentDate { get; set; }

    /// <summary>REQ-EXH-008: Whether this is a recurring template.</summary>
    public bool IsRecurring { get; set; }

    /// <summary>
    /// REQ-EXH-009/010: Recurrence pattern. When IsRecurring=false, must be OneTime.
    /// Homework allows OneTime/EverySession/EveryTwoSessions; Exam allows OneTime/Monthly.
    /// </summary>
    public RecurrencePattern RecurrencePattern { get; set; }

    /// <summary>Optional end date for recurrence; null = open-ended until stopped.</summary>
    public DateTime? RecurrenceEndDate { get; set; }

    /// <summary>REQ-EXH-014/015: Required for Homework; null for Exam.</summary>
    public HomeworkTrackingMode? TrackingMode { get; set; }

    /// <summary>REQ-EXH-020: Required for Exam (must be > 0); null for Homework.</summary>
    public decimal? MaxGrade { get; set; }

    /// <summary>REQ-EXH-021: Required for Exam (0 ≤ value ≤ MaxGrade); null for Homework.</summary>
    public decimal? PassingThreshold { get; set; }

    /// <summary>
    /// REQ-EXH-002/003: At least one scope required. Service deduplicates across scopes.
    /// </summary>
    public List<ScopeInputDto> Scopes { get; set; } = new();
}

/// <summary>
/// Input for editing an existing assignment template.
/// REQ-EXH-013: Recurrence pattern is locked once any obligation is non-Pending.
/// REQ-EXH-034: Editable fields — name, notes, date, scopes (via separate endpoints), grading config.
///
/// All fields are nullable — only provided fields are updated. Snapshots on existing
/// occurrences are NOT changed by edits (decoupling — see service implementation notes).
/// </summary>
public class UpdateAssignmentTemplateDto
{
    public string? Name { get; set; }
    public string? NameAr { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Only changeable when IsRecurring = false. For recurring templates,
    /// occurrence dates are immutable from this endpoint.
    /// </summary>
    public DateTime? AssignmentDate { get; set; }

    /// <summary>
    /// REQ-EXH-013: Subject to lock check via repo.CanEditRecurrencePatternAsync.
    /// </summary>
    public RecurrencePattern? RecurrencePattern { get; set; }

    public DateTime? RecurrenceEndDate { get; set; }

    public HomeworkTrackingMode? TrackingMode { get; set; }
    public decimal? MaxGrade { get; set; }
    public decimal? PassingThreshold { get; set; }

    /// <summary>Required — concurrency token from the previous read.</summary>
    public byte[] RowVersion { get; set; } = null!;
}

/// <summary>
/// Input for stopping a recurring template (REQ-EXH-012, BR-EXH-002).
/// Past occurrences are preserved; future scheduler runs skip this template.
/// </summary>
public class StopRecurrenceDto
{
    public byte[] RowVersion { get; set; } = null!;
}

/// <summary>
/// Input for adding more scope rows to an existing template (REQ-EXH-035).
/// </summary>
public class AddScopesDto
{
    public List<ScopeInputDto> Scopes { get; set; } = new();
}

/// <summary>
/// Input for adding specific students to an existing template (REQ-EXH-035, BR-EXH-005).
/// Uses the IndividualStudent scope type internally; this DTO is the convenience API
/// for the "select students from picker" UX flow.
/// </summary>
public class AddStudentsToTemplateDto
{
    /// <summary>
    /// Non-empty, max 500 ids. Each student must belong to the JWT teacher.
    /// </summary>
    public List<long> TeacherStudentIds { get; set; } = new();
}
