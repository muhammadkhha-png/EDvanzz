using System.ComponentModel.DataAnnotations;
using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Exams;

/// <summary>
/// Create-exam request (offline exam). The exam name is a SINGLE value that may be Arabic OR
/// English — there is no separate bilingual name. Every exam is anchored to one or more sessions;
/// the recipient "Groups"/"Students" picks in the UI resolve to the per-session entries below
/// (a group → its sessions; a specific student → their session with that student in StudentIds).
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
    /// The single exam date, applied to every resolved session. Required.
    /// SeparateTime: the exam's own date (today or future). DuringSession: the date whose scheduled
    /// class (per session) the exam is taken in — each targeted session must have a class that day.
    /// </summary>
    public DateTime? ExamDate { get; set; }

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
