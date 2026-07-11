using System.Net;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Exams;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements the offline Exams module (<c>/api/exams</c>).
///
/// MODEL: an exam is an <see cref="AssignmentTemplate"/> of type <see cref="AssignmentType.Exam"/>
/// anchored to one or more sessions. Each anchored session produces exactly one
/// <see cref="AssignmentOccurrence"/> (carrying <c>SessionId</c>, and for DuringSession the linked
/// <c>SessionOccurrenceId</c>), and one <see cref="StudentAssignmentObligation"/> per student in
/// that session (or the supplied subset). This per-session shape is what lets the opened-exam
/// screen group by session and compute per-session statistics.
///
/// ARCHITECTURE: every DB operation goes through a named repo method on <see cref="IUnitOfWork"/>.
/// Student resolution reuses <c>IPaymentRepo.GetStudentIdsBySessionAsync</c> — the single source of
/// truth for "who's in a session".
/// </summary>
public class ExamService : IExamService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ITimeZoneService _timeZoneService;
    private readonly IExamAttendanceSyncService _examAttendanceSync;

    public ExamService(
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer,
        ITimeZoneService timeZoneService,
        IExamAttendanceSyncService examAttendanceSync)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _timeZoneService = timeZoneService;
        _examAttendanceSync = examAttendanceSync;
    }

    /// <inheritdoc />
    public async Task<Result<ExamCreatedDto>> CreateExamAsync(
        long teacherId, long actingUserId, CreateExamDto dto)
    {
        // ── 1. Scalar validation ─────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 200)
            return Fail("ExamNameRequired");

        if (dto.Notes is not null && dto.Notes.Length > 2000)
            return Fail("AssignmentNotesTooLong");

        if (dto.MaxGrade <= 0m)
            return Fail("ExamRequiresMaxGrade");

        if (dto.SuccessScore < 0m)
            return Fail("PassingThresholdOutOfRange");

        // The core grade-range rule the tutor asked for: success can never exceed the max.
        if (dto.SuccessScore > dto.MaxGrade)
            return Fail("SuccessScoreExceedsMax");

        if (dto.Sessions is null || dto.Sessions.Count == 0)
            return Fail("ExamRequiresSession");

        var sessionIds = dto.Sessions.Select(s => s.SessionId).ToList();
        if (sessionIds.Distinct().Count() != sessionIds.Count)
            return Fail("DuplicateSessionInExam");

        DateTime utcNow = DateTime.UtcNow;
        DateTime today = utcNow.Date;

        // ── 2. Resolve & validate each session into a materialization plan ────
        var plans = new List<SessionPlan>();
        foreach (var input in dto.Sessions)
        {
            var session = await _unitOfWork.SessionsRepo
                .GetByIdAndTeacherAsync(input.SessionId, teacherId);
            if (session is null)
                return Fail("SessionNotFoundOrForeign");

            DateTime dueDate;
            long? sessionOccurrenceId = null;

            if (dto.DeliveryType == ExamDeliveryType.DuringSession)
            {
                if (input.SessionOccurrenceId is null)
                    return Fail("SessionOccurrenceRequired");

                var occ = await _unitOfWork.AttendanceRepo
                    .GetOccurrenceByIdAndTeacherAsync(input.SessionOccurrenceId.Value, teacherId);
                if (occ is null || occ.SessionId != input.SessionId)
                    return Fail("SessionOccurrenceNotFound", HttpStatusCode.NotFound);

                dueDate = occ.OccurrenceDate.Date;
                sessionOccurrenceId = occ.Id;
            }
            else // SeparateTime
            {
                if (input.ExamDate is null)
                    return Fail("ExamDateRequired");

                dueDate = input.ExamDate.Value.Date;
                if (dueDate < today)
                    return Fail("AssignmentDateInPast");
            }

            // Resolve the session's students (single source of truth), then apply the subset if any.
            var sessionStudentIds = await _unitOfWork.PaymentsRepo
                .GetStudentIdsBySessionAsync(teacherId, input.SessionId);
            var sessionStudentSet = sessionStudentIds.ToHashSet();

            List<long> studentIds;
            if (input.StudentIds is not null && input.StudentIds.Count > 0)
            {
                studentIds = input.StudentIds.Distinct().ToList();
                if (studentIds.Any(id => !sessionStudentSet.Contains(id)))
                    return Fail("StudentNotInSession");
            }
            else
            {
                studentIds = sessionStudentSet.ToList();
            }

            if (studentIds.Count == 0)
                return Fail("SessionHasNoStudents");

            plans.Add(new SessionPlan
            {
                SessionId = input.SessionId,
                DueDate = dueDate,
                SessionOccurrenceId = sessionOccurrenceId,
                StudentIds = studentIds,
            });
        }

        // ── 3. Build the entity graph: template → session scopes → per-session occurrences → obligations ──
        var template = new AssignmentTemplate
        {
            TeacherId = teacherId,
            AssignmentType = AssignmentType.Exam,
            Name = dto.Name.Trim(),
            NameAr = null,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            IsRecurring = false,
            RecurrencePattern = RecurrencePattern.OneTime,
            IsRecurrenceStopped = false,
            TrackingMode = null,
            MaxGrade = dto.MaxGrade,
            PassingThreshold = dto.SuccessScore,
            ExamDeliveryType = dto.DeliveryType,
            CreatedByUserId = actingUserId,
            UpdatedAt = utcNow,
            CreateAt = utcNow,
        };

        var scopes = plans.Select(p => new AssignmentScope
        {
            TeacherId = teacherId,
            Template = template,
            ScopeType = AssignmentScopeType.Session,
            SessionId = p.SessionId,
            CreateAt = utcNow,
        }).ToList();

        var occurrences = new List<AssignmentOccurrence>();
        var obligations = new List<StudentAssignmentObligation>();
        int occurrenceNumber = 1;
        foreach (var p in plans)
        {
            var occurrence = new AssignmentOccurrence
            {
                Template = template,
                TeacherId = teacherId,
                OccurrenceNumber = occurrenceNumber++,
                DueDate = p.DueDate,
                Status = AssignmentOccurrenceStatus.Pending,
                SessionId = p.SessionId,
                SessionOccurrenceId = p.SessionOccurrenceId,
                MaxGradeSnapshot = dto.MaxGrade,
                PassingThresholdSnapshot = dto.SuccessScore,
                TrackingModeSnapshot = null,
                TotalStudentCount = p.StudentIds.Count,
                CreateAt = utcNow,
            };
            p.Occurrence = occurrence;
            occurrences.Add(occurrence);

            foreach (var studentId in p.StudentIds)
            {
                obligations.Add(new StudentAssignmentObligation
                {
                    Occurrence = occurrence,
                    TeacherId = teacherId,
                    TeacherStudentId = studentId,
                    Status = ObligationStatus.Pending,
                    IsGradeEntered = false,
                    MarkedByScan = false,
                    UpdatedAt = utcNow,
                    CreateAt = utcNow,
                });
            }
        }

        // ── 4. Persist atomically ────────────────────────────────────────────
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _unitOfWork.ExamHomeworkRepo.AddTemplateAsync(template);
            await _unitOfWork.ExamHomeworkRepo.AddScopesRangeAsync(scopes);
            await _unitOfWork.ExamHomeworkRepo.AddOccurrencesRangeAsync(occurrences);
            await _unitOfWork.ExamHomeworkRepo.AddObligationsRangeAsync(obligations);

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await _unitOfWork.RollbackAsync();
            return Result<ExamCreatedDto>.Failure(_localizer, "DatabaseConflict", HttpStatusCode.Conflict);
        }

        // Phase 5: for DuringSession exams, back-fill each occurrence's obligations from attendance
        // already recorded on its linked SessionOccurrence (best-effort — the sync swallows errors).
        // Live updates thereafter flow from AttendanceService via IExamAttendanceSyncService.
        if (dto.DeliveryType == ExamDeliveryType.DuringSession)
        {
            foreach (var p in plans)
            {
                if (p.SessionOccurrenceId.HasValue)
                    await _examAttendanceSync.BackfillExamOccurrenceAsync(
                        teacherId, p.Occurrence!.Id, p.SessionOccurrenceId.Value, actingUserId);
            }
        }

        var response = new ExamCreatedDto
        {
            ExamId = template.Id,
            Name = template.Name,
            DeliveryType = dto.DeliveryType,
            MaxGrade = dto.MaxGrade,
            SuccessScore = dto.SuccessScore,
            SessionsCount = occurrences.Count,
            StudentsAssigned = obligations.Count,
            Sessions = plans.Select(p => new ExamSessionCreatedDto
            {
                SessionId = p.SessionId,
                OccurrenceId = p.Occurrence!.Id,
                ExamDate = p.DueDate,
                StudentsAssigned = p.StudentIds.Count,
            }).ToList(),
        };

        return Result<ExamCreatedDto>.Success(response, _localizer, "ExamCreated", HttpStatusCode.Created);
    }

    /// <inheritdoc />
    public async Task<Result<List<SessionExamDateDto>>> GetSessionExamDatesAsync(
        long teacherId, long sessionId, int year, int month)
    {
        if (month < 1 || month > 12 || year < 2000 || year > 2100)
            return Result<List<SessionExamDateDto>>.Failure(
                _localizer, "InvalidMonth", HttpStatusCode.BadRequest);

        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<List<SessionExamDateDto>>.Failure(
                _localizer, "SessionNotFoundOrForeign", HttpStatusCode.NotFound);

        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var occurrences = await _unitOfWork.AttendanceRepo
            .GetOccurrencesBySessionAndDateRangeAsync(sessionId, start, end);

        var items = occurrences.Select(o => new SessionExamDateDto
        {
            SessionOccurrenceId = o.Id,
            Date = o.OccurrenceDate,
            Status = o.Status.ToString(),
        }).ToList();

        return Result<List<SessionExamDateDto>>.Success(items, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<ExamHomeDto>> GetExamHomeAsync(long teacherId)
    {
        var occurrences = await _unitOfWork.ExamHomeworkRepo.GetExamOccurrencesForHomeAsync(teacherId);
        var summaries = await _unitOfWork.ExamHomeworkRepo
            .GetCompletionSummariesByOccurrenceIdsAsync(occurrences.Select(o => o.OccurrenceId));

        DateTime today = _timeZoneService.GetTeacherLocalDate(teacherId);

        var cards = occurrences.Select(o =>
        {
            var s = summaries.GetValueOrDefault(o.OccurrenceId);
            return new ExamHomeCardDto
            {
                ExamId = o.TemplateId,
                OccurrenceId = o.OccurrenceId,
                Name = o.ExamName,
                DeliveryType = o.DeliveryType,
                SessionId = o.SessionId,
                SessionName = o.SessionName,
                Date = o.DueDate,
                AssignedCount = s?.TotalStudents ?? 0,
                AttendedCount = s?.DoneOrAttended ?? 0,
                MissedCount = s?.NotDoneOrAbsent ?? 0,
                IsPast = o.DueDate.Date < today,
            };
        }).ToList();

        var result = new ExamHomeDto
        {
            Upcoming = cards.Where(c => !c.IsPast).OrderBy(c => c.Date).ToList(),
            Past = cards.Where(c => c.IsPast).OrderByDescending(c => c.Date).ToList(),
        };
        return Result<ExamHomeDto>.Success(result, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<ExamViewDto>> GetExamViewAsync(long teacherId, long examId)
    {
        var template = await _unitOfWork.ExamHomeworkRepo.GetTemplateByIdAndTeacherAsync(examId, teacherId);
        if (template is null || template.AssignmentType != AssignmentType.Exam)
            return Result<ExamViewDto>.Failure(_localizer, "ExamNotFound", HttpStatusCode.NotFound);

        var occurrences = await _unitOfWork.ExamHomeworkRepo.GetExamOccurrencesByTemplateAsync(teacherId, examId);
        var roster = await _unitOfWork.ExamHomeworkRepo.GetExamRosterByTemplateAsync(teacherId, examId);
        var rosterByOccurrence = roster.GroupBy(r => r.OccurrenceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sessions = occurrences.Select(o =>
        {
            var rows = rosterByOccurrence.GetValueOrDefault(o.OccurrenceId) ?? new List<ExamRosterRow>();
            return new ExamSessionViewDto
            {
                SessionId = o.SessionId ?? 0,
                SessionName = o.SessionName,
                OccurrenceId = o.OccurrenceId,
                Date = o.DueDate,
                Stats = ComputeStats(rows, o.PassingThresholdSnapshot),
                Students = rows.Select(r => MapRosterRow(r, o.PassingThresholdSnapshot)).ToList(),
            };
        }).ToList();

        var view = new ExamViewDto
        {
            ExamId = template.Id,
            Name = template.Name,
            DeliveryType = template.ExamDeliveryType,
            MaxGrade = template.MaxGrade,
            SuccessScore = template.PassingThreshold,
            GlobalStats = ComputeStats(roster, template.PassingThreshold),
            Sessions = sessions,
        };
        return Result<ExamViewDto>.Success(view, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<ExamSessionRosterDto>> GetExamSessionRosterAsync(
        long teacherId, long examId, long sessionId, int page, int pageSize, string? search)
    {
        var occurrences = await _unitOfWork.ExamHomeworkRepo.GetExamOccurrencesByTemplateAsync(teacherId, examId);
        if (occurrences.Count == 0)
            return Result<ExamSessionRosterDto>.Failure(_localizer, "ExamNotFound", HttpStatusCode.NotFound);

        var occ = occurrences.FirstOrDefault(o => o.SessionId == sessionId);
        if (occ is null)
            return Result<ExamSessionRosterDto>.Failure(_localizer, "SessionNotInExam", HttpStatusCode.NotFound);

        int safePage = page < 1 ? 1 : page;
        int safeSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        var (rows, totalCount) = await _unitOfWork.ExamHomeworkRepo.GetTrackingViewPagedAsync(
            teacherId, occ.OccurrenceId, search,
            statusFilter: null, missingEntries: null,
            gradeAboveThreshold: null, gradeBelowThreshold: null, belowPassingGrade: null,
            page: safePage, pageSize: safeSize);

        var students = rows.Select(r => new ExamStudentRowDto
        {
            ObligationId = r.ObligationId,
            TeacherStudentId = r.TeacherStudentId,
            StudentName = r.StudentName,
            StudentCode = r.StudentCode,
            Status = r.Status.ToString(),
            Attended = IsAttended(r.Status),
            Grade = r.GradeValue,
            IsGradeEntered = r.IsGradeEntered,
            IsBelowPassing = r.IsBelowPassing,
            RowVersion = Convert.ToBase64String(r.ObligationRowVersion),
        }).ToList();

        var dto = new ExamSessionRosterDto
        {
            ExamId = examId,
            SessionId = sessionId,
            SessionName = occ.SessionName,
            OccurrenceId = occ.OccurrenceId,
            Date = occ.DueDate,
            MaxGrade = occ.MaxGradeSnapshot,
            SuccessScore = occ.PassingThresholdSnapshot,
            Students = new PaginatedResponse<List<ExamStudentRowDto>>
            {
                data = students,
                page = safePage,
                pageSize = safeSize,
                totalCount = totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)safeSize),
            },
        };
        return Result<ExamSessionRosterDto>.Success(dto, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<BatchGradeResultDto>> SaveGradesAsync(
        long teacherId, long actingUserId, BatchGradeDto dto)
    {
        const int maxItems = 1000;
        if (dto.Items is null || dto.Items.Count == 0)
            return Result<BatchGradeResultDto>.Failure(_localizer, "BulkItemsEmpty", HttpStatusCode.BadRequest);
        if (dto.Items.Count > maxItems)
            return Result<BatchGradeResultDto>.Failure(_localizer, "BulkItemsTooMany", HttpStatusCode.BadRequest);

        // De-duplicate by obligation id (last value wins) so a batch can't fight itself.
        var itemsById = new Dictionary<long, GradeItemDto>();
        foreach (var it in dto.Items) itemsById[it.ObligationId] = it;

        var obligations = await _unitOfWork.ExamHomeworkRepo
            .GetObligationsForGradingByIdsAsync(teacherId, itemsById.Keys);
        var byId = obligations.ToDictionary(o => o.Id);

        DateTime utcNow = DateTime.UtcNow;
        var results = new List<BatchGradeItemResultDto>();
        var toApply = new List<(StudentAssignmentObligation Obligation, GradeItemDto Item)>();

        foreach (var (obligationId, item) in itemsById)
        {
            if (!byId.TryGetValue(obligationId, out var o))
            { results.Add(FailItem(obligationId, "ObligationNotFound")); continue; }

            var occurrence = o.Occurrence;
            if (occurrence.Template.AssignmentType != AssignmentType.Exam)
            { results.Add(FailItem(obligationId, "NotAnExam")); continue; }

            // Grade range — the core rule: 0 ≤ grade ≤ the exam's max.
            if (item.Grade < 0m)
            { results.Add(FailItem(obligationId, "GradeOutOfRange")); continue; }
            if (occurrence.MaxGradeSnapshot.HasValue && item.Grade > occurrence.MaxGradeSnapshot.Value)
            { results.Add(FailItem(obligationId, "GradeExceedsMax")); continue; }

            // You cannot grade a student who did not attend.
            if (o.Status == ObligationStatus.DidNotAttend)
            { results.Add(FailItem(obligationId, "CannotGradeAbsentStudent")); continue; }

            // During-session grades-only guard: attendance comes from the session. A during-session
            // student not yet marked attended cannot be graded from the exam screen.
            if (occurrence.Template.ExamDeliveryType == ExamDeliveryType.DuringSession
                && o.Status == ObligationStatus.Pending)
            { results.Add(FailItem(obligationId, "AttendanceNotRecordedForExam")); continue; }

            toApply.Add((o, item));
        }

        if (toApply.Count > 0)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                foreach (var (o, item) in toApply)
                {
                    var audit = new StudentObligationAuditLog
                    {
                        StudentObligationId = o.Id,
                        TeacherId = o.TeacherId,
                        OldStatus = o.Status,
                        NewStatus = ObligationStatus.AttendedWithGrade,
                        OldGradeValue = o.GradeValue,
                        NewGradeValue = item.Grade,
                        MaxGradeSnapshot = o.Occurrence.MaxGradeSnapshot,
                        PassingThresholdSnapshot = o.Occurrence.PassingThresholdSnapshot,
                        ChangeReason = "Grade entry",
                        ChangedByUserId = actingUserId,
                        ChangedAt = utcNow,
                        CreateAt = utcNow,
                    };

                    // Entering a grade implies the student attended (AttendedWithGrade). For
                    // during-session exams this only ever runs on an already-attended student.
                    o.Status = ObligationStatus.AttendedWithGrade;
                    o.GradeValue = item.Grade;
                    o.IsGradeEntered = true;
                    o.LastUpdatedByUserId = actingUserId;
                    o.UpdatedAt = utcNow;

                    await _unitOfWork.ExamHomeworkRepo.UpdateObligationAsync(o);
                    await _unitOfWork.ExamHomeworkRepo.AddAuditLogAsync(audit);
                    _unitOfWork.ExamHomeworkRepo.SetObligationOriginalRowVersion(o, item.RowVersion);
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await _unitOfWork.RollbackAsync();
                return Result<BatchGradeResultDto>.Failure(
                    _localizer, "ObligationConcurrencyConflict", HttpStatusCode.Conflict);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }

            foreach (var (o, _) in toApply)
            {
                results.Add(new BatchGradeItemResultDto
                {
                    ObligationId = o.Id,
                    Success = true,
                    Status = o.Status.ToString(),
                    Grade = o.GradeValue,
                    RowVersion = Convert.ToBase64String(o.RowVersion),
                });
            }
        }

        var payload = new BatchGradeResultDto
        {
            UpdatedCount = toApply.Count,
            AllSucceeded = results.All(r => r.Success),
            Items = results,
        };
        return Result<BatchGradeResultDto>.Success(payload, _localizer, "GradesSaved");
    }

    /// <inheritdoc />
    public async Task<Result<ExamAttendanceResultDto>> MarkExamAttendanceAsync(
        long teacherId, long actingUserId, ExamAttendanceDto dto)
    {
        const int maxItems = 1000;
        if (dto.Items is null || dto.Items.Count == 0)
            return Result<ExamAttendanceResultDto>.Failure(_localizer, "BulkItemsEmpty", HttpStatusCode.BadRequest);
        if (dto.Items.Count > maxItems)
            return Result<ExamAttendanceResultDto>.Failure(_localizer, "BulkItemsTooMany", HttpStatusCode.BadRequest);

        var occurrence = await _unitOfWork.ExamHomeworkRepo
            .GetOccurrenceWithTemplateAsync(dto.OccurrenceId, teacherId);
        if (occurrence is null)
            return Result<ExamAttendanceResultDto>.Failure(_localizer, "OccurrenceNotFound", HttpStatusCode.NotFound);
        if (occurrence.Template.AssignmentType != AssignmentType.Exam)
            return Result<ExamAttendanceResultDto>.Failure(_localizer, "NotAnExam", HttpStatusCode.BadRequest);
        if (occurrence.Template.ExamDeliveryType == ExamDeliveryType.DuringSession)
            return Result<ExamAttendanceResultDto>.Failure(
                _localizer, "AttendanceReadOnlyForDuringSession", HttpStatusCode.Conflict);

        var itemsById = new Dictionary<long, ExamAttendanceItemDto>();
        foreach (var it in dto.Items) itemsById[it.ObligationId] = it;

        var obligations = await _unitOfWork.ExamHomeworkRepo
            .GetObligationsByIdsAsync(teacherId, dto.OccurrenceId, itemsById.Keys);
        var byId = obligations.ToDictionary(o => o.Id);

        DateTime utcNow = DateTime.UtcNow;
        var results = new List<ExamAttendanceItemResultDto>();
        var toApply = new List<(StudentAssignmentObligation Obligation, bool Present)>();

        foreach (var (obligationId, item) in itemsById)
        {
            if (!byId.TryGetValue(obligationId, out var o))
            {
                results.Add(new ExamAttendanceItemResultDto
                { ObligationId = obligationId, Success = false, Code = "ObligationNotFound" });
                continue;
            }
            toApply.Add((o, item.Present));
        }

        if (toApply.Count > 0)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                foreach (var (o, present) in toApply)
                {
                    ObligationStatus newStatus;
                    decimal? newGrade = o.GradeValue;
                    bool gradeEntered = o.IsGradeEntered;

                    if (present)
                    {
                        newStatus = (o.IsGradeEntered && o.GradeValue.HasValue)
                            ? ObligationStatus.AttendedWithGrade
                            : ObligationStatus.Attended;
                    }
                    else
                    {
                        newStatus = ObligationStatus.DidNotAttend;
                        newGrade = null;
                        gradeEntered = false;
                    }

                    var audit = new StudentObligationAuditLog
                    {
                        StudentObligationId = o.Id,
                        TeacherId = o.TeacherId,
                        OldStatus = o.Status,
                        NewStatus = newStatus,
                        OldGradeValue = o.GradeValue,
                        NewGradeValue = newGrade,
                        MaxGradeSnapshot = occurrence.MaxGradeSnapshot,
                        PassingThresholdSnapshot = occurrence.PassingThresholdSnapshot,
                        ChangeReason = "Exam attendance",
                        ChangedByUserId = actingUserId,
                        ChangedAt = utcNow,
                        CreateAt = utcNow,
                    };

                    o.Status = newStatus;
                    o.GradeValue = newGrade;
                    o.IsGradeEntered = gradeEntered;
                    o.LastUpdatedByUserId = actingUserId;
                    o.UpdatedAt = utcNow;

                    await _unitOfWork.ExamHomeworkRepo.UpdateObligationAsync(o);
                    await _unitOfWork.ExamHomeworkRepo.AddAuditLogAsync(audit);
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await _unitOfWork.RollbackAsync();
                return Result<ExamAttendanceResultDto>.Failure(
                    _localizer, "ObligationConcurrencyConflict", HttpStatusCode.Conflict);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }

            foreach (var (o, _) in toApply)
            {
                results.Add(new ExamAttendanceItemResultDto
                {
                    ObligationId = o.Id,
                    Success = true,
                    Status = o.Status.ToString(),
                    RowVersion = Convert.ToBase64String(o.RowVersion),
                });
            }
        }

        var payload = new ExamAttendanceResultDto
        {
            UpdatedCount = toApply.Count,
            AllSucceeded = results.All(r => r.Success),
            Items = results,
        };
        return Result<ExamAttendanceResultDto>.Success(payload, _localizer, "ExamAttendanceSaved");
    }

    /// <inheritdoc />
    public async Task<Result<ExamScanResultDto>> ScanExamAttendanceAsync(
        long teacherId, long actingUserId, ExamScanDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            return Result<ExamScanResultDto>.Failure(_localizer, "BarcodeRequired", HttpStatusCode.BadRequest);

        var occurrence = await _unitOfWork.ExamHomeworkRepo
            .GetOccurrenceWithTemplateAsync(dto.OccurrenceId, teacherId);
        if (occurrence is null)
            return Result<ExamScanResultDto>.Failure(_localizer, "OccurrenceNotFound", HttpStatusCode.NotFound);
        if (occurrence.Template.AssignmentType != AssignmentType.Exam)
            return Result<ExamScanResultDto>.Failure(_localizer, "NotAnExam", HttpStatusCode.BadRequest);
        if (occurrence.Template.ExamDeliveryType == ExamDeliveryType.DuringSession)
            return Result<ExamScanResultDto>.Failure(
                _localizer, "AttendanceReadOnlyForDuringSession", HttpStatusCode.Conflict);

        var student = await _unitOfWork.Students.GetActiveByCodeAndTeacherAsync(dto.Code.Trim(), teacherId);
        if (student is null)
            return Result<ExamScanResultDto>.Failure(_localizer, "StudentCodeNotFound", HttpStatusCode.NotFound);

        var obligation = await _unitOfWork.ExamHomeworkRepo
            .GetObligationByOccurrenceAndStudentAsync(dto.OccurrenceId, student.Id);
        if (obligation is null || obligation.TeacherId != teacherId)
            return Result<ExamScanResultDto>.Failure(_localizer, "ExamStudentNotFound", HttpStatusCode.NotFound);

        bool alreadyProcessed = obligation.Status is ObligationStatus.Attended or ObligationStatus.AttendedWithGrade;

        // Atomic, idempotent — marks Attended while preserving an already-entered grade.
        await _unitOfWork.ExamHomeworkRepo.SetExamAttendanceByOccurrenceAsync(
            teacherId, dto.OccurrenceId, new[] { student.Id },
            ObligationStatus.Attended, clearGrade: false, skipGraded: true, DateTime.UtcNow, actingUserId);

        var newStatus = obligation.Status == ObligationStatus.AttendedWithGrade
            ? ObligationStatus.AttendedWithGrade
            : ObligationStatus.Attended;

        return Result<ExamScanResultDto>.Success(new ExamScanResultDto
        {
            ObligationId = obligation.Id,
            TeacherStudentId = student.Id,
            StudentName = student.StudentName,
            StudentCode = student.StudentCode,
            Status = newStatus.ToString(),
            AlreadyProcessed = alreadyProcessed,
        }, _localizer, alreadyProcessed ? "ScanAlreadyProcessed" : "ScanRecorded");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private Result<ExamCreatedDto> Fail(string key, HttpStatusCode status = HttpStatusCode.BadRequest) =>
        Result<ExamCreatedDto>.Failure(_localizer, key, status);

    private static BatchGradeItemResultDto FailItem(long obligationId, string code) =>
        new() { ObligationId = obligationId, Success = false, Code = code };

    private static bool IsAttended(ObligationStatus status) =>
        status == ObligationStatus.Attended || status == ObligationStatus.AttendedWithGrade;

    private static ExamStudentRowDto MapRosterRow(ExamRosterRow r, decimal? passingThreshold) => new()
    {
        ObligationId = r.ObligationId,
        TeacherStudentId = r.TeacherStudentId,
        StudentName = r.StudentName,
        StudentCode = r.StudentCode,
        Status = r.Status.ToString(),
        Attended = IsAttended(r.Status),
        Grade = r.GradeValue,
        IsGradeEntered = r.IsGradeEntered,
        IsBelowPassing = r.IsGradeEntered && r.GradeValue.HasValue && passingThreshold.HasValue
                         && r.GradeValue.Value < passingThreshold.Value,
        RowVersion = Convert.ToBase64String(r.ObligationRowVersion),
    };

    private static ExamStatsDto ComputeStats(IReadOnlyList<ExamRosterRow> rows, decimal? passingThreshold)
    {
        var graded = rows.Where(r => r.IsGradeEntered && r.GradeValue.HasValue)
            .Select(r => r.GradeValue!.Value).ToList();
        return new ExamStatsDto
        {
            TotalStudents = rows.Count,
            GradedCount = graded.Count,
            Average = graded.Count > 0 ? Math.Round(graded.Average(), 2) : null,
            Highest = graded.Count > 0 ? graded.Max() : null,
            Lowest = graded.Count > 0 ? graded.Min() : null,
            AttendedCount = rows.Count(r => IsAttended(r.Status)),
            MissedCount = rows.Count(r => r.Status == ObligationStatus.DidNotAttend),
            PendingCount = rows.Count(r => r.Status == ObligationStatus.Pending),
            BelowPassingCount = passingThreshold.HasValue
                ? graded.Count(g => g < passingThreshold.Value) : 0,
        };
    }

    /// <summary>Internal per-session materialization plan (mutated with the built occurrence).</summary>
    private sealed class SessionPlan
    {
        public long SessionId { get; init; }
        public DateTime DueDate { get; init; }
        public long? SessionOccurrenceId { get; init; }
        public List<long> StudentIds { get; init; } = new();
        public AssignmentOccurrence? Occurrence { get; set; }
    }
}
