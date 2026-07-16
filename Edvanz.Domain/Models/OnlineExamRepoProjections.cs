using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

// ════════════════════════════════════════════════════════════════════════════
// ONLINE EXAM MODULE — REPOSITORY PROJECTION TYPES
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// §3.2 reconciliation snapshot for one exam. <c>Assigned</c> comes from the live
/// scope resolution (never cached); <c>StatusCounts</c> comes straight from the
/// report table; <c>NotAttended</c> is computed by the caller as
/// <c>Assigned.Except(ReportedTeacherStudentIds)</c> — never by joining outward
/// from the assigned set (do-not-reintroduce #7).
/// </summary>
public sealed class OnlineExamReportStatusCounts
{
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Blocked { get; set; }
    public int InProgress { get; set; }
}

/// <summary>One exam's grouped-list row (§3.3 T4/S1) — counts only, no per-student detail.</summary>
public sealed class OnlineExamListAggregateRow
{
    public long OnlineExamId { get; set; }
    public int AssignedCount { get; set; }
    public OnlineExamReportStatusCounts StatusCounts { get; set; } = new();
}

/// <summary>
/// Teacher-facing question+options projection — includes <see cref="OnlineExamQuestionOptionRow.IsCorrect"/>.
/// Used by T12 (questions edit screen) and T10 (questions overview).
/// </summary>
public sealed class OnlineExamQuestionRow
{
    public long Id { get; set; }
    public string QuestionText { get; set; } = null!;
    public OnlineExamQuestionType QuestionType { get; set; }
    public decimal Degree { get; set; }
    public int SortOrder { get; set; }

    /// <summary>Registry image FK (FileObject.Id), or null. Projected by the repo.</summary>
    public long? ImageFileId { get; set; }

    /// <summary>Gated image URL, populated by the service from <see cref="ImageFileId"/> (not by the repo).</summary>
    public string? ImageUrl { get; set; }

    public List<OnlineExamQuestionOptionRow> Options { get; set; } = new();
}

public sealed class OnlineExamQuestionOptionRow
{
    public long Id { get; set; }
    public string OptionText { get; set; } = null!;
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// Student-facing take-screen projection (S2) — <c>IsCorrect</c> does not exist on
/// this type at all. This is the "dedicated projection — security" the plan calls
/// for: omission by shape, not by DTO-mapper discipline (do-not-reintroduce #9).
/// </summary>
public sealed class StudentOnlineExamQuestionRow
{
    public long Id { get; set; }
    public string QuestionText { get; set; } = null!;
    public OnlineExamQuestionType QuestionType { get; set; }
    public decimal Degree { get; set; }
    public int SortOrder { get; set; }

    /// <summary>Registry image FK (FileObject.Id), or null. Projected by the repo.</summary>
    public long? ImageFileId { get; set; }

    /// <summary>Gated image URL, populated by the service from <see cref="ImageFileId"/> (not by the repo).</summary>
    public string? ImageUrl { get; set; }

    public List<StudentOnlineExamQuestionOptionRow> Options { get; set; } = new();
}

public sealed class StudentOnlineExamQuestionOptionRow
{
    public long Id { get; set; }
    public string OptionText { get; set; } = null!;
    public int SortOrder { get; set; }
}
/// <summary>Assigned-student identity row (§3.2/T7 grid) — name+code alongside the id.</summary>
public sealed class AssignedStudentRow
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
}