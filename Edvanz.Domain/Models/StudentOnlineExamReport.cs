using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Own aggregate root — deliberately never loaded through <see cref="OnlineExam"/>
/// (§1, do-not-reintroduce #3). Created LAZILY: on first answer-by-answer write, on
/// bulk submit, or on a manual teacher block (T5s) — never at publish time (§3.6).
///
/// Outcome counts (Passed/Failed/Blocked) are read straight from this table;
/// NotAttended is computed as a set difference against the live-resolved assigned
/// set, never by joining outward from it (§3.2, do-not-reintroduce #7).
/// </summary>
public class StudentOnlineExamReport : BaseEntity
{
    public long OnlineExamId { get; set; }
    public OnlineExam OnlineExam { get; set; } = null!;

    public long TeacherStudentId { get; set; }
    public TeacherStudent TeacherStudent { get; set; } = null!;

    /// <summary>
    /// Denormalized from the owning exam's TeacherId — NOT a composite-FK target here
    /// (unlike <c>OnlineExamScope</c>); this is a standalone tenant-scoped column kept
    /// as one of the two live NoAction chains from Teacher (§2 — see
    /// <c>EdvanzDbContext.OnModelCreating</c> delete-matrix remarks).
    /// </summary>
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>Running/final total. <c>Σ StudentQuestionAnswer.AwardedDegree</c>.</summary>
    public decimal Score { get; set; }

    /// <summary><c>Score / ExamGrade × 100</c>, 0 while ExamGrade would be 0 (§3.4).</summary>
    public decimal Percentage { get; set; }

    public StudentOnlineExamStatus Status { get; set; } = StudentOnlineExamStatus.InProgress;

    /// <summary>
    /// Server-side count of anti-cheat violations (leaving/backgrounding the exam) for this student.
    /// Incremented atomically by the violation endpoint so the tally survives an app kill; when it
    /// reaches the exam's <c>MaxViolations</c> (and <c>BlockOnViolation</c> is on) the report is set
    /// <see cref="StudentOnlineExamStatus.Blocked"/>. Default 0.
    /// </summary>
    public int ViolationCount { get; set; } = 0;

    /// <summary>Null while in-progress. Set on bulk submit (S3) or window-end auto-finalize (§3.5).</summary>
    public DateTime? SubmittedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token — guards the double-submit race (§3.5: "second
    /// submit guarded by unique index + RowVersion").
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    public ICollection<StudentQuestionAnswer> Answers { get; set; } = new List<StudentQuestionAnswer>();
}