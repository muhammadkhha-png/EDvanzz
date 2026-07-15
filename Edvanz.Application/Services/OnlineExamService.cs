using Edvanz.Application.Dtos;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

public class OnlineExamService : IOnlineExamService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOnlineExamScopeResolver _scopeResolver;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly IOnlineExamGradingService _grading;

    public OnlineExamService(
        IUnitOfWork unitOfWork, IOnlineExamScopeResolver scopeResolver, IStringLocalizer<Messages> localizer, IOnlineExamGradingService grading)
    {
        _unitOfWork = unitOfWork;
        _scopeResolver = scopeResolver;
        _localizer = localizer;
        _grading = grading;

    }

    // ══════════════════════════════════════════════════════════════════════
    // T1 — CREATE
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<OnlineExamDetailDto>> CreateAsync(
        long teacherId, long actingUserId, CreateOnlineExamRequest request)
    {
        var validation = await ValidateCreateOrUpdateAsync(
            teacherId, request.Title, request.StartDateTime,
            request.EndDateTime, request.PassPercentage, request.Scopes);
        if (validation is not null)
            return Result<OnlineExamDetailDto>.Failure(_localizer, validation, HttpStatusCode.BadRequest);

        List<OnlineExamQuestion>? questionEntities = null;
        if (request.Questions is { Count: > 0 })
        {
            var qValidation = ValidateQuestions(request.Questions);
            if (qValidation is not null)
                return Result<OnlineExamDetailDto>.Failure(_localizer, qValidation, HttpStatusCode.BadRequest);

            questionEntities = BuildQuestionEntities(request.Questions);
        }

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync();

        try
        {
            var utcNow = DateTime.UtcNow;
            var exam = new OnlineExam
            {
                TeacherId = teacherId,
                Title = request.Title.Trim(),
                Description = request.Description,
                Instructions = request.Instructions,
                StartDateTime = request.StartDateTime,
                EndDateTime = request.EndDateTime,
                PassPercentage = request.PassPercentage,
                Visibility = request.Visibility,
                Status = OnlineExamStatus.Draft,
                CreatedByUserId = actingUserId,
                CreateAt = utcNow,
            };

            await _unitOfWork.OnlineExamsRepo.AddAsync(exam);
            await _unitOfWork.SaveChangesAsync(); // need exam.Id for children

            if (questionEntities is not null)
            {
                foreach (var q in questionEntities) q.OnlineExamId = exam.Id;
                await _unitOfWork.OnlineExamsRepo.AddQuestionsRangeAsync(questionEntities);
            }

            var scopeEntities = request.Scopes.Select(s => new OnlineExamScope
            {
                OnlineExamId = exam.Id,
                TeacherId = teacherId,
                ScopeType = s.ScopeType,
                SessionId = s.SessionId,
                SessionGroupId = s.SessionGroupId,
                AssignedByUserId = actingUserId,
                AssignedAt = utcNow,
                CreateAt = utcNow,
            }).ToList();
            await _unitOfWork.OnlineExamsRepo.AddScopesRangeAsync(scopeEntities);

            await _unitOfWork.SaveChangesAsync();
            if (ownsTransaction) await _unitOfWork.CommitAsync();

            var dto = await MapToDetailDtoAsync(exam);
            return Result<OnlineExamDetailDto>.Success(dto, _localizer, OnlineExamConstants.Messages.Created, HttpStatusCode.Created);
        }
        catch
        {
            if (ownsTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // T8 — UPDATE (metadata only, no questions)
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<OnlineExamDetailDto>> UpdateAsync(
        long teacherId, long actingUserId, long onlineExamId, UpdateOnlineExamRequest request)
    {
        var exam = await _unitOfWork.OnlineExamsRepo.GetByIdAndTeacherAsync(onlineExamId, teacherId);
        if (exam is null)
            return Result<OnlineExamDetailDto>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.NotFound);

        // "not solved for scope" guard (§4 T8) — block metadata edits once any student
        // has submitted, mirrors "Solved" EXISTS definition (§6).
        if (await _unitOfWork.StudentOnlineExamReportsRepo.HasAnySubmittedReportAsync(onlineExamId))
            return Result<OnlineExamDetailDto>.Failure(_localizer, OnlineExamConstants.Messages.ExamNotDraft, HttpStatusCode.Conflict);

        var validation = await ValidateCreateOrUpdateAsync(
            teacherId, request.Title, request.StartDateTime,
            request.EndDateTime, request.PassPercentage, scopes: null);
        if (validation is not null)
            return Result<OnlineExamDetailDto>.Failure(_localizer, validation, HttpStatusCode.BadRequest);

        if (!exam.RowVersion.SequenceEqual(request.RowVersion))
            return Result<OnlineExamDetailDto>.Failure(_localizer, OnlineExamConstants.Messages.ConcurrencyConflict, HttpStatusCode.Conflict);

        exam.Title = request.Title.Trim();
        exam.Description = request.Description;
        exam.Instructions = request.Instructions;
        exam.StartDateTime = request.StartDateTime;
        exam.EndDateTime = request.EndDateTime;
        exam.PassPercentage = request.PassPercentage;
        exam.Visibility = request.Visibility;
        exam.UpdatedByUserId = actingUserId;
        exam.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.OnlineExamsRepo.UpdateAsync(exam);

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Result<OnlineExamDetailDto>.Failure(_localizer, OnlineExamConstants.Messages.ConcurrencyConflict, HttpStatusCode.Conflict);
        }

        var dto = await MapToDetailDtoAsync(exam);
        return Result<OnlineExamDetailDto>.Success(dto, _localizer, OnlineExamConstants.Messages.Updated);
    }

    // ══════════════════════════════════════════════════════════════════════
    // T9 — GET (edit-screen DTO)
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<OnlineExamDetailDto>> GetByIdAsync(long teacherId, long onlineExamId)
    {
        var exam = await _unitOfWork.OnlineExamsRepo.GetByIdAndTeacherAsync(onlineExamId, teacherId);
        if (exam is null)
            return Result<OnlineExamDetailDto>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.NotFound);

        return Result<OnlineExamDetailDto>.Success(await MapToDetailDtoAsync(exam), _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // T4 — LIST (§3.3: 2 grouped queries, not 2N)
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<PaginatedResponse<List<OnlineExamListItemDto>>>> GetListAsync(
        long teacherId, OnlineExamListRequest request)
    {
        int page = request.Page < 1 ? 1 : request.Page;
        int pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (exams, totalCount) = await _unitOfWork.OnlineExamsRepo
            .GetPagedForTeacherAsync(teacherId, request.Status, request.Search, page, pageSize);

        var examIds = exams.Select(e => e.Id).ToList();

        // Exactly 2 grouped queries for the whole page — no per-row calls.
        var assignedCounts = await _unitOfWork.OnlineExamsRepo.GetAssignedCountsByExamIdsAsync(examIds, teacherId);
        var statusAggregates = await _unitOfWork.StudentOnlineExamReportsRepo.GetStatusCountsByExamIdsAsync(examIds);
        var statusByExam = statusAggregates.ToDictionary(a => a.OnlineExamId);

        var items = exams.Select(e =>
        {
            statusByExam.TryGetValue(e.Id, out var agg);
            assignedCounts.TryGetValue(e.Id, out var assigned);

            return new OnlineExamListItemDto
            {
                Id = e.Id,
                Title = e.Title,
                Status = e.Status,
                StartDateTime = e.StartDateTime,
                EndDateTime = e.EndDateTime,
                AssignedCount = assigned,
                PassedCount = agg?.StatusCounts.Passed ?? 0,
                FailedCount = agg?.StatusCounts.Failed ?? 0,
                BlockedCount = agg?.StatusCounts.Blocked ?? 0,
                InProgressCount = agg?.StatusCounts.InProgress ?? 0,
            };
        }).ToList();

        var response = new PaginatedResponse<List<OnlineExamListItemDto>>
        {
            data = items,
            page = page,
            pageSize = pageSize,
            totalCount = totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
        };

        return Result<PaginatedResponse<List<OnlineExamListItemDto>>>.Success(response, _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // T6 — OVERVIEW
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<OnlineExamOverviewDto>> GetOverviewAsync(long teacherId, long onlineExamId)
    {
        var exam = await _unitOfWork.OnlineExamsRepo.GetByIdAndTeacherAsync(onlineExamId, teacherId);
        if (exam is null)
            return Result<OnlineExamOverviewDto>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.NotFound);

        decimal examGrade = await _unitOfWork.OnlineExamsRepo.GetTotalDegreeAsync(onlineExamId);
        int assignedCount = await _unitOfWork.OnlineExamsRepo
            .BuildAssignedStudentIdsQuery(onlineExamId, teacherId).Distinct().CountAsync();
        var (avg, high, low) = await _unitOfWork.StudentOnlineExamReportsRepo.GetPercentageStatsAsync(onlineExamId);
        var statusCounts = await _unitOfWork.StudentOnlineExamReportsRepo.GetStatusCountsAsync(onlineExamId);
        var scopes = await _unitOfWork.OnlineExamsRepo.GetScopesByExamIdsAsync(new[] { onlineExamId });

        // §5 — one extra grouped query for all per-scope counts, not one per scope row.
        var perScopeCounts = await _unitOfWork.OnlineExamsRepo.GetAssignedCountsPerScopeAsync(onlineExamId, teacherId);

        var dto = new OnlineExamOverviewDto
        {
            ExamGrade = examGrade,
            PassGrade = Math.Round(examGrade * exam.PassPercentage / 100m, 2),
            AssignedCount = assignedCount,
            AveragePercentage = avg,
            HighestPercentage = high,
            LowestPercentage = low,
            PassedCount = statusCounts.Passed,
            FailedCount = statusCounts.Failed,
            BlockedCount = statusCounts.Blocked,
            InProgressCount = statusCounts.InProgress,
            Scopes = scopes.Select(s => MapScopeDto(s, perScopeCounts)).ToList(),
        };

        return Result<OnlineExamOverviewDto>.Success(dto, _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // T7 — SCOPE ANALYSIS GRID (§3.2 reconciliation, case-3 flagged)
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<List<OnlineExamScopeAnalysisRowDto>>> GetScopeAnalysisAsync(long teacherId, long onlineExamId)
    {
        var exam = await _unitOfWork.OnlineExamsRepo.GetByIdAndTeacherAsync(onlineExamId, teacherId);
        if (exam is null)
            return Result<List<OnlineExamScopeAnalysisRowDto>>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.NotFound);

        var assigned = await _unitOfWork.OnlineExamsRepo.GetAssignedStudentsAsync(onlineExamId, teacherId);
        var reports = await _unitOfWork.StudentOnlineExamReportsRepo.GetReportsForExamAsync(onlineExamId);
        var reportsByStudent = reports.ToDictionary(r => r.TeacherStudentId);
        var assignedIds = assigned.Select(a => a.TeacherStudentId).ToHashSet();

        var rows = new List<OnlineExamScopeAnalysisRowDto>();

        // Assigned-without-row ⇒ NotAttended (virtual). Assigned-with-row ⇒ real status.
        foreach (var student in assigned)
        {
            reportsByStudent.TryGetValue(student.TeacherStudentId, out var report);
            rows.Add(new OnlineExamScopeAnalysisRowDto
            {
                TeacherStudentId = student.TeacherStudentId,
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                Status = report?.Status.ToString() ?? "NotAttended",
                Percentage = report?.Percentage,
                Score = report?.Score,
                IsOutOfScope = false,
            });
        }

        // Report-without-assignment (case-3) ⇒ show, flagged.
        foreach (var report in reports.Where(r => !assignedIds.Contains(r.TeacherStudentId)))
        {
            rows.Add(new OnlineExamScopeAnalysisRowDto
            {
                TeacherStudentId = report.TeacherStudentId,
                StudentName = report.TeacherStudent?.StudentName ?? string.Empty,
                StudentCode = report.TeacherStudent?.StudentCode,
                Status = report.Status.ToString(),
                Percentage = report.Percentage,
                Score = report.Score,
                IsOutOfScope = true,
            });
        }

        return Result<List<OnlineExamScopeAnalysisRowDto>>.Success(rows, _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // T12 — GET QUESTIONS (edit) / T10 — QUESTIONS OVERVIEW
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<List<Domain.Interfaces.OnlineExamQuestionRow>>> GetQuestionsAsync(long teacherId, long onlineExamId)
    {
        var exam = await _unitOfWork.OnlineExamsRepo.GetByIdAndTeacherAsync(onlineExamId, teacherId);
        if (exam is null)
            return Result<List<Domain.Interfaces.OnlineExamQuestionRow>>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.NotFound);

        var rows = await _unitOfWork.OnlineExamsRepo.GetQuestionsForTeacherAsync(onlineExamId);
        return Result<List<Domain.Interfaces.OnlineExamQuestionRow>>.Success(rows.ToList(), _localizer);
    }

    public async Task<Result<OnlineExamQuestionsOverviewDto>> GetQuestionsOverviewAsync(long teacherId, long onlineExamId)
    {
        var exam = await _unitOfWork.OnlineExamsRepo.GetByIdAndTeacherAsync(onlineExamId, teacherId);
        if (exam is null)
            return Result<OnlineExamQuestionsOverviewDto>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.NotFound);

        var questions = await _unitOfWork.OnlineExamsRepo.GetQuestionsForTeacherAsync(onlineExamId);

        var dto = new OnlineExamQuestionsOverviewDto
        {
            SingleChoiceCount = questions.Count(q => q.QuestionType == OnlineExamQuestionType.SingleChoice),
            MultipleChoiceCount = questions.Count(q => q.QuestionType == OnlineExamQuestionType.MultipleChoice),
            Status = exam.Status,
        };
        return Result<OnlineExamQuestionsOverviewDto>.Success(dto, _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // T2 — ADD SINGLE QUESTION / T3 — ADD BULK (Draft-only)
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<bool>> AddQuestionAsync(long teacherId, long onlineExamId, CreateOnlineExamQuestionDto request)
        => await AddQuestionsBulkAsync(teacherId, onlineExamId, new List<CreateOnlineExamQuestionDto> { request });

    public async Task<Result<bool>> AddQuestionsBulkAsync(
        long teacherId, long onlineExamId, List<CreateOnlineExamQuestionDto> request)
    {
        var draftCheck = await GetDraftExamOrFailureAsync(teacherId, onlineExamId);
        if (draftCheck.Failure is not null) return draftCheck.Failure;

        var qValidation = ValidateQuestions(request);
        if (qValidation is not null)
            return Result<bool>.Failure(_localizer, qValidation, HttpStatusCode.BadRequest);

        var entities = BuildQuestionEntities(request);
        foreach (var q in entities) q.OnlineExamId = onlineExamId;

        await _unitOfWork.OnlineExamsRepo.AddQuestionsRangeAsync(entities);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, OnlineExamConstants.Messages.Updated);
    }

    // ══════════════════════════════════════════════════════════════════════
    // T13 — REPLACE QUESTIONS (edit, Draft-only)
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<bool>> ReplaceQuestionsAsync(
        long teacherId, long onlineExamId, ReplaceOnlineExamQuestionsRequest request)
    {
        var draftCheck = await GetDraftExamOrFailureAsync(teacherId, onlineExamId);
        if (draftCheck.Failure is not null) return draftCheck.Failure;

        if (request.Questions.Count == 0)
            return Result<bool>.Failure(_localizer, OnlineExamConstants.Messages.NoQuestionsProvided, HttpStatusCode.BadRequest);

        var qValidation = ValidateQuestions(request.Questions);
        if (qValidation is not null)
            return Result<bool>.Failure(_localizer, qValidation, HttpStatusCode.BadRequest);

        var entities = BuildQuestionEntities(request.Questions);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _unitOfWork.OnlineExamsRepo.ReplaceQuestionsAsync(onlineExamId, entities);
            await _unitOfWork.SaveChangesAsync();
            if (ownsTransaction) await _unitOfWork.CommitAsync();
        }
        catch
        {
            if (ownsTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }

        return Result<bool>.Success(true, _localizer, OnlineExamConstants.Messages.Updated);
    }

    public async Task<Result<bool>> UpdateStatusAsync(
     long teacherId, long onlineExamId, UpdateOnlineExamStatusRequest request)
    {
        var exam = await _unitOfWork.OnlineExamsRepo.GetByIdAndTeacherAsync(onlineExamId, teacherId);
        if (exam is null)
            return Result<bool>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.NotFound);

        if (!exam.RowVersion.SequenceEqual(request.RowVersion))
            return Result<bool>.Failure(_localizer, OnlineExamConstants.Messages.ConcurrencyConflict, HttpStatusCode.Conflict);

        // Both sides of this tuple are OnlineExamStatus — exam.Status (entity) and
        // request.Status (UpdateOnlineExamStatusRequest.Status, OnlineExamStatus).
        // Do NOT confuse this with UpdateOnlineExamStudentStatusRequest.Status,
        // which is StudentOnlineExamStatus (T5s, different method entirely).
        bool validTransition = (exam.Status, request.Status) switch
        {
            (OnlineExamStatus.Draft, OnlineExamStatus.Published) => true,
            (OnlineExamStatus.Published, OnlineExamStatus.Draft) => true,
            (OnlineExamStatus.Published, OnlineExamStatus.Closed) => true,
            _ => false,
        };
        if (!validTransition)
            return Result<bool>.Failure(_localizer, OnlineExamConstants.Messages.InvalidStatusTransition, HttpStatusCode.BadRequest);

        // §2 — Published → Draft only when nobody has submitted yet. Same "Solved" guard as T8.
        if (exam.Status == OnlineExamStatus.Published && request.Status == OnlineExamStatus.Draft
            && await _unitOfWork.StudentOnlineExamReportsRepo.HasAnySubmittedReportAsync(onlineExamId))
        {
            return Result<bool>.Failure(_localizer, OnlineExamConstants.Messages.UnpublishBlockedBySubmissions, HttpStatusCode.Conflict);
        }

        if (request.Status == OnlineExamStatus.Published)
        {
            int questionCount = (await _unitOfWork.OnlineExamsRepo.GetQuestionsForTeacherAsync(onlineExamId)).Count;
            int scopeCount = (await _unitOfWork.OnlineExamsRepo.GetScopesByExamIdsAsync(new[] { onlineExamId })).Count;
            decimal totalDegree = await _unitOfWork.OnlineExamsRepo.GetTotalDegreeAsync(onlineExamId);

            if (questionCount == 0 || scopeCount == 0 || totalDegree <= 0)
                return Result<bool>.Failure(_localizer, OnlineExamConstants.Messages.PublishRequiresQuestionAndScope, HttpStatusCode.BadRequest);
        }

        exam.Status = request.Status;
        exam.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.OnlineExamsRepo.UpdateAsync(exam);

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Result<bool>.Failure(_localizer, OnlineExamConstants.Messages.ConcurrencyConflict, HttpStatusCode.Conflict);
        }

        // No cast — request.Status is already OnlineExamStatus, matches the switch arms directly.
        string key = request.Status switch
        {
            OnlineExamStatus.Published => OnlineExamConstants.Messages.Published,
            OnlineExamStatus.Closed => OnlineExamConstants.Messages.Closed,
            _ => OnlineExamConstants.Messages.Updated,
        };
        return Result<bool>.Success(true, _localizer, key);
    }

    // ══════════════════════════════════════════════════════════════════════
    // T14 — DELETE (§3.7 leaf-first)
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<bool>> DeleteAsync(long teacherId, long onlineExamId)
    {
        var exam = await _unitOfWork.OnlineExamsRepo.GetByIdAndTeacherAsync(onlineExamId, teacherId);
        if (exam is null)
            return Result<bool>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.NotFound);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Report graph first, then exam graph — full §3.7 order.
            await _unitOfWork.StudentOnlineExamReportsRepo.PurgeReportsForExamAsync(onlineExamId);
            await _unitOfWork.OnlineExamsRepo.PurgeExamGraphAsync(onlineExamId);

            await _unitOfWork.SaveChangesAsync();
            if (ownsTransaction) await _unitOfWork.CommitAsync();
        }
        catch
        {
            if (ownsTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }

        return Result<bool>.Success(true, _localizer, OnlineExamConstants.Messages.Deleted, HttpStatusCode.NoContent);
    }

    // ══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════

    private async Task<(OnlineExam? Exam, Result<bool>? Failure)> GetDraftExamOrFailureAsync(long teacherId, long onlineExamId)
    {
        var exam = await _unitOfWork.OnlineExamsRepo.GetByIdAndTeacherAsync(onlineExamId, teacherId);
        if (exam is null)
            return (null, Result<bool>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.NotFound));

        if (exam.Status != OnlineExamStatus.Draft)
            return (null, Result<bool>.Failure(_localizer, OnlineExamConstants.Messages.QuestionsEditableOnlyInDraft, HttpStatusCode.Conflict));

        return (exam, null);
    }

    private async Task<string?> ValidateCreateOrUpdateAsync(
        long teacherId, string title, DateTime start, DateTime end,
        decimal passPercentage, List<OnlineExamScopeInputDto>? scopes)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 250)
            return OnlineExamConstants.Messages.TitleRequired;

        if (start >= end)
            return OnlineExamConstants.Messages.StartAfterEnd;

        if (passPercentage is < 0 or > 100)
            return OnlineExamConstants.Messages.PassPercentageOutOfRange;

        if (scopes is not null)
        {
            if (scopes.Count == 0)
                return OnlineExamConstants.Messages.ScopeCannotBeEmpty;

            if (scopes.Select(s => s.ScopeType).Distinct().Count() > 1)
                return OnlineExamConstants.Messages.MixedScopeTypesNotAllowed;

            foreach (var s in scopes)
            {
                long targetId = s.ScopeType == OnlineExamScopeType.Session ? s.SessionId ?? 0 : s.SessionGroupId ?? 0;
                if (!await _unitOfWork.OnlineExamsRepo.IsScopeTargetOwnedByTeacherAsync(teacherId, s.ScopeType, targetId))
                    return OnlineExamConstants.Messages.ScopeTargetNotOwned;
            }
        }

        return null;
    }

    private string? ValidateQuestions(List<CreateOnlineExamQuestionDto> questions)
    {
        foreach (var q in questions)
        {
            if (q.Degree <= 0)
                return OnlineExamConstants.Messages.DegreeMustBePositive;

            if (q.Options.Count < 2)
                return OnlineExamConstants.Messages.QuestionNeedsAtLeastTwoOptions;

            int correctCount = q.Options.Count(o => o.IsCorrect);

            if (q.QuestionType == OnlineExamQuestionType.SingleChoice && correctCount != 1)
                return OnlineExamConstants.Messages.SingleChoiceNeedsExactlyOneCorrect;

            if (q.QuestionType == OnlineExamQuestionType.MultipleChoice && correctCount < 1)
                return OnlineExamConstants.Messages.MultipleChoiceNeedsAtLeastOneCorrect;
        }
        return null;
    }

    private static List<OnlineExamQuestion> BuildQuestionEntities(List<CreateOnlineExamQuestionDto> dtos)
    {
        var utcNow = DateTime.UtcNow;
        return dtos.Select((q, i) => new OnlineExamQuestion
        {
            QuestionText = q.QuestionText.Trim(),
            QuestionType = q.QuestionType,
            Degree = q.Degree,
            SortOrder = i,
            CreateAt = utcNow,
            Options = q.Options.Select((o, oi) => new OnlineExamQuestionOption
            {
                OptionText = o.OptionText.Trim(),
                IsCorrect = o.IsCorrect,
                SortOrder = oi,
                CreateAt = utcNow,
            }).ToList()
        }).ToList();
    }

    private async Task<OnlineExamDetailDto> MapToDetailDtoAsync(OnlineExam exam)
    {
        var scopes = await _unitOfWork.OnlineExamsRepo.GetScopesByExamIdsAsync(new[] { exam.Id });

        return new OnlineExamDetailDto
        {
            Id = exam.Id,
            Title = exam.Title,
            Description = exam.Description,
            Instructions = exam.Instructions,
            StartDateTime = exam.StartDateTime,
            EndDateTime = exam.EndDateTime,
            PassPercentage = exam.PassPercentage,
            Visibility = exam.Visibility,
            Status = exam.Status,
            Scopes = scopes.Select(s => MapScopeDto(s)).ToList(),
            RowVersion = exam.RowVersion,
        };
    }

    private static OnlineExamScopeDto MapScopeDto(OnlineExamScope s, Dictionary<long, int>? perScopeCounts = null) => new()
    {
        Id = s.Id,
        ScopeType = s.ScopeType,
        SessionId = s.SessionId,
        SessionName = s.Session?.SessionName,
        SessionGroupId = s.SessionGroupId,
        GroupName = s.SessionGroup?.GroupName,
        AssignedCount = perScopeCounts is not null && perScopeCounts.TryGetValue(s.Id, out var c) ? c : 0,
    };
   
    // ══════════════════════════════════════════════════════════════════════
    // T5s — MANUAL STATUS (block/unblock; lazy-creates report; returns stats)
    // ══════════════════════════════════════════════════════════════════════
    public async Task<Result<OnlineExamStatsDto>> UpdateStudentStatusAsync(
        long teacherId, long onlineExamId, long teacherStudentId, UpdateOnlineExamStudentStatusRequest request)
    {
        var exam = await _unitOfWork.OnlineExamsRepo.GetByIdAndTeacherAsync(onlineExamId, teacherId);
        if (exam is null)
            return Result<OnlineExamStatsDto>.Failure(_localizer, OnlineExamConstants.Messages.NotFound, HttpStatusCode.NotFound);

        // Only Blocked/InProgress via this endpoint — Passed/Failed only ever come from grading (§0-Q2/Q8).
        if (request.Status != StudentOnlineExamStatus.Blocked && request.Status != StudentOnlineExamStatus.InProgress)
            return Result<OnlineExamStatsDto>.Failure(_localizer, OnlineExamConstants.Messages.ManualStatusNotAllowed, HttpStatusCode.BadRequest);

        if (!await _unitOfWork.OnlineExamsRepo.IsTeacherStudentOwnedByTeacherAsync(teacherStudentId, teacherId))
            return Result<OnlineExamStatsDto>.Failure(_localizer, OnlineExamConstants.Messages.StudentNotOwned, HttpStatusCode.Forbidden);

        var report = await _unitOfWork.StudentOnlineExamReportsRepo.GetByExamAndStudentAsync(onlineExamId, teacherStudentId);
        var utcNow = DateTime.UtcNow;

        if (report is null)
        {
            // Can't "unblock" a report that was never created.
            if (request.Status == StudentOnlineExamStatus.InProgress)
                return Result<OnlineExamStatsDto>.Failure(_localizer, OnlineExamConstants.Messages.ReportNotFound, HttpStatusCode.NotFound);

            report = new StudentOnlineExamReport
            {
                OnlineExamId = onlineExamId,
                TeacherStudentId = teacherStudentId,
                TeacherId = teacherId,
                Status = StudentOnlineExamStatus.Blocked,
                Score = 0,
                Percentage = 0,
                CreateAt = utcNow,
            };
            await _unitOfWork.StudentOnlineExamReportsRepo.AddAsync(report);
            await _unitOfWork.SaveChangesAsync();
        }
        else
        {
            if (report.SubmittedAt is not null)
                return Result<OnlineExamStatsDto>.Failure(_localizer, OnlineExamConstants.Messages.CannotBlockFinalizedReport, HttpStatusCode.Conflict);

            report.Status = request.Status;
            report.UpdatedAt = utcNow;
            await _unitOfWork.StudentOnlineExamReportsRepo.UpdateAsync(report);
            await _unitOfWork.SaveChangesAsync();
        }

        var questions = await _unitOfWork.OnlineExamsRepo.GetQuestionsForTeacherAsync(onlineExamId);
        var answers = await _unitOfWork.StudentOnlineExamReportsRepo.GetAnswersForReportAsync(report.Id);
        decimal totalGrade = questions.Sum(q => q.Degree);

        var stats = _grading.ComputeStats(questions, answers, report.Score, totalGrade);
        return Result<OnlineExamStatsDto>.Success(stats, _localizer, OnlineExamConstants.Messages.Updated);
    }
}