using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// Extended repository for the Exams &amp; Homework Module (Module 6).
/// Centralizes all domain-specific query logic for assignment-related entities.
///
/// ARCHITECTURAL NOTE:
/// Inherits GenericRepo&lt;StudentAssignmentObligation, long&gt; for basic CRUD on the primary
/// (hot) entity. All other entities (AssignmentTemplate, AssignmentScope, AssignmentOccurrence,
/// StudentObligationAuditLog, AssignmentDeletionLog) are accessed via _context directly through
/// named methods — same pattern as PaymentRepo and AttendanceRepo.
///
/// QUERY PATTERNS:
/// - Paged queries use CountAsync + Skip/Take.
/// - Search uses EF.Functions.Like for SQL Server index-friendly case-insensitive matching.
///   Never ToLower().Contains() (forces full scan).
/// - Hot tracking-view paths project directly to TrackingViewRow inside SQL via Select —
///   avoids materializing full StudentAssignmentObligation entities.
/// - Barcode-scan path uses ExecuteUpdateAsync for atomic conditional UPDATE — no
///   DbUpdateConcurrencyException on the &lt; 1-second hot path (design decision 5.3).
/// - All queries include teacherId guard for tenant isolation (REQ-EXH-NFR-004).
/// </summary>
public class ExamHomeworkRepo : GenericRepo<StudentAssignmentObligation, long>, IExamHomeworkRepo
{
    public ExamHomeworkRepo(EdvanzDbContext context) : base(context)
    {
    }

    // ══════════════════════════════════════════════
    // ASSIGNMENT TEMPLATE QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddTemplateAsync(AssignmentTemplate template)
    {
        await _context.AssignmentTemplates.AddAsync(template);
    }

    /// <inheritdoc />
    public async Task UpdateTemplateAsync(AssignmentTemplate template)
    {
        _context.Entry(template).State = EntityState.Modified;
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DeleteTemplateAsync(AssignmentTemplate template)
    {
        // Hard delete per REQ-EXH-037. Cascade configured in fluent API removes
        // scopes, occurrences, and obligations. Audit logs survive (Restrict FK)
        // and must be archived by the service layer before this call.
        _context.AssignmentTemplates.Remove(template);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<AssignmentTemplate?> GetTemplateByIdAndTeacherAsync(long templateId, long teacherId)
    {
        return await _context.AssignmentTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TeacherId == teacherId);
    }

    /// <inheritdoc />
    public async Task<AssignmentTemplate?> GetTemplateWithScopesAsync(long templateId, long teacherId)
    {
        return await _context.AssignmentTemplates
            .Include(t => t.Scopes)
                .ThenInclude(s => s.TeacherStudent)
            .Include(t => t.Scopes)
                .ThenInclude(s => s.Session)
            .Include(t => t.Scopes)
                .ThenInclude(s => s.SessionGroup)
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TeacherId == teacherId);
    }

    /// <inheritdoc />
    public async Task<bool> CanEditRecurrencePatternAsync(long templateId)
    {
        // REQ-EXH-013: Pattern is locked once any obligation has Status != Pending.
        // Returns true if NO non-pending obligations exist for any occurrence of this template.
        bool hasRecordedData = await _context.StudentAssignmentObligations
            .AnyAsync(o => o.Occurrence.TemplateId == templateId
                        && o.Status != ObligationStatus.Pending);

        return !hasRecordedData;
    }

    /// <inheritdoc />
    public IQueryable<AssignmentTemplate> BuildAssignmentOverviewQuery(
        long teacherId,
        string? search = null,
        AssignmentType? assignmentType = null,
        bool? recurrenceFilter = null)
    {
        var query = _context.AssignmentTemplates
            .Where(t => t.TeacherId == teacherId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            query = query.Where(t =>
                EF.Functions.Like(t.Name, pattern)
                || EF.Functions.Like(t.NameAr, pattern));
        }

        if (assignmentType.HasValue)
            query = query.Where(t => t.AssignmentType == assignmentType.Value);

        if (recurrenceFilter.HasValue)
            query = query.Where(t => t.IsRecurring == recurrenceFilter.Value);

        // Default ordering — newest first (REQ-EXH-033 lists "every assignment ever created").
        return query.OrderByDescending(t => t.CreateAt);
    }

    // ══════════════════════════════════════════════
    // ASSIGNMENT SCOPE QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddScopesRangeAsync(IEnumerable<AssignmentScope> scopes)
    {
        await _context.AssignmentScopes.AddRangeAsync(scopes);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssignmentScope>> GetScopesByTemplateAsync(long templateId)
    {
        return await _context.AssignmentScopes
            .Where(s => s.TemplateId == templateId)
            .Include(s => s.TeacherStudent)
            .Include(s => s.Session)
            .Include(s => s.SessionGroup)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task DeleteScopesRangeAsync(IEnumerable<AssignmentScope> scopes)
    {
        _context.AssignmentScopes.RemoveRange(scopes);
        await Task.CompletedTask;
    }

    // ══════════════════════════════════════════════
    // ASSIGNMENT OCCURRENCE QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddOccurrenceAsync(AssignmentOccurrence occurrence)
    {
        await _context.AssignmentOccurrences.AddAsync(occurrence);
    }

    /// <inheritdoc />
    public async Task AddOccurrencesRangeAsync(IEnumerable<AssignmentOccurrence> occurrences)
    {
        await _context.AssignmentOccurrences.AddRangeAsync(occurrences);
    }

    /// <inheritdoc />
    public async Task UpdateOccurrenceAsync(AssignmentOccurrence occurrence)
    {
        _context.Entry(occurrence).State = EntityState.Modified;
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<AssignmentOccurrence?> GetOccurrenceByIdAndTeacherAsync(long occurrenceId, long teacherId)
    {
        return await _context.AssignmentOccurrences
            .FirstOrDefaultAsync(o => o.Id == occurrenceId && o.TeacherId == teacherId);
    }

    /// <inheritdoc />
    public async Task<AssignmentOccurrence?> GetOccurrenceWithTemplateAsync(long occurrenceId, long teacherId)
    {
        return await _context.AssignmentOccurrences
            .Include(o => o.Template)
            .FirstOrDefaultAsync(o => o.Id == occurrenceId && o.TeacherId == teacherId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssignmentOccurrence>> GetOccurrencesByDateRangeAsync(
        long teacherId, DateTime startDate, DateTime endDate)
    {
        // Backed by IX_AssignmentOccurrences_DueDate (Section 7.2 index #7).
        return await _context.AssignmentOccurrences
            .Where(o => o.TeacherId == teacherId
                     && o.DueDate >= startDate.Date
                     && o.DueDate <= endDate.Date)
            .OrderBy(o => o.DueDate)
            .ThenBy(o => o.OccurrenceNumber)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<int> GetHighestOccurrenceNumberAsync(long templateId)
    {
        // Returns 0 if no occurrences exist yet — first occurrence will be number 1.
        var max = await _context.AssignmentOccurrences
            .Where(o => o.TemplateId == templateId)
            .Select(o => (int?)o.OccurrenceNumber)
            .MaxAsync();

        return max ?? 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssignmentTemplate>> GetTemplatesDueForOccurrenceGenerationAsync(
        DateTime asOfDate)
    {
        // Backed by IX_AssignmentTemplates_RecurrenceScheduler (filtered partial index).
        // Service layer decides which dates to materialize based on RecurrencePattern.
        return await _context.AssignmentTemplates
            .Where(t => t.IsRecurring
                     && !t.IsRecurrenceStopped
                     && (t.RecurrenceEndDate == null || t.RecurrenceEndDate >= asOfDate.Date))
            .AsNoTracking()
            .ToListAsync();
    }

    // ══════════════════════════════════════════════
    // OBLIGATION QUERIES — TRACKING VIEW (REQ-EXH-030/031/032)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddObligationsRangeAsync(IEnumerable<StudentAssignmentObligation> obligations)
    {
        await _context.StudentAssignmentObligations.AddRangeAsync(obligations);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> GetExistingObligationStudentIdsAsync(
        long occurrenceId, IEnumerable<long> candidateStudentIds)
    {
        // REQ-EXH-003 / REQ-EXH-035: Service layer calls this before bulk-inserting
        // obligations to filter out students who already have one for this occurrence.
        // The unique index UX_StudentAssignmentObligations_Occurrence_Student is the
        // safety net behind this check.
        var ids = candidateStudentIds.ToList();
        return await _context.StudentAssignmentObligations
            .Where(o => o.OccurrenceId == occurrenceId && ids.Contains(o.TeacherStudentId)).AsNoTracking()
            .Select(o => o.TeacherStudentId)
            .ToListAsync();
    }

    /// <inheritdoc />
    /// Hot-path query — projects directly to TrackingViewRow without materializing
    /// the full obligation graph. Backed by IX_StudentAssignmentObligations_Tracking
    /// (covering index, Section 7.2 index #1). Drives REQ-EXH-NFR-001 (&lt; 2 seconds).
    public async Task<(IReadOnlyList<TrackingViewRow> Items, int TotalCount)> GetTrackingViewPagedAsync(
        long teacherId, long occurrenceId,
        string? search,
        ObligationStatus? statusFilter,
        bool? missingEntries,
        decimal? gradeAboveThreshold,
        decimal? gradeBelowThreshold,
        bool? belowPassingGrade,
        int page, int pageSize)
    {
        // Look up the passing-threshold snapshot once so the IsBelowPassing flag
        // can be computed inside SQL.
        decimal? passingThreshold = await _context.AssignmentOccurrences
            .Where(o => o.Id == occurrenceId && o.TeacherId == teacherId)
            .Select(o => o.PassingThresholdSnapshot)
            .FirstOrDefaultAsync();

        var query = _context.StudentAssignmentObligations
            .Where(o => o.TeacherId == teacherId && o.OccurrenceId == occurrenceId);

        // REQ-EXH-031 — search by name or code.
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            query = query.Where(o =>
                EF.Functions.Like(o.TeacherStudent.StudentName, pattern)
                || EF.Functions.Like(o.TeacherStudent.StudentCode, pattern));
        }

        // REQ-EXH-031 filter 1 — by completion / attendance status.
        if (statusFilter.HasValue)
            query = query.Where(o => o.Status == statusFilter.Value);

        // REQ-EXH-031 filter 2 — students who have not submitted homework or did not attend.
        if (missingEntries == true)
        {
            query = query.Where(o =>
                o.Status == ObligationStatus.NotDone
                || o.Status == ObligationStatus.DidNotAttend
                || o.Status == ObligationStatus.Pending);
        }

        // REQ-EXH-031 filter 3 — grades above a specified value (exam only).
        if (gradeAboveThreshold.HasValue)
            query = query.Where(o => o.GradeValue.HasValue && o.GradeValue.Value > gradeAboveThreshold.Value);

        // REQ-EXH-031 filter 4 — grades below a specified value (exam only).
        if (gradeBelowThreshold.HasValue)
            query = query.Where(o => o.GradeValue.HasValue && o.GradeValue.Value < gradeBelowThreshold.Value);

        // REQ-EXH-031 filter 5 — students who scored below the passing threshold.
        if (belowPassingGrade == true && passingThreshold.HasValue)
        {
            query = query.Where(o => o.GradeValue.HasValue && o.GradeValue.Value < passingThreshold.Value);
        }

        int totalCount = await query.CountAsync();

        // Project to TrackingViewRow inside SQL — avoids materializing entities.
        var items = await query
            .OrderBy(o => o.TeacherStudent.StudentName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new TrackingViewRow
            {
                ObligationId = o.Id,
                TeacherStudentId = o.TeacherStudentId,
                StudentName = o.TeacherStudent.StudentName,
                StudentCode = o.TeacherStudent.StudentCode,
                Status = o.Status,
                GradeValue = o.GradeValue,
                IsGradeEntered = o.IsGradeEntered,
                IsBelowPassing = passingThreshold.HasValue
                                  && o.GradeValue.HasValue
                                  && o.GradeValue.Value < passingThreshold.Value,
                MarkedByScan = o.MarkedByScan,
                ScannedAt = o.ScannedAt,
                UpdatedAt = o.UpdatedAt,
            })
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    /// REQ-EXH-026-A: Backed by IX_StudentAssignmentObligations_PendingGrades filtered index
    /// (Status IN (3, 6) — Attended and DoneWithoutGrade). The filtered index makes this
    /// query touch only grade-pending rows.
    public async Task<(IReadOnlyList<TrackingViewRow> Items, int TotalCount)> GetGradeEntryViewPagedAsync(
        long teacherId, long occurrenceId,
        string? search,
        int page, int pageSize)
    {
        decimal? passingThreshold = await _context.AssignmentOccurrences
            .Where(o => o.Id == occurrenceId && o.TeacherId == teacherId)
            .Select(o => o.PassingThresholdSnapshot)
            .FirstOrDefaultAsync();

        var query = _context.StudentAssignmentObligations
            .Where(o => o.TeacherId == teacherId
                     && o.OccurrenceId == occurrenceId
                     && (o.Status == ObligationStatus.Attended
                      || o.Status == ObligationStatus.DoneWithoutGrade));

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            query = query.Where(o =>
                EF.Functions.Like(o.TeacherStudent.StudentName, pattern)
                || EF.Functions.Like(o.TeacherStudent.StudentCode, pattern));
        }

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(o => o.TeacherStudent.StudentName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new TrackingViewRow
            {
                ObligationId = o.Id,
                TeacherStudentId = o.TeacherStudentId,
                StudentName = o.TeacherStudent.StudentName,
                StudentCode = o.TeacherStudent.StudentCode,
                Status = o.Status,
                GradeValue = o.GradeValue,
                IsGradeEntered = o.IsGradeEntered,
                IsBelowPassing = false, // Grade pending — not applicable.
                MarkedByScan = o.MarkedByScan,
                ScannedAt = o.ScannedAt,
                UpdatedAt = o.UpdatedAt,
            })
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    // ══════════════════════════════════════════════
    // OBLIGATION QUERIES — STUDENT HISTORY (REQ-EXH-040)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<(IReadOnlyList<StudentAssignmentObligation> Items, int TotalCount)>
        GetStudentHistoryPagedAsync(
            long teacherId, long teacherStudentId,
            DateTime? startDate, DateTime? endDate,
            AssignmentType? assignmentType,
            int page, int pageSize)
    {
        // Backed by IX_StudentAssignmentObligations_StudentHistory (Section 7.2 index #2).
        var query = _context.StudentAssignmentObligations
            .Where(o => o.TeacherId == teacherId && o.TeacherStudentId == teacherStudentId)
            .Include(o => o.Occurrence)
                .ThenInclude(occ => occ.Template)
            .AsQueryable();

        if (startDate.HasValue)
            query = query.Where(o => o.Occurrence.DueDate >= startDate.Value.Date);
        if (endDate.HasValue)
            query = query.Where(o => o.Occurrence.DueDate <= endDate.Value.Date);
        if (assignmentType.HasValue)
            query = query.Where(o => o.Occurrence.Template.AssignmentType == assignmentType.Value);

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(o => o.CreateAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<(int Completed, int TotalAssigned)> GetHomeworkCompletionStatsAsync(
        long teacherId, long teacherStudentId)
    {
        // REQ-EXH-040 — single GroupBy projection, evaluated entirely in SQL.
        var stats = await _context.StudentAssignmentObligations
            .Where(o => o.TeacherId == teacherId
                     && o.TeacherStudentId == teacherStudentId
                     && o.Occurrence.Template.AssignmentType == AssignmentType.Homework)
            .GroupBy(o => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Completed = g.Count(o => o.Status == ObligationStatus.Done
                                      || o.Status == ObligationStatus.DoneWithGrade
                                      || o.Status == ObligationStatus.DoneWithoutGrade)
            })
            .FirstOrDefaultAsync();

        return stats == null ? (0, 0) : (stats.Completed, stats.Total);
    }

    /// <inheritdoc />
    public async Task<(int Attended, int TotalAssigned)> GetExamAttendanceStatsAsync(
        long teacherId, long teacherStudentId)
    {
        var stats = await _context.StudentAssignmentObligations
            .Where(o => o.TeacherId == teacherId
                     && o.TeacherStudentId == teacherStudentId
                     && o.Occurrence.Template.AssignmentType == AssignmentType.Exam)
            .GroupBy(o => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Attended = g.Count(o => o.Status == ObligationStatus.Attended
                                     || o.Status == ObligationStatus.AttendedWithGrade)
            })
            .FirstOrDefaultAsync();

        return stats == null ? (0, 0) : (stats.Attended, stats.Total);
    }

    // ══════════════════════════════════════════════
    // OBLIGATION QUERIES — REPORTS (REQ-EXH-039 through 046)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentAssignmentObligation>> GetObligationsByOccurrenceAsync(
        long teacherId, long occurrenceId)
    {
        // REQ-EXH-039 — Single Assignment Report.
        return await _context.StudentAssignmentObligations
            .Where(o => o.TeacherId == teacherId && o.OccurrenceId == occurrenceId)
            .Include(o => o.TeacherStudent)
            .OrderBy(o => o.TeacherStudent.StudentName)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<AbsenceReportRow> Items, int TotalCount)>
        GetHomeworkAbsenceReportPagedAsync(
            long teacherId,
            long? sessionId, long? sessionGroupId, long? specificTemplateId,
            int page, int pageSize)
    {
        return await BuildAbsenceReportAsync(
            teacherId, AssignmentType.Homework,
            new[] { ObligationStatus.NotDone },
            sessionId, sessionGroupId, specificTemplateId,
            page, pageSize);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<AbsenceReportRow> Items, int TotalCount)>
        GetExamAbsenceReportPagedAsync(
            long teacherId,
            long? sessionId, long? sessionGroupId, long? specificTemplateId,
            int page, int pageSize)
    {
        return await BuildAbsenceReportAsync(
            teacherId, AssignmentType.Exam,
            new[] { ObligationStatus.DidNotAttend },
            sessionId, sessionGroupId, specificTemplateId,
            page, pageSize);
    }

    /// <summary>
    /// Shared implementation for REQ-EXH-041 and REQ-EXH-042. Backed by
    /// IX_StudentAssignmentObligations_Absence filtered index (Status IN (2, 5)).
    /// </summary>
    private async Task<(IReadOnlyList<AbsenceReportRow> Items, int TotalCount)> BuildAbsenceReportAsync(
        long teacherId,
        AssignmentType assignmentType,
        ObligationStatus[] absenceStatuses,
        long? sessionId, long? sessionGroupId, long? specificTemplateId,
        int page, int pageSize)
    {
        // First — filter obligations to the absence states scoped by tenant and assignment type.
        var absentObligations = _context.StudentAssignmentObligations
            .Where(o => o.TeacherId == teacherId
                     && absenceStatuses.Contains(o.Status)
                     && o.Occurrence.Template.AssignmentType == assignmentType);

        if (specificTemplateId.HasValue)
            absentObligations = absentObligations.Where(o => o.Occurrence.TemplateId == specificTemplateId.Value);

        if (sessionId.HasValue)
        {
            absentObligations = absentObligations.Where(o =>
                o.TeacherStudent.SessionId == sessionId.Value);
        }

        if (sessionGroupId.HasValue)
        {
            absentObligations = absentObligations.Where(o =>
                o.TeacherStudent.Session != null
                && o.TeacherStudent.Session.SessionGroupId == sessionGroupId.Value);
        }

        // Group by student — each row is one student with their list of missed assignments.
        var grouped = absentObligations
            .GroupBy(o => new
            {
                o.TeacherStudentId,
                o.TeacherStudent.StudentName,
                o.TeacherStudent.StudentCode,
                o.TeacherStudent.SessionId,
                SessionName = o.TeacherStudent.Session != null ? o.TeacherStudent.Session.SessionName : null,
            })
            .Select(g => new
            {
                g.Key,
                MissedNames = g.Select(o => o.Occurrence.Template.Name).ToList(),
                TotalMissed = g.Count(),
            });

        int totalCount = await grouped.CountAsync();

        var paged = await grouped
            .OrderByDescending(g => g.TotalMissed)
            .ThenBy(g => g.Key.StudentName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = paged.Select(p => new AbsenceReportRow
        {
            TeacherStudentId = p.Key.TeacherStudentId,
            StudentName = p.Key.StudentName,
            StudentCode = p.Key.StudentCode,
            SessionId = p.Key.SessionId,
            SessionName = p.Key.SessionName,
            MissedAssignmentNames = p.MissedNames,
            TotalMissedCount = p.TotalMissed,
        }).ToList();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<ExamGradeAnalysis> GetExamGradeAnalysisAsync(long teacherId, long occurrenceId)
    {
        // Single round trip: fetch the occurrence's snapshots and aggregate stats together.
        var occurrence = await _context.AssignmentOccurrences
            .Where(o => o.Id == occurrenceId && o.TeacherId == teacherId)
            .Select(o => new
            {
                o.Id,
                o.MaxGradeSnapshot,
                o.PassingThresholdSnapshot,
            })
            .FirstOrDefaultAsync();

        if (occurrence == null)
        {
            return new ExamGradeAnalysis { OccurrenceId = occurrenceId };
        }

        var threshold = occurrence.PassingThresholdSnapshot;

        // REQ-EXH-043 — only consider attended students with a grade entered.
        var attendedWithGrade = _context.StudentAssignmentObligations
            .Where(o => o.OccurrenceId == occurrenceId
                     && o.TeacherId == teacherId
                     && o.Status == ObligationStatus.AttendedWithGrade
                     && o.GradeValue.HasValue);

        var stats = await attendedWithGrade
            .GroupBy(o => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Highest = g.Max(o => o.GradeValue),
                Lowest = g.Min(o => o.GradeValue),
                Average = g.Average(o => o.GradeValue),
                AbovePassing = threshold.HasValue
                    ? g.Count(o => o.GradeValue!.Value >= threshold.Value)
                    : 0,
                BelowPassing = threshold.HasValue
                    ? g.Count(o => o.GradeValue!.Value < threshold.Value)
                    : 0,
            })
            .FirstOrDefaultAsync();

        return new ExamGradeAnalysis
        {
            OccurrenceId = occurrenceId,
            MaxGradeSnapshot = occurrence.MaxGradeSnapshot,
            PassingThresholdSnapshot = occurrence.PassingThresholdSnapshot,
            AttendedStudentsCount = stats?.Count ?? 0,
            HighestGrade = stats?.Highest,
            LowestGrade = stats?.Lowest,
            AverageGrade = stats?.Average,
            AbovePassingCount = stats?.AbovePassing ?? 0,
            BelowPassingCount = stats?.BelowPassing ?? 0,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentAssignmentObligation>> GetBelowPassingGradeAsync(
        long teacherId,
        long? occurrenceId, long? sessionId, long? sessionGroupId)
    {
        // REQ-EXH-044 — Below Passing Grade Report. Comparison against the snapshotted
        // PassingThreshold on the occurrence row (NOT the live template, per REQ-EXH-043
        // historical reproducibility).
        var query = _context.StudentAssignmentObligations
            .Where(o => o.TeacherId == teacherId
                     && o.Status == ObligationStatus.AttendedWithGrade
                     && o.GradeValue.HasValue
                     && o.Occurrence.PassingThresholdSnapshot.HasValue
                     && o.GradeValue.Value < o.Occurrence.PassingThresholdSnapshot.Value);

        if (occurrenceId.HasValue)
            query = query.Where(o => o.OccurrenceId == occurrenceId.Value);

        if (sessionId.HasValue)
            query = query.Where(o => o.TeacherStudent.SessionId == sessionId.Value);

        if (sessionGroupId.HasValue)
        {
            query = query.Where(o =>
                o.TeacherStudent.Session != null
                && o.TeacherStudent.Session.SessionGroupId == sessionGroupId.Value);
        }

        return await query
            .Include(o => o.TeacherStudent)
            .Include(o => o.Occurrence)
                .ThenInclude(occ => occ.Template)
            .OrderBy(o => o.GradeValue)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentAssignmentObligation>> GetAboveGradeThresholdAsync(
        long teacherId, long occurrenceId, decimal threshold)
    {
        // REQ-EXH-045 — Above Grade Threshold Report (tutor-defined threshold, not the passing one).
        return await _context.StudentAssignmentObligations
            .Where(o => o.TeacherId == teacherId
                     && o.OccurrenceId == occurrenceId
                     && o.Status == ObligationStatus.AttendedWithGrade
                     && o.GradeValue.HasValue
                     && o.GradeValue.Value > threshold)
            .Include(o => o.TeacherStudent)
            .OrderByDescending(o => o.GradeValue)
            .AsNoTracking()
            .ToListAsync();
    }

    // ══════════════════════════════════════════════
    // OBLIGATION QUERIES — IDEMPOTENT BARCODE SCAN (REQ-EXH-026, NFR-002)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    /// Atomic conditional UPDATE — design decision 5.3.
    /// One round-trip; no SELECT-then-UPDATE; no DbUpdateConcurrencyException on duplicate scan.
    /// Resolves the obligation by (TeacherId, OccurrenceId, TeacherStudentId), which hits
    /// the unique index UX_StudentAssignmentObligations_Occurrence_Student.
    /// Returns rows affected — 0 means another scan won the race (idempotent success).
    public async Task<int> TryMarkScannedAsync(
        long teacherId, long occurrenceId, long teacherStudentId,
        ObligationStatus newStatus, DateTime scannedAt, long scannedByUserId)
    {
        return await _context.StudentAssignmentObligations
            .Where(o => o.TeacherId == teacherId
                     && o.OccurrenceId == occurrenceId
                     && o.TeacherStudentId == teacherStudentId
                     && o.Status == ObligationStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, newStatus)
                .SetProperty(o => o.MarkedByScan, true)
                .SetProperty(o => o.ScannedAt, scannedAt)
                .SetProperty(o => o.LastUpdatedByUserId, (long?)scannedByUserId)
                .SetProperty(o => o.UpdatedAt, DateTime.UtcNow));
    }

    // ══════════════════════════════════════════════
    // OBLIGATION QUERIES — SINGLE-ROW LOOKUPS
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<StudentAssignmentObligation?> GetObligationByIdAndTeacherAsync(
        long obligationId, long teacherId)
    {
        // Tracked: caller (manual grade-entry) modifies and saves with RowVersion check.
        return await _context.StudentAssignmentObligations
            .FirstOrDefaultAsync(o => o.Id == obligationId && o.TeacherId == teacherId);
    }

    /// <inheritdoc />
    public async Task<StudentAssignmentObligation?> GetObligationByOccurrenceAndStudentAsync(
        long occurrenceId, long teacherStudentId)
    {
        // Hits UX_StudentAssignmentObligations_Occurrence_Student — O(1) lookup.
        return await _context.StudentAssignmentObligations
            .FirstOrDefaultAsync(o => o.OccurrenceId == occurrenceId
                                   && o.TeacherStudentId == teacherStudentId);
    }

    /// <inheritdoc />
    public async Task DeleteObligationAsync(StudentAssignmentObligation obligation)
    {
        _context.StudentAssignmentObligations.Remove(obligation);
        await Task.CompletedTask;
    }

    // ══════════════════════════════════════════════
    // AUDIT LOG QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddAuditLogAsync(StudentObligationAuditLog auditLog)
    {
        await _context.StudentObligationAuditLogs.AddAsync(auditLog);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentObligationAuditLog>> GetAuditHistoryByObligationAsync(
        long obligationId)
    {
        // Backed by IX_StudentObligationAuditLogs_Obligation_ChangedAt.
        return await _context.StudentObligationAuditLogs
            .Where(a => a.StudentObligationId == obligationId)
            .Include(a => a.ChangedByUser)
            .OrderByDescending(a => a.ChangedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    // ══════════════════════════════════════════════
    // DELETION LOG QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task AddDeletionLogAsync(AssignmentDeletionLog log)
    {
        await _context.AssignmentDeletionLogs.AddAsync(log);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<AssignmentDeletionLog> Items, int TotalCount)>
        GetDeletionLogsPagedAsync(
            long teacherId, DateTime? startDate, DateTime? endDate, int page, int pageSize)
    {
        var query = _context.AssignmentDeletionLogs
            .Where(d => d.TeacherId == teacherId);

        if (startDate.HasValue)
            query = query.Where(d => d.DeletedAt >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(d => d.DeletedAt <= endDate.Value.Date.AddDays(1));

        int totalCount = await query.CountAsync();

        var items = await query
            .Include(d => d.DeletedByUser)
            .OrderByDescending(d => d.DeletedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }


    // For documentation only; do not actually create this class. Copy the methods
    // below into the existing ExamHomeworkRepo class.

    private readonly EdvanzDbContext _context = null!;

 

  
    // For documentation only. Do not actually create a class. Copy the methods below
    // into the existing ExamHomeworkRepo class. The `_context` references resolve
    // correctly when these methods live alongside the existing ones.

    // ══════════════════════════════════════════════
    // SCOPE COUNTS PER TEMPLATE
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public  async Task<Dictionary<long, ScopeCountAggregate>> GetScopeCountsByTemplateIdsAsync(
        IEnumerable<long> templateIds)
    {
        var idList = templateIds.ToList();
        if (idList.Count == 0) return new Dictionary<long, ScopeCountAggregate>();

        var rows = await _context.AssignmentScopes
            .Where(s => idList.Contains(s.TemplateId))
            .GroupBy(s => s.TemplateId)
            .Select(g => new ScopeCountAggregate
            {
                TemplateId = g.Key,
                IndividualCount = g.Count(s => s.ScopeType == AssignmentScopeType.IndividualStudent),
                SessionCount = g.Count(s => s.ScopeType == AssignmentScopeType.Session),
                GroupCount = g.Count(s => s.ScopeType == AssignmentScopeType.SessionGroup),
            })
            .AsNoTracking()
            .ToListAsync();

        return rows.ToDictionary(r => r.TemplateId);
    }




    // ══════════════════════════════════════════════
    // ASSIGNMENT OVERVIEW LIST — replaces BuildAssignmentOverviewQuery
    // ══════════════════════════════════════════════

    /// <inheritdoc />
   public async Task<(IReadOnlyList<AssignmentTemplate> Items, int TotalCount)>
        GetAssignmentOverviewPagedAsync(
            long teacherId,
            string? search,
            AssignmentType? assignmentType,
            RecurrencePattern? recurrencePattern,
            bool? isRecurring,
            int page,
            int pageSize)
    {
        // Backed by IX_AssignmentTemplates_TeacherList (Section 7.2 covering index).
        var query = _context.AssignmentTemplates
            .Where(t => t.TeacherId == teacherId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            query = query.Where(t =>
                EF.Functions.Like(t.Name, pattern)
             || EF.Functions.Like(t.NameAr, pattern));
        }

        if (assignmentType.HasValue)
            query = query.Where(t => t.AssignmentType == assignmentType.Value);

        if (recurrencePattern.HasValue)
            query = query.Where(t => t.RecurrencePattern == recurrencePattern.Value);

        if (isRecurring.HasValue)
            query = query.Where(t => t.IsRecurring == isRecurring.Value);

        int totalCount = await query.CountAsync();

        // Newest first (REQ-EXH-033 lists "every assignment ever created").
        var items = await query
            .OrderByDescending(t => t.CreateAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }




    // ══════════════════════════════════════════════
    // OVERVIEW & OCCURRENCE AGGREGATES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Dictionary<long, long>> GetLatestOccurrenceIdsByTemplateAsync(
        IEnumerable<long> templateIds)
    {
        var idList = templateIds.ToList();
        if (idList.Count == 0) return new Dictionary<long, long>();

        // Single query: GROUP BY templateId, take the row with MAX(OccurrenceNumber).
        var rows = await _context.AssignmentOccurrences
            .Where(o => idList.Contains(o.TemplateId))
            .GroupBy(o => o.TemplateId)
            .Select(g => new
            {
                TemplateId = g.Key,
                LatestOccurrenceId = g.OrderByDescending(o => o.OccurrenceNumber)
                                      .Select(o => o.Id)
                                      .First()
            })
            .AsNoTracking()
            .ToListAsync();

        return rows.ToDictionary(r => r.TemplateId, r => r.LatestOccurrenceId);
    }

    /// <inheritdoc />
    public async Task<Dictionary<long, OccurrenceCompletionSummary>>
        GetCompletionSummariesByOccurrenceIdsAsync(IEnumerable<long> occurrenceIds)
    {
        var idList = occurrenceIds.ToList();
        if (idList.Count == 0) return new Dictionary<long, OccurrenceCompletionSummary>();

        var rows = await _context.StudentAssignmentObligations
            .Where(o => idList.Contains(o.OccurrenceId))
            .GroupBy(o => o.OccurrenceId)
            .Select(g => new OccurrenceCompletionSummary
            {
                OccurrenceId = g.Key,
                TotalStudents = g.Count(),
                DoneOrAttended = g.Count(o =>
                    o.Status == ObligationStatus.Done
                 || o.Status == ObligationStatus.Attended
                 || o.Status == ObligationStatus.AttendedWithGrade
                 || o.Status == ObligationStatus.DoneWithoutGrade
                 || o.Status == ObligationStatus.DoneWithGrade),
                NotDoneOrAbsent = g.Count(o =>
                    o.Status == ObligationStatus.NotDone
                 || o.Status == ObligationStatus.DidNotAttend),
                Pending = g.Count(o => o.Status == ObligationStatus.Pending),
            })
            .AsNoTracking()
            .ToListAsync();

        return rows.ToDictionary(r => r.OccurrenceId);
    }

    /// <inheritdoc />
    public async Task<Dictionary<long, DateTime?>> GetNextOrLastOccurrenceDatesAsync(
        IEnumerable<long> templateIds, DateTime today)
    {
        var idList = templateIds.ToList();
        if (idList.Count == 0) return new Dictionary<long, DateTime?>();

        DateTime cutoff = today.Date;

        var nextRows = await _context.AssignmentOccurrences
            .Where(o => idList.Contains(o.TemplateId) && o.DueDate >= cutoff)
            .GroupBy(o => o.TemplateId)
            .Select(g => new { TemplateId = g.Key, Date = g.Min(o => (DateTime?)o.DueDate) })
            .AsNoTracking()
            .ToListAsync();

        var lastRows = await _context.AssignmentOccurrences
            .Where(o => idList.Contains(o.TemplateId) && o.DueDate < cutoff)
            .GroupBy(o => o.TemplateId)
            .Select(g => new { TemplateId = g.Key, Date = g.Max(o => (DateTime?)o.DueDate) })
            .AsNoTracking()
            .ToListAsync();

        var nextDict = nextRows.ToDictionary(r => r.TemplateId, r => r.Date);
        var lastDict = lastRows.ToDictionary(r => r.TemplateId, r => r.Date);

        return idList.ToDictionary(
            id => id,
            id => nextDict.GetValueOrDefault(id) ?? lastDict.GetValueOrDefault(id));
    }

    // ══════════════════════════════════════════════
    // OCCURRENCE PAGINATION
    // ══════════════════════════════════════════════

    /// <inheritdoc />
   public  async Task<(IReadOnlyList<AssignmentOccurrence> Items, int TotalCount)>
        GetOccurrencesByTemplatePagedAsync(long teacherId, long templateId, int page, int pageSize)
    {
        var query = _context.AssignmentOccurrences
            .Where(o => o.TeacherId == teacherId && o.TemplateId == templateId);

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(o => o.OccurrenceNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<AssignmentOccurrence?> GetFirstOccurrenceAsync(long templateId, long teacherId)
    {
        // Tracked: caller (UpdateTemplate) edits DueDate.
        return await _context.AssignmentOccurrences
            .FirstOrDefaultAsync(o => o.TemplateId == templateId
                                   && o.TeacherId == teacherId
                                   && o.OccurrenceNumber == 1);
    }

    /// <inheritdoc />
    public async Task<AssignmentOccurrence?> GetLatestOccurrenceAsync(long templateId, long teacherId)
    {
        return await _context.AssignmentOccurrences
            .Where(o => o.TemplateId == templateId && o.TeacherId == teacherId)
            .OrderByDescending(o => o.OccurrenceNumber)
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    // ══════════════════════════════════════════════
    // DELETION-TIME AGGREGATES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<int> CountStudentsWithRecordedDataAsync(long templateId)
    {
        return await _context.StudentAssignmentObligations
            .Where(o => o.Occurrence.TemplateId == templateId
                     && (o.Status != ObligationStatus.Pending || o.IsGradeEntered))
            .Select(o => o.TeacherStudentId)
            .Distinct()
            .CountAsync();
    }

    /// <inheritdoc />
    public async Task<int> CountOccurrencesByTemplateAsync(long templateId)
    {
        return await _context.AssignmentOccurrences
            .CountAsync(o => o.TemplateId == templateId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentObligationAuditLog>> GetAuditLogsForTemplateAsync(
        long templateId)
    {
        return await _context.StudentObligationAuditLogs
            .Where(a => a.StudentObligation.Occurrence.TemplateId == templateId)
            .OrderBy(a => a.ChangedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAuditLogsForTemplateAsync(long templateId)
    {
        // Single SQL DELETE — avoids materializing rows. Runs inside the ambient
        // transaction opened by the caller (EF Core 7+ shares the connection's
        // current transaction with ExecuteDeleteAsync).
        await _context.StudentObligationAuditLogs
            .Where(a => a.StudentObligation.Occurrence.TemplateId == templateId)
            .ExecuteDeleteAsync();
    }

    // ══════════════════════════════════════════════
    // CONCURRENCY HELPER
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public void SetTemplateOriginalRowVersion(AssignmentTemplate template, byte[] rowVersion)
    {
        // Set the original RowVersion bytes on the tracked entity so EF generates the
        // correct WHERE [RowVersion] = @original clause on save. If the entity is not
        // tracked (e.g., loaded via AsNoTracking), Attach it first.
        var entry = _context.Entry(template);
        if (entry.State == EntityState.Detached)
        {
            _context.Attach(template);
            entry.State = EntityState.Modified;
        }
        entry.OriginalValues["RowVersion"] = rowVersion;
    }


    /// <inheritdoc />
    public async Task UpdateObligationAsync(StudentAssignmentObligation obligation)
    {
        _context.Entry(obligation).State = EntityState.Modified;
        await Task.CompletedTask;
    }

    public void SetObligationOriginalRowVersion(
             StudentAssignmentObligation obligation, byte[] rowVersion)
    {
        var entry = _context.Entry(obligation);
        if (entry.State == EntityState.Detached)
        {
            _context.Attach(obligation);
            entry.State = EntityState.Modified;
        }
        entry.OriginalValues["RowVersion"] = rowVersion;
    }

    public async Task<IReadOnlyList<StudentAssignmentObligation>> GetObligationsByIdsAsync(
            long teacherId, long occurrenceId, IEnumerable<long> obligationIds)
    {
        var idList = obligationIds.ToList();
        if (idList.Count == 0) return Array.Empty<StudentAssignmentObligation>();

        // Tracked: callers (BulkUpdateStatusAsync) mutate Status/Grade in place.
        return await _context.StudentAssignmentObligations
            .Where(o => o.TeacherId == teacherId
                     && o.OccurrenceId == occurrenceId
                     && idList.Contains(o.Id))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<StudentPickerRow>> SearchStudentsInOccurrenceAsync(
            long teacherId, long occurrenceId, string query, int limit)
    {
        string trimmed = query?.Trim() ?? string.Empty;
        string pattern = $"%{trimmed}%";

        // Backed by IX_StudentAssignmentObligations_Tracking (TeacherId, OccurrenceId).
        return await _context.StudentAssignmentObligations
            .Where(o => o.TeacherId == teacherId
                     && o.OccurrenceId == occurrenceId
                     && (string.IsNullOrEmpty(trimmed)
                         || EF.Functions.Like(o.TeacherStudent.StudentName, pattern)
                         || EF.Functions.Like(o.TeacherStudent.StudentCode, pattern)))
            .OrderBy(o => o.TeacherStudent.StudentName)
            .Take(limit)
            .Select(o => new StudentPickerRow
            {
                ObligationId = o.Id,
                TeacherStudentId = o.TeacherStudentId,
                StudentName = o.TeacherStudent.StudentName,
                StudentCode = o.TeacherStudent.StudentCode,
                CurrentStatus = o.Status,
            })
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<EligibleStudentRow> Items, int TotalCount)>
            GetEligibleStudentsForTemplatePagedAsync(
                long teacherId, long templateId, long? sessionId,
                string? search, int page, int pageSize)
    {
        // ── Step 1: Compute the set of student ids ALREADY in the template's scope ──
        // Union of three buckets: individual scope rows, students in scoped sessions,
        // students in sessions belonging to scoped session-groups. We compute these
        // as IQueryables to keep the work in SQL.

        var individualScopeIds = _context.AssignmentScopes
            .Where(s => s.TemplateId == templateId
                     && s.ScopeType == AssignmentScopeType.IndividualStudent
                     && s.TeacherStudentId.HasValue)
            .Select(s => s.TeacherStudentId!.Value);

        var sessionScopeStudentIds = _context.AssignmentScopes
            .Where(s => s.TemplateId == templateId
                     && s.ScopeType == AssignmentScopeType.Session
                     && s.SessionId.HasValue)
            .SelectMany(s => _context.TeacherStudents
                .Where(ts => ts.SessionId == s.SessionId && !ts.IsDeleted)
                .Select(ts => ts.Id));

        var groupScopeStudentIds = _context.AssignmentScopes
            .Where(s => s.TemplateId == templateId
                     && s.ScopeType == AssignmentScopeType.SessionGroup
                     && s.SessionGroupId.HasValue)
            .SelectMany(s => _context.TeacherStudents
                .Where(ts => !ts.IsDeleted
                          && ts.Session != null
                          && ts.Session.SessionGroupId == s.SessionGroupId)
                .Select(ts => ts.Id));

        // ── Step 2: candidates = teacher's active students MINUS the included set ──
        var includedIds = individualScopeIds
            .Concat(sessionScopeStudentIds)
            .Concat(groupScopeStudentIds);

        var query = _context.TeacherStudents
            .Where(ts => ts.TeacherId == teacherId && !ts.IsDeleted)
            .Where(ts => !includedIds.Contains(ts.Id));

        if (sessionId.HasValue)
            query = query.Where(ts => ts.SessionId == sessionId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            query = query.Where(ts =>
                EF.Functions.Like(ts.StudentName, pattern)
             || EF.Functions.Like(ts.StudentCode, pattern));
        }

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(ts => ts.StudentName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ts => new EligibleStudentRow
            {
                TeacherStudentId = ts.Id,
                StudentName = ts.StudentName,
                StudentCode = ts.StudentCode,
                SessionName = ts.Session != null ? ts.Session.SessionName : null,
            })
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<StudentAssignmentObligation>> GetFutureObligationsForStudentAsync(
            long teacherId, long templateId, long teacherStudentId, DateTime asOfDate)
    {
        DateTime cutoff = asOfDate.Date;
        // Tracked: caller deletes these rows.
        return await _context.StudentAssignmentObligations
            .Where(o => o.TeacherId == teacherId
                     && o.TeacherStudentId == teacherStudentId
                     && o.Occurrence.TemplateId == templateId
                     && o.Occurrence.DueDate >= cutoff)
            .Include(o => o.Occurrence)
            .ToListAsync();
    }

    /// <inheritdoc />
  public  async Task<int> CountStudentObligationsWithDataAsync(
        long teacherId, long templateId, long teacherStudentId)
    {
        return await _context.StudentAssignmentObligations
            .Where(o => o.TeacherId == teacherId
                     && o.TeacherStudentId == teacherStudentId
                     && o.Occurrence.TemplateId == templateId
                     && (o.Status != ObligationStatus.Pending || o.IsGradeEntered))
            .CountAsync();
    }

    /// <inheritdoc />
    public async Task<AssignmentScope?> GetScopeByIdAndTeacherAsync(long scopeId, long teacherId)
    {
        return await _context.AssignmentScopes
            .FirstOrDefaultAsync(s => s.Id == scopeId && s.TeacherId == teacherId);
    }

    /// <inheritdoc />
    public async Task DeleteScopeAsync(AssignmentScope scope)
    {
        _context.AssignmentScopes.Remove(scope);
        await Task.CompletedTask;
    }
    
   
}
