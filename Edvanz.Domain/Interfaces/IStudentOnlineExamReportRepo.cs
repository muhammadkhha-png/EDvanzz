using Edvanz.Domain.Entities;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Extended repository for the student-report aggregate
/// (<see cref="StudentOnlineExamReport"/> + <see cref="StudentQuestionAnswer"/> +
/// <see cref="StudentQuestionAnswerOption"/>). Kept as a separate repo from
/// <see cref="IOnlineExamRepo"/> because it is its own aggregate root (§1) —
/// mirrors the <c>ExamHomeworkRepo</c> vs. tracking-view split.
///
/// PHASE NOTE: CRUD-support surface only for Phase 1. §3.4 grading, §3.5 lifecycle,
/// and the "solved?" EXISTS query land in Phase 2/5 against verified call sites.
/// </summary>
public interface IStudentOnlineExamReportRepo : IGenericRepo<StudentOnlineExamReport, long>
{
    /// <summary>Loads a report scoped to exam + student — the row targeted by the UX_...OnlineExamId_TeacherStudentId unique index.</summary>
    Task<StudentOnlineExamReport?> GetByExamAndStudentAsync(long onlineExamId, long teacherStudentId);
    /// <summary>§3.2 — status counts straight from the report table, scope-independent.</summary>
    Task<OnlineExamReportStatusCounts> GetStatusCountsAsync(long onlineExamId);

    /// <summary>
    /// TeacherStudentIds with a report row for this exam — used by the caller to
    /// compute NotAttended as <c>assignedIds.Except(this)</c> (§3.2, do-not-reintroduce #7).
    /// </summary>
    Task<IReadOnlyList<long>> GetReportedStudentIdsAsync(long onlineExamId);

    /// <summary>§3.3 grouped list — status counts for a page of exam ids in one query.</summary>
    Task<IReadOnlyList<OnlineExamListAggregateRow>> GetStatusCountsByExamIdsAsync(IEnumerable<long> onlineExamIds);

    /// <summary>"Solved" = EXISTS(report WHERE OnlineExamId=@id AND SubmittedAt != null) (§6).</summary>
    Task<bool> HasAnySubmittedReportAsync(long onlineExamId);

    /// <summary>Purges a report graph leaf-first — called before <c>IOnlineExamRepo.PurgeExamGraphAsync</c> (§3.7).</summary>
    Task PurgeReportsForExamAsync(long onlineExamId);
    /// <summary>T7 grid — full report rows for an exam, TeacherStudent included (needed for case-3 name display).</summary>
    Task<IReadOnlyList<StudentOnlineExamReport>> GetReportsForExamAsync(long onlineExamId);

    /// <summary>T6 overview — avg/high/low over finalized (SubmittedAt != null) reports only.</summary>
    Task<(decimal Avg, decimal High, decimal Low)> GetPercentageStatsAsync(long onlineExamId);
    /// <summary>S6 review — report with answers + selected options eager-loaded. Null if student hasn't started.</summary>
    Task<StudentOnlineExamReport?> GetReportWithAnswersAsync(long onlineExamId, long teacherStudentId);
    Task<StudentQuestionAnswer?> GetAnswerByReportAndQuestionAsync(long studentReportId, long questionId);
    Task AddAnswerAsync(StudentQuestionAnswer answer);

    /// <summary>Re-answer overwrite (S4): deletes existing selections, inserts the new picks. Leaf-first, no cascade needed since it's one hop.</summary>
    Task ReplaceAnswerOptionsAsync(long studentQuestionAnswerId, IEnumerable<StudentQuestionAnswerOption> newOptions);

    Task<IReadOnlyList<StudentQuestionAnswer>> GetAnswersForReportAsync(long studentReportId);
}