using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Edvanz.Domain.Entities;

/// <summary>
/// A student's attempt at a video's quiz (<see cref="VideoExam"/>) — the video-module
/// mirror of <see cref="StudentOnlineExamReport"/>. Own aggregate root; never loaded
/// through <see cref="VideoAsset"/>'s navigation set.
///
/// ANCHOR: keyed by <see cref="VideoAssetId"/> (one live report per student per video —
/// unique index <c>UX_StudentVideoExamReports_Video_Student</c>), NOT by
/// <see cref="VideoExamId"/>. The video's quiz is replaced wholesale on a teacher edit
/// (<c>VideoService.UpdateVideoAsync</c> deletes + recreates the <see cref="VideoExam"/>
/// with fresh ids), so the student flow anchors on the stable video id and treats the
/// video's CURRENT exam as authoritative. <see cref="VideoExamId"/> is a denormalized
/// record of which exam version was attempted (no FK — the old exam may be gone).
///
/// RETAKE: a <see cref="StudentVideoExamStatus.Completed"/> report can be reset back to
/// <see cref="StudentVideoExamStatus.InProgress"/> (answers/score wiped) — the design's
/// "Retry". The latest attempt is the only one kept (video-quiz only; online exams never
/// retake).
///
/// DELETE POSTURE: FK to <see cref="VideoAsset"/> is CASCADE (same posture as
/// <see cref="VideoExam"/> — the exam tree is also CASCADE off VideoAsset), so a video
/// hard-delete / teacher-purge removes the report graph at the DB level with zero
/// service-layer cleanup. The report tree (report → answer → answer-option) is disjoint
/// from the exam tree (exam → question → option) — no table is reachable by two cascade
/// paths, so there is no SQL Server multi-cascade-path conflict. <see cref="TeacherId"/>
/// and <see cref="TeacherStudentId"/> are denormalized tenant columns with NO navigation
/// FK (so student/teacher purge is never blocked by this table).
/// </summary>
public class StudentVideoExamReport : BaseEntity
{
    /// <summary>The video whose quiz was attempted. CASCADE delete root (see class remarks).</summary>
    public long VideoAssetId { get; set; }
    public VideoAsset VideoAsset { get; set; } = null!;

    /// <summary>
    /// Denormalized record of which <see cref="VideoExam"/> version was attempted. No FK —
    /// the exam is replaced (new id) on a teacher edit; this may dangle after a replace.
    /// </summary>
    public long VideoExamId { get; set; }

    /// <summary>Denormalized tenant column (no navigation FK) — the student who attempted.</summary>
    public long TeacherStudentId { get; set; }

    /// <summary>Denormalized tenant column (no navigation FK) — the owning teacher.</summary>
    public long TeacherId { get; set; }

    /// <summary>Running/final total. <c>Σ StudentVideoExamAnswer.AwardedDegree</c> (1 point per question).</summary>
    public decimal Score { get; set; }

    /// <summary><c>Score / TotalQuestions × 100</c>, 0 while there are no questions.</summary>
    public decimal Percentage { get; set; }

    public StudentVideoExamStatus Status { get; set; } = StudentVideoExamStatus.InProgress;

    /// <summary>Null while in-progress; set on submit (finalize). Cleared on retry.</summary>
    public DateTime? SubmittedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token — guards the double-submit race.</summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    public ICollection<StudentVideoExamAnswer> Answers { get; set; } = new List<StudentVideoExamAnswer>();
}
