using Edvanz.Domain.Entities;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Extended repository for the student video-quiz attempt aggregate
/// (<see cref="StudentVideoExamReport"/> + <see cref="StudentVideoExamAnswer"/> +
/// <see cref="StudentVideoExamAnswerOption"/>). Its own aggregate root — never loaded
/// through <see cref="VideoAsset"/>'s navigation set. Mirrors
/// <see cref="IStudentOnlineExamReportRepo"/> (video-module twin).
///
/// The report is anchored by <c>(VideoAssetId, TeacherStudentId)</c> — the video, not the
/// exam version — so it survives a teacher's exam replace and supports RETAKE.
/// </summary>
public interface IStudentVideoExamReportRepo : IGenericRepo<StudentVideoExamReport, long>
{
    /// <summary>Loads the student's report for a video — the row targeted by the unique index. Tracked for mutation.</summary>
    Task<StudentVideoExamReport?> GetByVideoAndStudentAsync(long videoAssetId, long teacherStudentId);

    /// <summary>Loads the report with answers + selected options eager-loaded (review/result read). Null if the student never attempted. AsNoTracking.</summary>
    Task<StudentVideoExamReport?> GetReportWithAnswersAsync(long videoAssetId, long teacherStudentId);

    /// <summary>Adds a report root (with any attached answer/option children) to the change tracker. Caller owns SaveChanges.</summary>
    Task AddReportAsync(StudentVideoExamReport report);

    /// <summary>
    /// Wipes a report's answers (options → answers, leaf-first) in the caller's transaction —
    /// used by RETRY (reset to a fresh in-progress attempt) and defensively before a re-submit.
    /// Does not touch the report row itself; caller resets its fields.
    /// </summary>
    Task ClearAnswersForReportAsync(long studentVideoExamReportId);

    /// <summary>
    /// Purges the whole report graph for a video (options → answers → reports, leaf-first) in the
    /// caller's transaction. Called when the teacher REPLACES a video's quiz — prior attempts are
    /// void because the quiz changed. (Video / teacher hard-delete is handled by the CASCADE FK.)
    /// </summary>
    Task PurgeReportsForVideoAsync(long videoAssetId);
}
