using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;

namespace Edvanz.Application.Dtos;

/// <summary>S1 per-exam row. StudentDegree/StudentStatus null when not yet attempted (upcoming).</summary>
public sealed class OnlineExamStudentListItemDto
{
    public long ExamId { get; set; }
    public string ExamName { get; set; } = string.Empty;
    public DateOnly ExamDate { get; set; }
    public TimeOnly ExamTime { get; set; }
    public TimeSpan Duration { get; set; }
    public int QuestionsCount { get; set; }
    public decimal ExamDegree { get; set; }
    public decimal? StudentDegree { get; set; }
    public string? StudentStatus { get; set; }
}

/// <summary>S1 response — split per QD.</summary>
public sealed class StudentOnlineExamListDto
{
    public List<OnlineExamStudentListItemDto> Upcoming { get; set; } = new();
    public List<OnlineExamStudentListItemDto> Past { get; set; } = new();
}

/// <summary>S2 take-screen response — no IsCorrect anywhere on this shape (security by projection, do-not-reintroduce #9).</summary>
public sealed class OnlineExamTakeScreenDto
{
    public long ExamId { get; set; }
    public string ExamName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public decimal ExamDegree { get; set; }
    public List<StudentOnlineExamQuestionRow> Questions { get; set; } = new();
}

public sealed class OnlineExamReviewOptionDto
{
    public long OptionId { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
    /// <summary>Null until Finalized=true (do-not-reintroduce #9).</summary>
    public bool? IsCorrect { get; set; }
}

public sealed class OnlineExamReviewQuestionDto
{
    public long QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public OnlineExamQuestionType QuestionType { get; set; }
    public decimal Degree { get; set; }
    /// <summary>Null until Finalized=true.</summary>
    public decimal? AwardedDegree { get; set; }

    /// <summary>Gated question-image URL, or null — same URL the take screen showed.</summary>
    public string? ImageUrl { get; set; }

    public List<OnlineExamReviewOptionDto> Options { get; set; } = new();
}

/// <summary>S6 response.</summary>
public sealed class OnlineExamReviewDto
{
    public long ExamId { get; set; }
    public string ExamName { get; set; } = string.Empty;
    public bool Finalized { get; set; }
    public string? ReportStatus { get; set; }
    public decimal? Score { get; set; }
    public decimal? Percentage { get; set; }
    public List<OnlineExamReviewQuestionDto> Questions { get; set; } = new();
}
public sealed class SubmitOnlineExamAnswerRequest
{
    public long QuestionId { get; set; }
    public List<long> SelectedOptionIds { get; set; } = new();
}

/// <summary>S3 bulk body — any subset of questions; unanswered ones stay NotAnswered.</summary>
public sealed class SubmitOnlineExamRequest
{
    public List<SubmitOnlineExamAnswerRequest> Answers { get; set; } = new();
}

/// <summary>Shared stats shape — S3/S4/S5 and T5s (Phase 6).</summary>
public sealed class OnlineExamStatsDto
{
    public decimal Percentage { get; set; }
    public int Stars { get; set; }
    public int NotAnswered { get; set; }
    public int Correct { get; set; }
    public int Wrong { get; set; }
}