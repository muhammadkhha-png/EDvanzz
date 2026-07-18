using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Edvanz.Application.Dtos.VideoContentManagement;

// ════════════════════════════════════════════════════════════════════════════
// VIDEO CONTENT MANAGEMENT MODULE 14 — STUDENT VIDEO-QUIZ DTOs
// ════════════════════════════════════════════════════════════════════════════
//
// Student-facing quiz flow for a video's exam (VideoExam). Mirrors the online-exam
// student DTOs (take-screen / stats / review) — but the take-screen and review shapes
// deliberately NEVER carry an answer key before finalize (security by shape).
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Take-screen response — the video's quiz plus the student's current attempt state.
/// Every question carries its options with NO <c>isCorrect</c> field on the shape
/// (security by projection, mirrors <c>OnlineExamTakeScreenDto</c>).
/// </summary>
public sealed class VideoExamTakeScreenDto
{
    public long VideoAssetId { get; set; }
    public long VideoExamId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int QuestionsCount { get; set; }

    /// <summary>Total attainable points — one per question.</summary>
    public decimal TotalDegree { get; set; }

    /// <summary>The student's current attempt status, or null if they have never attempted this quiz.</summary>
    public string? Status { get; set; }

    /// <summary>Whether the student may retake — true once the current attempt is finalized (Completed).</summary>
    public bool CanRetake { get; set; }

    /// <summary>The finalized attempt's percentage, or null while in-progress / not attempted.</summary>
    public decimal? LastPercentage { get; set; }

    /// <summary>The finalized attempt's score, or null while in-progress / not attempted.</summary>
    public decimal? LastScore { get; set; }

    public List<VideoExamStudentQuestionDto> Questions { get; set; } = new();
}

/// <summary>One take-screen question — no <c>isCorrect</c> anywhere on this shape.</summary>
public sealed class VideoExamStudentQuestionDto
{
    public long QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VideoExamQuestionType QuestionType { get; set; }

    /// <summary>Points for this question — always 1 (video questions are equally weighted).</summary>
    public decimal Degree { get; set; }

    /// <summary>Gated question-image URL (<c>/api/files/{id}</c>), or null.</summary>
    public string? ImageUrl { get; set; }

    public List<VideoExamStudentOptionDto> Options { get; set; } = new();
}

public sealed class VideoExamStudentOptionDto
{
    public long OptionId { get; set; }
    public string OptionText { get; set; } = string.Empty;
}

/// <summary>One question's answer in a submit payload.</summary>
public sealed class SubmitVideoExamAnswerDto
{
    public long QuestionId { get; set; }
    public List<long> SelectedOptionIds { get; set; } = new();
}

/// <summary>Submit body — any subset of the quiz's questions; unanswered ones score 0.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] // typo'd/stale field → 400, never silently ignored (§7.2b precedent)
public sealed class SubmitVideoExamRequest
{
    [Required]
    public List<SubmitVideoExamAnswerDto> Answers { get; set; } = new();
}

/// <summary>
/// Shared stats shape for submit / result — mirrors <c>OnlineExamStatsDto</c>, plus the
/// raw <c>Score</c>/<c>TotalDegree</c> so the client can render "N / M".
/// </summary>
public sealed class VideoExamStatsDto
{
    public decimal Percentage { get; set; }
    public int Stars { get; set; }
    public int NotAnswered { get; set; }
    public int Correct { get; set; }
    public int Wrong { get; set; }
    public decimal Score { get; set; }
    public decimal TotalDegree { get; set; }

    /// <summary>The attempt status (Completed once submitted).</summary>
    public string? Status { get; set; }
}

/// <summary>Review response — questions + the student's picks; the answer key + awarded degree appear ONLY after finalize.</summary>
public sealed class VideoExamReviewDto
{
    public long VideoAssetId { get; set; }
    public long VideoExamId { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>True once the attempt is submitted — gates <c>IsCorrect</c>/<c>AwardedDegree</c>.</summary>
    public bool Finalized { get; set; }

    public string? Status { get; set; }
    public decimal? Score { get; set; }
    public decimal? Percentage { get; set; }
    public int? Stars { get; set; }

    public List<VideoExamReviewQuestionDto> Questions { get; set; } = new();
}

public sealed class VideoExamReviewQuestionDto
{
    public long QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VideoExamQuestionType QuestionType { get; set; }

    public decimal Degree { get; set; }

    /// <summary>Null until <see cref="VideoExamReviewDto.Finalized"/> is true.</summary>
    public decimal? AwardedDegree { get; set; }

    public string? ImageUrl { get; set; }

    public List<VideoExamReviewOptionDto> Options { get; set; } = new();
}

public sealed class VideoExamReviewOptionDto
{
    public long OptionId { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public bool IsSelected { get; set; }

    /// <summary>Null until <see cref="VideoExamReviewDto.Finalized"/> is true (answer key hidden pre-submit).</summary>
    public bool? IsCorrect { get; set; }
}
