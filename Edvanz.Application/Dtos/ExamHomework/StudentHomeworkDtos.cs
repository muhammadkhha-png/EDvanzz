using System.Text.Json.Serialization;
using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.ExamHomework;

/// <summary>
/// Student-facing projection for one homework occurrence (Module 6, AssignmentType.Homework).
/// Mirrors StudentOfflineExamListItemDto's shape, adapted for homework's simpler status model
/// (completion-based, optionally graded — no leaderboard rank, unlike exams).
/// </summary>
public sealed class StudentHomeworkListItemDto
{
    public long HomeworkId { get; set; }
    public string HomeworkName { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly DueDate { get; set; }
    public string? Subject { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ObligationStatus Status { get; set; }

    /// <summary>CompletionOnly or Graded — tells the client whether to expect Grade/MaxGrade at all.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HomeworkTrackingMode? TrackingMode { get; set; }

    /// <summary>Null unless TrackingMode is Graded AND a grade has been entered.</summary>
    public decimal? Grade { get; set; }

    /// <summary>Null for CompletionOnly homework.</summary>
    public decimal? MaxGrade { get; set; }
}