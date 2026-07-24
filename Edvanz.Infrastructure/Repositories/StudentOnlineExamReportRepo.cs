using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// Extended repository for the student-report aggregate. Kept separate from
/// <see cref="OnlineExamRepo"/> — own aggregate root (§1), never joined through
/// OnlineExam's navigation set.
/// </summary>
public class StudentOnlineExamReportRepo : GenericRepo<StudentOnlineExamReport, long>, IStudentOnlineExamReportRepo
{
    public StudentOnlineExamReportRepo(EdvanzDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<StudentOnlineExamReport?> GetByExamAndStudentAsync(long onlineExamId, long teacherStudentId)
    {
        // Hits UX_StudentOnlineExamReports_Exam_Student — O(1) lookup.
        return await _context.StudentOnlineExamReports
            .FirstOrDefaultAsync(r => r.OnlineExamId == onlineExamId && r.TeacherStudentId == teacherStudentId);
    }

    /// <inheritdoc />
    public async Task<int> IncrementViolationCountAsync(long reportId)
    {
        // Atomic SET ViolationCount = ViolationCount + 1 — no lost updates under concurrent violations.
        await _context.StudentOnlineExamReports
            .Where(r => r.Id == reportId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ViolationCount, r => r.ViolationCount + 1));

        // Read back the fresh value (scalar projection — untracked, so it reflects the DB, not a stale
        // tracked copy).
        return await _context.StudentOnlineExamReports
            .Where(r => r.Id == reportId)
            .Select(r => r.ViolationCount)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<bool> TryBlockForViolationAsync(long reportId, DateTime now)
    {
        // Block ONLY a still-live report (not submitted, not already Blocked) — set-based so it can't
        // race a concurrent finalize into a 500.
        await _context.StudentOnlineExamReports
            .Where(r => r.Id == reportId
                && r.SubmittedAt == null
                && r.Status != StudentOnlineExamStatus.Blocked)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, StudentOnlineExamStatus.Blocked)
                .SetProperty(r => r.UpdatedAt, (DateTime?)now));

        // True when the report is Blocked now — whether this call blocked it or a concurrent one did;
        // false if it was finalized (submitted) meanwhile.
        return await _context.StudentOnlineExamReports
            .Where(r => r.Id == reportId)
            .Select(r => r.Status == StudentOnlineExamStatus.Blocked)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<OnlineExamReportStatusCounts> GetStatusCountsAsync(long onlineExamId)
    {
        var rows = await _context.StudentOnlineExamReports
            .Where(r => r.OnlineExamId == onlineExamId)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .AsNoTracking()
            .ToListAsync();

        var counts = new OnlineExamReportStatusCounts();
        foreach (var row in rows)
        {
            switch (row.Status)
            {
                case StudentOnlineExamStatus.Passed: counts.Passed = row.Count; break;
                case StudentOnlineExamStatus.Failed: counts.Failed = row.Count; break;
                case StudentOnlineExamStatus.Blocked: counts.Blocked = row.Count; break;
                case StudentOnlineExamStatus.InProgress: counts.InProgress = row.Count; break;
            }
        }
        return counts;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> GetReportedStudentIdsAsync(long onlineExamId)
    {
        return await _context.StudentOnlineExamReports
            .Where(r => r.OnlineExamId == onlineExamId)
            .Select(r => r.TeacherStudentId)
            
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OnlineExamListAggregateRow>> GetStatusCountsByExamIdsAsync(
        IEnumerable<long> onlineExamIds)
    {
        var grouped = await _context.StudentOnlineExamReports
            .Where(r => onlineExamIds.Contains(r.OnlineExamId))
            .GroupBy(r => new { r.OnlineExamId, r.Status })
            .Select(g => new { g.Key.OnlineExamId, g.Key.Status, Count = g.Count() })
            .AsNoTracking()
            .ToListAsync();

        return grouped
            .GroupBy(g => g.OnlineExamId)
            .Select(examGroup =>
            {
                var row = new OnlineExamListAggregateRow { OnlineExamId = examGroup.Key };
                foreach (var g in examGroup)
                {
                    switch (g.Status)
                    {
                        case StudentOnlineExamStatus.Passed: row.StatusCounts.Passed = g.Count; break;
                        case StudentOnlineExamStatus.Failed: row.StatusCounts.Failed = g.Count; break;
                        case StudentOnlineExamStatus.Blocked: row.StatusCounts.Blocked = g.Count; break;
                        case StudentOnlineExamStatus.InProgress: row.StatusCounts.InProgress = g.Count; break;
                    }
                }
                return row;
            })
            .ToList();
        // NOTE: AssignedCount is intentionally left 0 here — it comes from
        // IOnlineExamRepo.BuildAssignedStudentIdsQuery per exam id, zipped in by the
        // service layer (§3.3: "one grouped scope resolution + one grouped report
        // aggregation... zip in memory"). This repo only owns the report half.
    }

    /// <inheritdoc />
    public async Task<bool> HasAnySubmittedReportAsync(long onlineExamId)
    {
        return await _context.StudentOnlineExamReports
            .AnyAsync(r => r.OnlineExamId == onlineExamId && r.SubmittedAt != null);
    }

    /// <inheritdoc />
    public async Task PurgeReportsForExamAsync(long onlineExamId)
    {
        // Leaf-first, called BEFORE IOnlineExamRepo.PurgeExamGraphAsync (§3.7 full order:
        // AnswerOptions → Answers → Reports → QuestionOptions → Questions → Scopes → Exam).
        await _context.StudentQuestionAnswerOptions
            .IgnoreQueryFilters()
            .Where(o => o.StudentQuestionAnswer.StudentReport.OnlineExamId == onlineExamId)
            .ExecuteDeleteAsync();

        await _context.StudentQuestionAnswers
            .IgnoreQueryFilters()
            .Where(a => a.StudentReport.OnlineExamId == onlineExamId)
            .ExecuteDeleteAsync();

        await _context.StudentOnlineExamReports
            .IgnoreQueryFilters()
            .Where(r => r.OnlineExamId == onlineExamId)
            .ExecuteDeleteAsync();
    }
    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentOnlineExamReport>> GetReportsForExamAsync(long onlineExamId)
    {
        return await _context.StudentOnlineExamReports
            .Where(r => r.OnlineExamId == onlineExamId)
            .Include(r => r.TeacherStudent)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<(decimal Avg, decimal High, decimal Low)> GetPercentageStatsAsync(long onlineExamId)
    {
        var percentages = await _context.StudentOnlineExamReports
            .Where(r => r.OnlineExamId == onlineExamId && r.SubmittedAt != null)
            .Select(r => r.Percentage)
            .ToListAsync();

        if (percentages.Count == 0) return (0m, 0m, 0m);
        return (Math.Round(percentages.Average(), 2), percentages.Max(), percentages.Min());
    }
    /// <inheritdoc />
    public async Task<StudentOnlineExamReport?> GetReportWithAnswersAsync(long onlineExamId, long teacherStudentId)
    {
        return await _context.StudentOnlineExamReports
            .Where(r => r.OnlineExamId == onlineExamId && r.TeacherStudentId == teacherStudentId)
            .Include(r => r.Answers).ThenInclude(a => a.SelectedOptions)
            .FirstOrDefaultAsync();
    }
    /// <inheritdoc />
    public async Task<StudentQuestionAnswer?> GetAnswerByReportAndQuestionAsync(long studentReportId, long questionId)
    {
        return await _context.StudentQuestionAnswers
            .Include(a => a.SelectedOptions)
            .FirstOrDefaultAsync(a => a.StudentReportId == studentReportId && a.QuestionId == questionId);
    }

    /// <inheritdoc />
    public async Task AddAnswerAsync(StudentQuestionAnswer answer)
    {
        await _context.StudentQuestionAnswers.AddAsync(answer);
    }

    /// <inheritdoc />
    public async Task ReplaceAnswerOptionsAsync(long studentQuestionAnswerId, IEnumerable<StudentQuestionAnswerOption> newOptions)
    {
        await _context.StudentQuestionAnswerOptions
            .Where(o => o.StudentQuestionAnswerId == studentQuestionAnswerId)
            .ExecuteDeleteAsync();

        await _context.StudentQuestionAnswerOptions.AddRangeAsync(newOptions);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentQuestionAnswer>> GetAnswersForReportAsync(long studentReportId)
    {
        return await _context.StudentQuestionAnswers
            .Where(a => a.StudentReportId == studentReportId)
            .AsNoTracking()
            .ToListAsync();
    }
}