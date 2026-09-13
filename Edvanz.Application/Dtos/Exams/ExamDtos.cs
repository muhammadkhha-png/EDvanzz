using System.ComponentModel.DataAnnotations;
using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Exams;

/// <summary>
/// Create-exam request (offline exam). The exam name is a SINGLE value that may be Arabic OR
/// English — there is no separate bilingual name. Every exam is anchored to one or more sessions;
/// the recipient "Groups"/"Students" picks in the UI resolve to the per-session entries below
/// (a group → its sessions; a specific student → their session with that student in StudentIds).
/// Dates depend on the delivery type: DuringSession = one picked class occurrence PER resolved
/// session in <see cref="SessionOccurrences"/>; SeparateTime = the single <see cref="ExamDate"/>.
/// </summary>
public class CreateExamDto
{
    /// <summary>Exam name — Arabic or English, single field. Required, max 200 chars.</summary>
    [Required]
    public string Name { get; set; } = null!;

    /// <summary>Optional description / notes.</summary>
    public string? Notes { get; set; }

    /// <summary>DuringSession (taken inside a scheduled class) or SeparateTime (own date).</summary>
    public ExamDeliveryType DeliveryType { get; set; }

    /// <summary>Maximum exam score (must be &gt; 0).</summary>
    public decimal MaxGrade { get; set; }

    /// <summary>Passing / success score (0 ≤ value ≤ MaxGrade).</summary>
    public decimal SuccessScore { get; set; }

    /// <summary>
    /// SeparateTime ONLY: the exam's own single date (today or future), applied to every resolved
    /// session. Must be omitted for DuringSession, which anchors each session to its own picked
    /// class occurrence in <see cref="SessionOccurrences"/> instead.
    /// </summary>
    public DateTime? ExamDate { get; set; }

    /// <summary>
    /// DuringSession ONLY: the picked class occurrence of EVERY resolved session — exactly one
    /// entry per selected session, or per member session of the selected groups (the picked
    /// occurrences may fall on different dates across sessions). Pick each session's occurrence
    /// from <c>GET /api/exams/session-dates</c>. Must be omitted for SeparateTime.
    /// </summary>
    public List<ExamSessionOccurrenceDto>? SessionOccurrences { get; set; }

    /// <summary>
    /// Recipient by sessions — the session ids. Provide EITHER <see cref="SessionIds"/> OR
    /// <see cref="GroupIds"/> (leave the other null/empty).
    /// </summary>
    public List<long>? SessionIds { get; set; }

    /// <summary>
    /// Recipient by groups — the group ids; each expands to its member sessions server-side.
    /// Provide EITHER this OR <see cref="SessionIds"/>.
    /// </summary>
    public List<long>? GroupIds { get; set; }

    /// <summary>Optional global student subset across the resolved sessions; null/empty = every student.</summary>
    public List<long>? StudentIds { get; set; }

    /// <summary>
    /// Optional papers (PDF or photos) to attach at creation — <c>fileId</c>s from
    /// <c>POST /api/upload</c> with <c>category=ExamAttachment</c>. Most teachers upload the
    /// paper AFTER the exam instead, via <c>POST /api/exams/{examId}/attachments</c>; this is
    /// here so preparing it in advance does not need a second trip.
    /// <para>
    /// Students cannot see it until the exam's release gate opens, so attaching early is
    /// safe — see <c>AssignmentTemplate.AttachmentsReleaseBaseAt</c>.
    /// </para>
    /// </summary>
    public List<Guid>? AttachmentFileIds { get; set; }
}

/// <summary>
/// Request for <c>POST /api/exams/{examId}/attachments</c> — attach already-uploaded papers.
/// </summary>
public class AddExamAttachmentsDto
{
    /// <summary>
    /// <c>fileId</c>s returned by <c>POST /api/upload</c> (category <c>ExamAttachment</c>).
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<Guid> FileIds { get; set; } = new();
}

/// <summary>
/// Request for <c>PUT /api/exams/{examId}/attachments/release</c> — the teacher's override of
/// the automatic release schedule.
/// </summary>
public class SetExamAttachmentReleaseDto
{
    /// <summary>
    /// <c>true</c> = show the paper to students now, ahead of schedule. <c>false</c> = hold it
    /// back even though the schedule has passed. <c>null</c> = follow the schedule again.
    /// <para>
    /// Nullable ON PURPOSE, and null is a real value here (not "unchanged") — clearing the
    /// override is exactly how a teacher hands control back to the automatic release.
    /// </para>
    /// </summary>
    public bool? Override { get; set; }
}

/// <summary>
/// One during-session exam anchor: a targeted session and the scheduled class occurrence the exam
/// is taken in (its date becomes that session's exam date and its attendance drives the exam).
/// Used by <see cref="CreateExamDto.SessionOccurrences"/> / <see cref="UpdateExamDto.SessionOccurrences"/>.
/// </summary>
public class ExamSessionOccurrenceDto
{
    /// <summary>The session id — must be one of the exam's resolved sessions (selected directly or via a group).</summary>
    [Required]
    public long? SessionId { get; set; }

    /// <summary>
    /// The picked class occurrence of that session (from <c>GET /api/exams/session-dates</c>).
    /// Must belong to the session; may be in the past (attendance is back-filled from the class).
    /// </summary>
    [Required]
    public long? SessionOccurrenceId { get; set; }
}

/// <summary>
/// Edit-exam request (clean surface, PUT /api/exams/{examId}). Mirrors <see cref="CreateExamDto"/> —
/// the edit screen submits the same fields. Metadata (name, notes, grade bounds) is always editable;
/// STRUCTURAL fields (delivery type, exam date, and the assigned sessions/groups/students) rebuild the
/// exam's per-session occurrences and obligations and are therefore <b>rejected once the exam has any
/// recorded attendance or grade</b> (code <c>ExamHasResultsCannotRestructure</c>). Same grade rules as
/// create (MaxGrade &gt; 0, 0 ≤ SuccessScore ≤ MaxGrade). Concurrency is handled server-side.
/// </summary>
public class UpdateExamDto
{
    /// <summary>Exam name — Arabic or English, single field. Required, max 200 chars.</summary>
    [Required]
    public string Name { get; set; } = null!;

    /// <summary>Optional description / notes (max 2000 chars).</summary>
    public string? Notes { get; set; }

    /// <summary>DuringSession (taken inside a scheduled class) or SeparateTime (own date). Structural.</summary>
    public ExamDeliveryType DeliveryType { get; set; }

    /// <summary>Maximum exam score (must be &gt; 0).</summary>
    public decimal MaxGrade { get; set; }

    /// <summary>Passing / success score (0 ≤ value ≤ MaxGrade).</summary>
    public decimal SuccessScore { get; set; }

    /// <summary>SeparateTime ONLY: the exam's own single date (today or future). Structural.</summary>
    public DateTime? ExamDate { get; set; }

    /// <summary>
    /// DuringSession ONLY: the picked class occurrence of EVERY resolved session (one entry per
    /// selected session or per member session of the selected groups). Structural.
    /// </summary>
    public List<ExamSessionOccurrenceDto>? SessionOccurrences { get; set; }

    /// <summary>Recipient by sessions — provide EITHER this OR <see cref="GroupIds"/>. Structural.</summary>
    public List<long>? SessionIds { get; set; }

    /// <summary>Recipient by groups (expand to member sessions server-side) — EITHER this OR <see cref="SessionIds"/>. Structural.</summary>
    public List<long>? GroupIds { get; set; }

    /// <summary>Optional global student subset across the resolved sessions; null/empty = every student. Structural.</summary>
    public List<long>? StudentIds { get; set; }
}

/// <summary>Result of creating an exam — the exam id plus the per-session occurrences that were materialized.</summary>
public class ExamCreatedDto
{
    public long ExamId { get; set; }
    public string Name { get; set; } = null!;
    public ExamDeliveryType DeliveryType { get; set; }
    public decimal MaxGrade { get; set; }
    public decimal SuccessScore { get; set; }
    public int SessionsCount { get; set; }
    public int StudentsAssigned { get; set; }
    public List<ExamSessionCreatedDto> Sessions { get; set; } = new();
}

public class ExamSessionCreatedDto
{
    public long SessionId { get; set; }
    public long OccurrenceId { get; set; }
    public DateTime ExamDate { get; set; }
    public int StudentsAssigned { get; set; }
}

/// <summary>One selectable exam date for the "during session" date picker (a session occurrence).</summary>
public class SessionExamDateDto
{
    public long SessionOccurrenceId { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
}
