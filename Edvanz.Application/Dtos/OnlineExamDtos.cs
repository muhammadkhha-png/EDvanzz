using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos;

/// <summary>
/// Input-side scope row — used by the resolver's not-yet-persisted overload
/// (create/append-scope flows, Phase 3). Mirrors <c>VideoScopeInputDto</c> minus
/// the individual-student branch.
/// </summary>
public sealed class OnlineExamScopeInputDto
{
    public OnlineExamScopeType ScopeType { get; set; }
    public long? SessionId { get; set; }
    public long? SessionGroupId { get; set; }
}


public sealed class CreateOnlineExamQuestionOptionDto
{
    public string OptionText { get; set; } = null!;
    public bool IsCorrect { get; set; }
}

public sealed class CreateOnlineExamQuestionDto
{
    public string QuestionText { get; set; } = null!;
    public OnlineExamQuestionType QuestionType { get; set; }
    public decimal Degree { get; set; }
    public List<CreateOnlineExamQuestionOptionDto> Options { get; set; } = new();
}

/// <summary>T1 create body. Questions optional at create time (§4).</summary>
public sealed class CreateOnlineExamRequest
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public decimal PassPercentage { get; set; }
    public bool Visibility { get; set; } = true;
    public List<OnlineExamScopeInputDto> Scopes { get; set; } = new();
    public List<CreateOnlineExamQuestionDto>? Questions { get; set; }
}

/// <summary>T8 edit body — metadata only, no questions. RowVersion required.</summary>
public sealed class UpdateOnlineExamRequest
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public decimal PassPercentage { get; set; }
    public bool Visibility { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

/// <summary>T13 replace-all questions body. Status must be Draft.</summary>
public sealed class ReplaceOnlineExamQuestionsRequest
{
    public List<CreateOnlineExamQuestionDto> Questions { get; set; } = new();
}

/// <summary>T11 status transition body.</summary>
public sealed class UpdateOnlineExamStatusRequest
{
    public OnlineExamStatus Status { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

/// <summary>T9 edit-screen DTO (== create shape) + T1 create response.</summary>
public sealed class OnlineExamDetailDto
{
    public long Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public decimal PassPercentage { get; set; }
    public bool Visibility { get; set; }
    public OnlineExamStatus Status { get; set; }
    public List<OnlineExamScopeDto> Scopes { get; set; } = new();
    public byte[] RowVersion { get; set; } = null!;
}

public sealed class OnlineExamScopeDto
{
    public long Id { get; set; }
    public OnlineExamScopeType ScopeType { get; set; }
    public long? SessionId { get; set; }
    public string? SessionName { get; set; }
    public long? SessionGroupId { get; set; }
    public string? GroupName { get; set; }
    public int AssignedCount { get; set; }   // NEW — per-scope count (§5)
}

/// <summary>T4 list request.</summary>
public sealed class OnlineExamListRequest
{
    public OnlineExamStatus? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class OnlineExamListItemDto
{
    public long Id { get; set; }
    public string Title { get; set; } = null!;
    public OnlineExamStatus Status { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public int AssignedCount { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int BlockedCount { get; set; }
    public int InProgressCount { get; set; }
}

/// <summary>T6 overview.</summary>
public sealed class OnlineExamOverviewDto
{
    public decimal ExamGrade { get; set; }
    public decimal PassGrade { get; set; }
    public int AssignedCount { get; set; }
    public decimal AveragePercentage { get; set; }
    public decimal HighestPercentage { get; set; }
    public decimal LowestPercentage { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int BlockedCount { get; set; }
    public int InProgressCount { get; set; }
    public List<OnlineExamScopeDto> Scopes { get; set; } = new();
}

/// <summary>T7 scope-analysis grid row.</summary>
public sealed class OnlineExamScopeAnalysisRowDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
    public string Status { get; set; } = "NotAttended"; // string: enum name or virtual "NotAttended"
    public decimal? Percentage { get; set; }
    public decimal? Score { get; set; }
    public bool IsOutOfScope { get; set; } // case-3 flag
}

/// <summary>T10 questions overview.</summary>
public sealed class OnlineExamQuestionsOverviewDto
{
    public int SingleChoiceCount { get; set; }
    public int MultipleChoiceCount { get; set; }
    public OnlineExamStatus Status { get; set; }
}

/// <summary>T5s — per-student manual status (Blocked / InProgress only). Teacher endpoint, targets one student's report.</summary>
public sealed class UpdateOnlineExamStudentStatusRequest
{
    public StudentOnlineExamStatus Status { get; set; }
}