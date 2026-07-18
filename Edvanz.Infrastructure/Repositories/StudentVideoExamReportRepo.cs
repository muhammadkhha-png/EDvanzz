using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for the student video-quiz attempt aggregate. Video-module twin of
/// <see cref="StudentOnlineExamReportRepo"/>. Kept separate from <see cref="VideoAssetRepo"/> —
/// its own aggregate root, never joined through the video's navigation set.
/// </summary>
public sealed class StudentVideoExamReportRepo
    : GenericRepo<StudentVideoExamReport, long>, IStudentVideoExamReportRepo
{
    public StudentVideoExamReportRepo(EdvanzDbContext context) : base(context) { }

    /// <inheritdoc />
    public async Task<StudentVideoExamReport?> GetByVideoAndStudentAsync(long videoAssetId, long teacherStudentId)
    {
        // Hits UX_StudentVideoExamReports_Video_Student — O(1) lookup. Tracked for mutation.
        return await _context.StudentVideoExamReports
            .FirstOrDefaultAsync(r => r.VideoAssetId == videoAssetId && r.TeacherStudentId == teacherStudentId);
    }

    /// <inheritdoc />
    public async Task<StudentVideoExamReport?> GetReportWithAnswersAsync(long videoAssetId, long teacherStudentId)
    {
        return await _context.StudentVideoExamReports
            .Where(r => r.VideoAssetId == videoAssetId && r.TeacherStudentId == teacherStudentId)
            .Include(r => r.Answers).ThenInclude(a => a.SelectedOptions)
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task AddReportAsync(StudentVideoExamReport report)
    {
        await _context.StudentVideoExamReports.AddAsync(report);
    }

    /// <inheritdoc />
    public async Task ClearAnswersForReportAsync(long studentVideoExamReportId)
    {
        // Leaf-first: options then answers. ExecuteDeleteAsync — set-based, no in-memory load.
        await _context.StudentVideoExamAnswerOptions
            .Where(o => o.StudentVideoExamAnswer.StudentVideoExamReportId == studentVideoExamReportId)
            .ExecuteDeleteAsync();

        await _context.StudentVideoExamAnswers
            .Where(a => a.StudentVideoExamReportId == studentVideoExamReportId)
            .ExecuteDeleteAsync();
    }

    /// <inheritdoc />
    public async Task PurgeReportsForVideoAsync(long videoAssetId)
    {
        // Leaf-first: options → answers → reports. Called on quiz replace (attempts are void).
        await _context.StudentVideoExamAnswerOptions
            .Where(o => o.StudentVideoExamAnswer.StudentVideoExamReport.VideoAssetId == videoAssetId)
            .ExecuteDeleteAsync();

        await _context.StudentVideoExamAnswers
            .Where(a => a.StudentVideoExamReport.VideoAssetId == videoAssetId)
            .ExecuteDeleteAsync();

        await _context.StudentVideoExamReports
            .Where(r => r.VideoAssetId == videoAssetId)
            .ExecuteDeleteAsync();
    }
}
