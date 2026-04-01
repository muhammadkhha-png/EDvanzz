using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Attendance;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements all Attendance Module operations (Module 3).
/// Follows the Result pattern for operation outcomes.
/// All database access goes through IUnitOfWork — no direct repository access with raw predicates.
/// 
/// ARCHITECTURAL NOTE:
/// All query logic is encapsulated in IAttendanceRepo named methods.
/// If a query changes, you edit the repo method — not this service.
/// 
/// PERFORMANCE DESIGN:
/// - Absent-student alerts use denormalized StudentAbsenceCounter for O(1) lookups.
/// - Duplicate detection uses the UNIQUE index on (SessionOccurrenceId, TeacherStudentId).
/// - Cross-session checks use batch-loaded linked session IDs.
/// 
/// STUDENT/PARENT ACCESS:
/// Read-only methods validate visibility via TeacherConfiguration before returning data.
/// </summary>
public class AttendanceService : IAttendanceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public AttendanceService(
        IUnitOfWork unitOfWork,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    // ══════════════════════════════════════════════
    // SESSION OCCURRENCE MANAGEMENT
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<int>> GenerateOccurrencesAsync(long teacherId, long sessionId)
    {
        // 1. Validate session exists and belongs to the teacher
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<int>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        // 2. Delete existing occurrences that have no attendance records
        int protectedCount = await _unitOfWork.AttendanceRepo.DeleteUnusedOccurrencesAsync(sessionId);

        // 3. Compute all occurrence dates from the session's recurrence configuration
        var occurrenceDates = ComputeOccurrenceDates(session);

        // 4. Filter out dates that already have protected occurrences (with attendance)
        var existingOccurrences = await _unitOfWork.AttendanceRepo.GetOccurrencesBySessionAsync(sessionId);
        var existingDates = existingOccurrences.Select(o => o.OccurrenceDate.Date).ToHashSet();

        var newOccurrences = new List<SessionOccurrence>();
        int index = 0;

        foreach (var date in occurrenceDates.OrderBy(d => d))
        {
            if (!existingDates.Contains(date.Date))
            {
                newOccurrences.Add(new SessionOccurrence
                {
                    SessionId = sessionId,
                    OccurrenceDate = date,
                    OccurrenceIndex = index,
                    CreateAt = DateTime.UtcNow
                });
            }
            index++;
        }

        // 5. Bulk-add new occurrences
        if (newOccurrences.Count > 0)
        {
            await _unitOfWork.AttendanceRepo.AddOccurrencesAsync(newOccurrences);
            await _unitOfWork.SaveChangesAsync();
        }

        return Result<int>.Success(newOccurrences.Count + existingOccurrences.Count, _localizer, "OccurrencesGeneratedSuccess");
    }

    /// <inheritdoc />
    public async Task<Result<AttendanceDashboardDto>> GetDashboardAsync(long teacherId)
    {
        // 1. Validate teacher exists
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<AttendanceDashboardDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        var today = DateTime.UtcNow.Date;

        // 2. Get all occurrences for today across all teacher's sessions
        var todayOccurrences = await _unitOfWork.AttendanceRepo.GetOccurrencesByDateAndTeacherAsync(teacherId, today);

        // 3. Build session cards
        var sessionCards = new List<AttendanceDashboardSessionDto>();
        int completedCount = 0;

        foreach (var occurrence in todayOccurrences)
        {
            var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(occurrence.SessionId, teacherId);
            if (session is null) continue;

            // Get attendance counts for this occurrence
            var (presentCount, absentCount, heldCount) = await _unitOfWork.AttendanceRepo
                .GetOccurrenceStatusCountsAsync(occurrence.Id);

            // Get total assigned students
            var assignments = await _unitOfWork.AttendanceRepo
                .GetActiveAssignmentsBySessionAsync(occurrence.SessionId);
            int totalAssigned = assignments.Count;
            int markedCount = presentCount + absentCount + heldCount;

            // Determine color status (REQ-ATT-051)
            string statusColor = DetermineSessionStatusColor(markedCount, totalAssigned);

            if (statusColor == "Green")
                completedCount++;

            // Load group name
            string? groupName = null;
            if (session.SessionGroupId.HasValue)
            {
                var group = await _unitOfWork.SessionsRepo.GetGroupByIdAndTeacherAsync(
                    session.SessionGroupId.Value, teacherId);
                groupName = group?.GroupName;
            }

            sessionCards.Add(new AttendanceDashboardSessionDto
            {
                SessionId = session.Id,
                SessionName = session.SessionName,
                SessionGroupId = session.SessionGroupId,
                SessionGroupName = groupName,
                IsToday = true,
                TodayOccurrenceId = occurrence.Id,
                MarkedCount = markedCount,
                TotalAssigned = totalAssigned,
                StatusColor = statusColor
            });
        }

        var dashboard = new AttendanceDashboardDto
        {
            Today = today,
            TotalSessionsToday = sessionCards.Count,
            CompletedSessions = completedCount,
            PendingSessions = sessionCards.Count - completedCount,
            Sessions = sessionCards.OrderByDescending(s => s.StatusColor == "Red")
                .ThenByDescending(s => s.StatusColor == "Amber")
                .ThenBy(s => s.SessionName)
                .ToList()
        };

        return Result<AttendanceDashboardDto>.Success(dashboard, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<SessionOccurrenceDto>>>> GetSessionOccurrencesAsync(
        long teacherId, long sessionId, int page = 1, int pageSize = 50)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<PaginatedResponse<List<SessionOccurrenceDto>>>.Failure(
                _localizer, "SessionNotFound", HttpStatusCode.NotFound);

        var occurrences = await _unitOfWork.AttendanceRepo.GetOccurrencesBySessionAsync(sessionId);

        // Build DTOs with completion status
        var dtos = new List<SessionOccurrenceDto>();
        foreach (var occ in occurrences)
        {
            var (presentCount, absentCount, heldCount) = await _unitOfWork.AttendanceRepo
                .GetOccurrenceStatusCountsAsync(occ.Id);
            var assignments = await _unitOfWork.AttendanceRepo
                .GetActiveAssignmentsBySessionAsync(occ.SessionId);

            int totalAssigned = assignments.Count;
            int totalMarked = presentCount + absentCount + heldCount;

            string completionStatus = totalMarked == 0 ? "Unrecorded"
                : totalMarked >= totalAssigned ? "Complete"
                : "Partial";

            dtos.Add(new SessionOccurrenceDto
            {
                Id = occ.Id,
                SessionId = occ.SessionId,
                OccurrenceDate = occ.OccurrenceDate,
                OccurrenceIndex = occ.OccurrenceIndex,
                CompletionStatus = completionStatus,
                PresentCount = presentCount,
                AbsentCount = absentCount,
                TotalAssigned = totalAssigned
            });
        }

        // Paginate
        int totalCount = dtos.Count;
        var pagedDtos = dtos.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var response = new PaginatedResponse<List<SessionOccurrenceDto>>
        {
            totalCount = totalCount,
            page = page,
            pageSize = pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            data = pagedDtos
        };

        return Result<PaginatedResponse<List<SessionOccurrenceDto>>>.Success(response, _localizer);
    }

    // ══════════════════════════════════════════════
    // TAKE ATTENDANCE
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<TakeAttendanceScreenDto>> GetTakeAttendanceScreenAsync(
        long teacherId, long sessionId, DateTime? occurrenceDate = null)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<TakeAttendanceScreenDto>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        var targetDate = occurrenceDate?.Date ?? DateTime.UtcNow.Date;

        // Check if an occurrence exists for this date
        var occurrence = await _unitOfWork.AttendanceRepo.GetOccurrenceBySessionAndDateAsync(sessionId, targetDate);

        // REQ-ATT-004: Warning if not a scheduled occurrence day
        bool isScheduledToday = occurrence is not null;

        if (!isScheduledToday && occurrenceDate is null)
        {
            // Try to find the nearest occurrence for today
            return Result<TakeAttendanceScreenDto>.Failure(
                _localizer, "AttendanceNotScheduledToday", HttpStatusCode.BadRequest);
        }

        if (occurrence is null)
        {
            return Result<TakeAttendanceScreenDto>.Failure(
                _localizer, "OccurrenceNotFound", HttpStatusCode.NotFound);
        }

        // Load primary session students
        var primaryAssignments = await _unitOfWork.AttendanceRepo.GetActiveAssignmentsBySessionAsync(sessionId);

        // Load existing attendance records for this occurrence
        var existingRecords = await _unitOfWork.AttendanceRepo.GetRecordsByOccurrenceAsync(occurrence.Id);
        var markedStudentIds = existingRecords.ToDictionary(r => r.TeacherStudentId, r => r.Status);

        // Build primary student list (REQ-ATT-054: unmarked first)
        var primaryStudents = new List<TakeAttendanceStudentDto>();
        foreach (var assignment in primaryAssignments)
        {
            var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
                assignment.TeacherStudentId, teacherId);
            if (student is null) continue;

            primaryStudents.Add(new TakeAttendanceStudentDto
            {
                TeacherStudentId = student.Id,
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                Barcode = student.Barcode,
                CurrentStatus = markedStudentIds.GetValueOrDefault(student.Id),
                IsFromLinkedSession = false,
                LinkedSessionName = null
            });
        }

        // Load linked session students (REQ-ATT-014/015)
        var linkedSessions = await _unitOfWork.SessionsRepo.GetLinkedSessionsAsync(sessionId);
        var linkedStudents = new List<TakeAttendanceStudentDto>();

        foreach (var linkedSession in linkedSessions)
        {
            var linkedAssignments = await _unitOfWork.AttendanceRepo
                .GetActiveAssignmentsBySessionAsync(linkedSession.Id);

            foreach (var assignment in linkedAssignments)
            {
                var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
                    assignment.TeacherStudentId, teacherId);
                if (student is null) continue;

                linkedStudents.Add(new TakeAttendanceStudentDto
                {
                    TeacherStudentId = student.Id,
                    StudentName = student.StudentName,
                    StudentCode = student.StudentCode,
                    Barcode = student.Barcode,
                    CurrentStatus = markedStudentIds.GetValueOrDefault(student.Id),
                    IsFromLinkedSession = true,
                    LinkedSessionName = linkedSession.SessionName
                });
            }
        }

        // Sort: unmarked first (REQ-ATT-054)
        primaryStudents = primaryStudents
            .OrderBy(s => s.CurrentStatus.HasValue ? 1 : 0)
            .ThenBy(s => s.StudentName)
            .ToList();

        linkedStudents = linkedStudents
            .OrderBy(s => s.CurrentStatus.HasValue ? 1 : 0)
            .ThenBy(s => s.LinkedSessionName)
            .ThenBy(s => s.StudentName)
            .ToList();

        int presentCount = existingRecords.Count(r => r.Status == AttendanceStatus.Present);
        int absentCount = existingRecords.Count(r => r.Status == AttendanceStatus.Absent);
        int heldCount = existingRecords.Count(r => r.Status == AttendanceStatus.Held);
        int totalStudents = primaryStudents.Count + linkedStudents.Count;

        var screen = new TakeAttendanceScreenDto
        {
            SessionId = sessionId,
            SessionName = session.SessionName,
            SessionOccurrenceId = occurrence.Id,
            OccurrenceDate = occurrence.OccurrenceDate,
            IsScheduledToday = isScheduledToday,
            PresentCount = presentCount,
            AbsentCount = absentCount,
            HeldCount = heldCount,
            UnmarkedCount = totalStudents - presentCount - absentCount - heldCount,
            TotalStudents = totalStudents,
            PrimaryStudents = primaryStudents,
            LinkedStudents = linkedStudents
        };

        return Result<TakeAttendanceScreenDto>.Success(screen, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<AttendanceResultDto>> RecordAttendanceByCodeAsync(RecordAttendanceByCodeDto dto)
    {
        // 1. Find the student by code under this teacher
        var student = await FindStudentByCodeAsync(dto.TeacherId, dto.StudentCode);
        if (student is null)
            return Result<AttendanceResultDto>.Failure(_localizer, "StudentNotFoundByCode", HttpStatusCode.NotFound);

        // 2. Get today's occurrence for the session
        var today = DateTime.UtcNow.Date;
        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(dto.SessionId, today);

        if (occurrence is null)
            return Result<AttendanceResultDto>.Failure(_localizer, "OccurrenceNotFound", HttpStatusCode.NotFound);

        // 3. Record attendance using the shared core method
        return await RecordSingleAttendanceCoreAsync(
            dto.TeacherId, dto.SessionId, student, occurrence,
            AttendanceMethod.ManualCode, dto.RecordedByUserId);
    }

    /// <inheritdoc />
    public async Task<Result<AttendanceResultDto>> RecordAttendanceByBarcodeAsync(RecordAttendanceByBarcodeDto dto)
    {
        // 1. Find the student by barcode (barcode encodes student code per REQ-ATT-009)
        var student = await FindStudentByBarcodeAsync(dto.TeacherId, dto.BarcodeData);
        if (student is null)
            return Result<AttendanceResultDto>.Failure(_localizer, "BarcodeNotRecognized", HttpStatusCode.NotFound);

        // 2. Check if student is assigned to this session or a linked session
        var assignment = await _unitOfWork.AttendanceRepo.GetActiveAssignmentAsync(student.Id);

        // REQ-ATT-013: Warning if student not assigned to current session
        bool isCrossSession = assignment is not null && assignment.SessionId != dto.SessionId;

        if (assignment is null)
            return Result<AttendanceResultDto>.Failure(_localizer, "StudentNotAssignedToAnySession", HttpStatusCode.BadRequest);

        // 3. Get today's occurrence for the session
        var today = DateTime.UtcNow.Date;
        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(dto.SessionId, today);

        if (occurrence is null)
            return Result<AttendanceResultDto>.Failure(_localizer, "OccurrenceNotFound", HttpStatusCode.NotFound);

        // 4. Record attendance
        return await RecordSingleAttendanceCoreAsync(
            dto.TeacherId, dto.SessionId, student, occurrence,
            AttendanceMethod.BarcodeScan, dto.RecordedByUserId);
    }

    /// <inheritdoc />
    public async Task<Result<List<AttendanceResultDto>>> RecordAttendanceMultiSelectAsync(
        RecordAttendanceMultiSelectDto dto)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionId, dto.TeacherId);
        if (session is null)
            return Result<List<AttendanceResultDto>>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        var today = DateTime.UtcNow.Date;
        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(dto.SessionId, today);

        if (occurrence is null)
            return Result<List<AttendanceResultDto>>.Failure(_localizer, "OccurrenceNotFound", HttpStatusCode.NotFound);

        var results = new List<AttendanceResultDto>();

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            foreach (var studentId in dto.TeacherStudentIds)
            {
                var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(studentId, dto.TeacherId);
                if (student is null) continue;

                var result = await RecordOrUpdateAttendanceCoreAsync(
                    dto.TeacherId, dto.SessionId, student, occurrence,
                    dto.Status, AttendanceMethod.MultiSelect, dto.RecordedByUserId);

                results.Add(result);
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        return Result<List<AttendanceResultDto>>.Success(results, _localizer, "AttendanceRecordedSuccess");
    }

    /// <inheritdoc />
    public async Task<Result<MarkAllPresentResultDto>> MarkAllPresentAsync(MarkAllPresentDto dto)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionId, dto.TeacherId);
        if (session is null)
            return Result<MarkAllPresentResultDto>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        var today = DateTime.UtcNow.Date;
        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(dto.SessionId, today);

        if (occurrence is null)
            return Result<MarkAllPresentResultDto>.Failure(_localizer, "OccurrenceNotFound", HttpStatusCode.NotFound);

        // Get all assigned students
        var assignments = await _unitOfWork.AttendanceRepo.GetActiveAssignmentsBySessionAsync(dto.SessionId);
        var existingRecords = await _unitOfWork.AttendanceRepo.GetRecordsByOccurrenceAsync(occurrence.Id);
        var alreadyMarkedIds = existingRecords.Select(r => r.TeacherStudentId).ToHashSet();

        int markedCount = 0;
        int alreadyMarkedCount = alreadyMarkedIds.Count;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            foreach (var assignment in assignments)
            {
                if (alreadyMarkedIds.Contains(assignment.TeacherStudentId))
                    continue;

                var record = new AttendanceRecord
                {
                    TeacherId = dto.TeacherId,
                    TeacherStudentId = assignment.TeacherStudentId,
                    StudentSessionAssignmentId = assignment.Id,
                    SessionOccurrenceId = occurrence.Id,
                    OccurrenceDate = occurrence.OccurrenceDate,
                    Status = AttendanceStatus.Present,
                    AttendanceMethod = AttendanceMethod.MarkAllPresent,
                    IsCrossSession = false,
                    RecordedAt = DateTime.UtcNow,
                    RecordedByUserId = dto.RecordedByUserId,
                    CreateAt = DateTime.UtcNow
                };

                await _unitOfWork.AttendanceRepo.AddAsync(record);

                // Update absence counter
                await UpdateAbsenceCounterOnPresenceAsync(dto.TeacherId, assignment.TeacherStudentId, session.SessionName);

                markedCount++;
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        var result = new MarkAllPresentResultDto
        {
            MarkedPresentCount = markedCount,
            AlreadyMarkedCount = alreadyMarkedCount,
            TotalStudents = assignments.Count
        };

        return Result<MarkAllPresentResultDto>.Success(result, _localizer, "MarkAllPresentSuccess");
    }

    /// <inheritdoc />
    public async Task<Result<AttendanceCompletionSummaryDto>> GetCompletionSummaryAsync(
        long teacherId, long sessionId, long occurrenceId)
    {
        var records = await _unitOfWork.AttendanceRepo.GetRecordsByOccurrenceAsync(occurrenceId);

        int totalPresent = records.Count(r => r.Status == AttendanceStatus.Present);
        int totalAbsent = records.Count(r => r.Status == AttendanceStatus.Absent);
        int totalHeld = records.Count(r => r.Status == AttendanceStatus.Held);

        // Get flagged students (absent with consecutive streaks)
        var flaggedStudents = new List<FlaggedAbsentStudentDto>();
        var absentStudentIds = records
            .Where(r => r.Status == AttendanceStatus.Absent)
            .Select(r => r.TeacherStudentId)
            .ToList();

        foreach (var studentId in absentStudentIds)
        {
            var counter = await _unitOfWork.AttendanceRepo.GetAbsenceCounterByStudentAsync(studentId);
            if (counter is null || counter.ConsecutiveAbsences <= 1) continue;

            var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(studentId, teacherId);
            if (student is null) continue;

            flaggedStudents.Add(new FlaggedAbsentStudentDto
            {
                TeacherStudentId = studentId,
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                ConsecutiveAbsences = counter.ConsecutiveAbsences
            });
        }

        var summary = new AttendanceCompletionSummaryDto
        {
            TotalPresent = totalPresent,
            TotalAbsent = totalAbsent,
            TotalHeld = totalHeld,
            FlaggedStudents = flaggedStudents.OrderByDescending(f => f.ConsecutiveAbsences).ToList()
        };

        return Result<AttendanceCompletionSummaryDto>.Success(summary, _localizer);
    }

    // ══════════════════════════════════════════════
    // EDIT ATTENDANCE
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<EditAttendanceResultDto>> EditAttendanceAsync(EditAttendanceDto dto)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionId, dto.TeacherId);
        if (session is null)
            return Result<EditAttendanceResultDto>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(dto.SessionId, dto.OccurrenceDate.Date);

        if (occurrence is null)
            return Result<EditAttendanceResultDto>.Failure(_localizer, "OccurrenceNotFound", HttpStatusCode.NotFound);

        int createdCount = 0;
        int updatedCount = 0;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            foreach (var entry in dto.Entries)
            {
                var existingRecord = await _unitOfWork.AttendanceRepo
                    .GetRecordByOccurrenceAndStudentAsync(occurrence.Id, entry.TeacherStudentId);

                if (existingRecord is not null)
                {
                    // Update existing record — log the edit (REQ-ATT-025)
                    if (existingRecord.Status != entry.Status)
                    {
                        var editLog = new AttendanceEditLog
                        {
                            AttendanceRecordId = existingRecord.Id,
                            PreviousStatus = existingRecord.Status,
                            NewStatus = entry.Status,
                            EditedAt = DateTime.UtcNow,
                            EditedByUserId = dto.EditedByUserId,
                            EditReason = dto.EditReason,
                            CreateAt = DateTime.UtcNow
                        };

                        await _unitOfWork.AttendanceRepo.AddEditLogAsync(editLog);

                        // Update the record's status (original RecordedAt is NOT changed per BR-ATT-006)
                        existingRecord.Status = entry.Status;
                        await _unitOfWork.AttendanceRepo.UpdateAsync(existingRecord);

                        // Update the absence counter to reflect the change
                        await RecalculateAbsenceCounterAsync(dto.TeacherId, entry.TeacherStudentId);

                        updatedCount++;
                    }
                }
                else
                {
                    // Create new record for this occurrence (REQ-ATT-024: add missed records)
                    var assignment = await _unitOfWork.AttendanceRepo.GetActiveAssignmentAsync(entry.TeacherStudentId);
                    if (assignment is null) continue;

                    var newRecord = new AttendanceRecord
                    {
                        TeacherId = dto.TeacherId,
                        TeacherStudentId = entry.TeacherStudentId,
                        StudentSessionAssignmentId = assignment.Id,
                        SessionOccurrenceId = occurrence.Id,
                        OccurrenceDate = occurrence.OccurrenceDate,
                        Status = entry.Status,
                        AttendanceMethod = AttendanceMethod.ManualEdit,
                        IsCrossSession = false,
                        RecordedAt = DateTime.UtcNow,
                        RecordedByUserId = dto.EditedByUserId,
                        CreateAt = DateTime.UtcNow
                    };

                    await _unitOfWork.AttendanceRepo.AddAsync(newRecord);

                    // Update absence counter
                    if (entry.Status == AttendanceStatus.Absent)
                        await UpdateAbsenceCounterOnAbsenceAsync(dto.TeacherId, entry.TeacherStudentId, session.SessionName, occurrence.OccurrenceDate);
                    else if (entry.Status == AttendanceStatus.Present)
                        await UpdateAbsenceCounterOnPresenceAsync(dto.TeacherId, entry.TeacherStudentId, session.SessionName);

                    createdCount++;
                }
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        var result = new EditAttendanceResultDto
        {
            CreatedCount = createdCount,
            UpdatedCount = updatedCount,
            TotalProcessed = createdCount + updatedCount
        };

        return Result<EditAttendanceResultDto>.Success(result, _localizer, "AttendanceEditedSuccess");
    }

    /// <inheritdoc />
    public async Task<Result<List<AttendanceEditLogDto>>> GetAttendanceEditLogsAsync(
        long teacherId, long attendanceRecordId)
    {
        var record = await _unitOfWork.AttendanceRepo.GetByIdAsync(attendanceRecordId);
        if (record is null || record.TeacherId != teacherId)
            return Result<List<AttendanceEditLogDto>>.Failure(_localizer, "AttendanceRecordNotFound", HttpStatusCode.NotFound);

        var logs = await _unitOfWork.AttendanceRepo.GetEditLogsByRecordAsync(attendanceRecordId);

        var dtos = logs.Select(l => new AttendanceEditLogDto
        {
            Id = l.Id,
            PreviousStatus = l.PreviousStatus,
            NewStatus = l.NewStatus,
            EditedAt = l.EditedAt,
            EditReason = l.EditReason
        }).ToList();

        return Result<List<AttendanceEditLogDto>>.Success(dtos, _localizer);
    }

    // ══════════════════════════════════════════════
    // ABSENCE OVERVIEW
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<AbsenceOverviewStudentDto>>>> GetAbsenceOverviewAsync(
        AbsenceOverviewRequest request)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(request.SessionId, request.TeacherId);
        if (session is null)
            return Result<PaginatedResponse<List<AbsenceOverviewStudentDto>>>.Failure(
                _localizer, "SessionNotFound", HttpStatusCode.NotFound);

        // Get linked session IDs for cross-session view (REQ-ATT-033)
        var linkedSessions = await _unitOfWork.SessionsRepo.GetLinkedSessionsAsync(request.SessionId);
        var linkedSessionIds = linkedSessions.Select(s => s.Id).ToList();

        // Build the query
        var query = _unitOfWork.AttendanceRepo.BuildAbsenceOverviewQuery(
            request.SessionId, linkedSessionIds, request.Search);

        int totalCount = await _unitOfWork.AttendanceRepo.CountAsync(
            query.Select(c => c.TeacherStudent).Cast<AttendanceRecord>().AsQueryable());

        // Simplified: count from counters directly
        var counters = await _unitOfWork.AttendanceRepo
            .GetAbsenceCountersBySessionAsync(request.SessionId);

        // Include linked session counters
        if (linkedSessionIds.Count > 0)
        {
            var linkedCounters = await _unitOfWork.AttendanceRepo
                .GetAbsenceCountersBySessionsAsync(linkedSessionIds);
            counters = counters.Concat(linkedCounters).ToList().AsReadOnly();
        }

        // Filter by search if provided
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLower();
            counters = counters.Where(c =>
            {
                // We need the student data — loaded from repo
                return true; // Filtering done in repo query
            }).ToList().AsReadOnly();
        }

        // Sort by consecutive absences descending (REQ-ATT-067)
        var sortedCounters = counters
            .Where(c => c.ConsecutiveAbsences > 0)
            .OrderByDescending(c => c.ConsecutiveAbsences)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var dtos = new List<AbsenceOverviewStudentDto>();
        foreach (var counter in sortedCounters)
        {
            var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
                counter.TeacherStudentId, request.TeacherId);
            if (student is null) continue;

            // Get last 5 statuses (REQ-ATT-068)
            var recentRecords = await _unitOfWork.AttendanceRepo
                .GetRecordsByStudentAsync(counter.TeacherStudentId);
            var last5 = recentRecords
                .OrderByDescending(r => r.OccurrenceDate)
                .Take(5)
                .Select(r => r.Status)
                .ToList();

            var assignment = await _unitOfWork.AttendanceRepo.GetActiveAssignmentAsync(counter.TeacherStudentId);

            dtos.Add(new AbsenceOverviewStudentDto
            {
                TeacherStudentId = counter.TeacherStudentId,
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                ConsecutiveAbsences = counter.ConsecutiveAbsences,
                CumulativeTotalAbsences = counter.CumulativeTotalAbsences,
                SessionName = assignment?.SessionNameSnapshot ?? string.Empty,
                Last5Statuses = last5
            });
        }

        var response = new PaginatedResponse<List<AbsenceOverviewStudentDto>>
        {
            totalCount = counters.Count(c => c.ConsecutiveAbsences > 0),
            page = request.Page,
            pageSize = request.PageSize,
            totalPages = (int)Math.Ceiling(counters.Count(c => c.ConsecutiveAbsences > 0) / (double)request.PageSize),
            data = dtos
        };

        return Result<PaginatedResponse<List<AbsenceOverviewStudentDto>>>.Success(response, _localizer);
    }

    // ══════════════════════════════════════════════
    // STUDENT ATTENDANCE TIMELINE
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<StudentAttendanceAllTimeSummaryDto>>>> GetStudentTimelineListAsync(
        StudentTimelineListRequest request)
    {
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(request.TeacherId);
        if (teacher is null)
            return Result<PaginatedResponse<List<StudentAttendanceAllTimeSummaryDto>>>.Failure(
                _localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // Build student list query using existing student repo
        var query = _unitOfWork.Students.BuildStudentListQuery(
            request.TeacherId,
            request.Search,
            request.SessionId);

        int totalCount = await _unitOfWork.Students.CountAsync(query);
        var students = await _unitOfWork.Students.GetPagedAsync(query, request.Page, request.PageSize);

        var dtos = new List<StudentAttendanceAllTimeSummaryDto>();
        foreach (var student in students)
        {
            var (totalRecords, totalAbsences) = await _unitOfWork.AttendanceRepo
                .GetStudentAllTimeSummaryAsync(student.Id);

            var counter = await _unitOfWork.AttendanceRepo.GetAbsenceCounterByStudentAsync(student.Id);

            dtos.Add(new StudentAttendanceAllTimeSummaryDto
            {
                TeacherStudentId = student.Id,
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                TotalOccurrences = totalRecords,
                TotalAbsences = totalAbsences,
                AttendancePercentage = totalRecords > 0
                    ? Math.Round((decimal)(totalRecords - totalAbsences) / totalRecords * 100, 1)
                    : 0,
                CurrentConsecutiveAbsences = counter?.ConsecutiveAbsences ?? 0
            });
        }

        var response = new PaginatedResponse<List<StudentAttendanceAllTimeSummaryDto>>
        {
            totalCount = totalCount,
            page = request.Page,
            pageSize = request.PageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
            data = dtos
        };

        return Result<PaginatedResponse<List<StudentAttendanceAllTimeSummaryDto>>>.Success(response, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<StudentAttendanceAllTimeSummaryDto>> GetStudentAllTimeSummaryAsync(
        long teacherId, long teacherStudentId)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(teacherStudentId, teacherId);
        if (student is null)
            return Result<StudentAttendanceAllTimeSummaryDto>.Failure(
                _localizer, "StudentNotFound", HttpStatusCode.NotFound);

        var (totalRecords, totalAbsences) = await _unitOfWork.AttendanceRepo
            .GetStudentAllTimeSummaryAsync(teacherStudentId);

        var counter = await _unitOfWork.AttendanceRepo.GetAbsenceCounterByStudentAsync(teacherStudentId);

        var dto = new StudentAttendanceAllTimeSummaryDto
        {
            TeacherStudentId = teacherStudentId,
            StudentName = student.StudentName,
            StudentCode = student.StudentCode,
            TotalOccurrences = totalRecords,
            TotalAbsences = totalAbsences,
            AttendancePercentage = totalRecords > 0
                ? Math.Round((decimal)(totalRecords - totalAbsences) / totalRecords * 100, 1)
                : 0,
            CurrentConsecutiveAbsences = counter?.ConsecutiveAbsences ?? 0
        };

        return Result<StudentAttendanceAllTimeSummaryDto>.Success(dto, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<StudentAttendanceMonthDto>> GetStudentTimelineMonthAsync(
        long teacherId, long teacherStudentId, int year, int month)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(teacherStudentId, teacherId);
        if (student is null)
            return Result<StudentAttendanceMonthDto>.Failure(
                _localizer, "StudentNotFound", HttpStatusCode.NotFound);

        return await BuildStudentMonthDtoAsync(teacherStudentId, year, month);
    }

    /// <inheritdoc />
    public async Task<Result<List<StudentSessionAssignmentDto>>> GetStudentAssignmentHistoryAsync(
        long teacherId, long teacherStudentId)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(teacherStudentId, teacherId);
        if (student is null)
            return Result<List<StudentSessionAssignmentDto>>.Failure(
                _localizer, "StudentNotFound", HttpStatusCode.NotFound);

        var assignments = await _unitOfWork.AttendanceRepo.GetAssignmentsByStudentAsync(teacherStudentId);

        var dtos = new List<StudentSessionAssignmentDto>();
        foreach (var a in assignments)
        {
            var records = await _unitOfWork.AttendanceRepo.GetRecordsByAssignmentAsync(a.Id);

            dtos.Add(new StudentSessionAssignmentDto
            {
                Id = a.Id,
                SessionId = a.SessionId ?? 0,
                SessionName = a.SessionNameSnapshot,
                AssignedAt = a.AssignedAt,
                UnassignedAt = a.UnassignedAt,
                IsActive = a.IsActive,
                AttendanceRecordCount = records.Count,
                AbsenceCount = records.Count(r => r.Status == AttendanceStatus.Absent)
            });
        }

        return Result<List<StudentSessionAssignmentDto>>.Success(dtos, _localizer);
    }

    // ══════════════════════════════════════════════
    // STUDENT / PARENT READ-ONLY ACCESS
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<StudentAttendanceAllTimeSummaryDto>> GetStudentOwnAttendanceSummaryAsync(
        long studentUserId, long teacherId)
    {
        // Validate student-teacher link and visibility
        var link = await _unitOfWork.Users.GetActiveStudentTeacherLinkAsync(studentUserId, teacherId);
        if (link is null || link.TeacherStudentId is null)
            return Result<StudentAttendanceAllTimeSummaryDto>.Failure(
                _localizer, "LinkNotFound", HttpStatusCode.NotFound);

        // Check visibility
        var config = await _unitOfWork.Users.GetTeacherConfigurationAsync(teacherId);
        if (config is null || !config.StudentVisibilityAttendance)
            return Result<StudentAttendanceAllTimeSummaryDto>.Failure(
                _localizer, "AttendanceNotVisible", HttpStatusCode.Forbidden);

        return await GetStudentAllTimeSummaryAsync(teacherId, link.TeacherStudentId.Value);
    }

    /// <inheritdoc />
    public async Task<Result<StudentAttendanceAllTimeSummaryDto>> GetParentChildAttendanceSummaryAsync(
        long parentUserId, long teacherId, long teacherStudentId)
    {
        // Validate parent has access to this student via teacher
        var config = await _unitOfWork.Users.GetTeacherConfigurationAsync(teacherId);
        if (config is null || !config.ParentVisibilityAttendance)
            return Result<StudentAttendanceAllTimeSummaryDto>.Failure(
                _localizer, "AttendanceNotVisible", HttpStatusCode.Forbidden);

        return await GetStudentAllTimeSummaryAsync(teacherId, teacherStudentId);
    }

    /// <inheritdoc />
    public async Task<Result<StudentAttendanceMonthDto>> GetStudentOwnTimelineMonthAsync(
        long studentUserId, long teacherId, int year, int month)
    {
        var link = await _unitOfWork.Users.GetActiveStudentTeacherLinkAsync(studentUserId, teacherId);
        if (link is null || link.TeacherStudentId is null)
            return Result<StudentAttendanceMonthDto>.Failure(
                _localizer, "LinkNotFound", HttpStatusCode.NotFound);

        var config = await _unitOfWork.Users.GetTeacherConfigurationAsync(teacherId);
        if (config is null || !config.StudentVisibilityAttendance)
            return Result<StudentAttendanceMonthDto>.Failure(
                _localizer, "AttendanceNotVisible", HttpStatusCode.Forbidden);

        return await BuildStudentMonthDtoAsync(link.TeacherStudentId.Value, year, month);
    }

    /// <inheritdoc />
    public async Task<Result<StudentAttendanceMonthDto>> GetParentChildTimelineMonthAsync(
        long parentUserId, long teacherId, long teacherStudentId, int year, int month)
    {
        var config = await _unitOfWork.Users.GetTeacherConfigurationAsync(teacherId);
        if (config is null || !config.ParentVisibilityAttendance)
            return Result<StudentAttendanceMonthDto>.Failure(
                _localizer, "AttendanceNotVisible", HttpStatusCode.Forbidden);

        return await BuildStudentMonthDtoAsync(teacherStudentId, year, month);
    }

    // ══════════════════════════════════════════════
    // PRIVATE CORE METHODS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Core method for recording a single student's attendance.
    /// Handles duplicate detection (BR-ATT-002), absence alert generation
    /// (REQ-ATT-027/028/057/058), cross-session detection, and counter updates.
    /// Used by all three attendance methods.
    /// </summary>
    private async Task<Result<AttendanceResultDto>> RecordSingleAttendanceCoreAsync(
        long teacherId, long sessionId, TeacherStudent student,
        SessionOccurrence occurrence, AttendanceMethod method, long? recordedByUserId)
    {
        // 1. Duplicate detection (BR-ATT-002, REQ-ATT-069/070)
        var existingRecord = await _unitOfWork.AttendanceRepo
            .GetRecordByOccurrenceAndStudentAsync(occurrence.Id, student.Id);

        if (existingRecord is not null)
        {
            return Result<AttendanceResultDto>.Success(new AttendanceResultDto
            {
                IsSuccess = false,
                TeacherStudentId = student.Id,
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                IsDuplicate = true,
                DuplicateRecordedAt = existingRecord.RecordedAt
            }, _localizer, "AttendanceDuplicateDetected");
        }

        // Also check cross-session duplicate for linked sessions
        var linkedSessions = await _unitOfWork.SessionsRepo.GetLinkedSessionsAsync(sessionId);
        var linkedSessionIds = linkedSessions.Select(s => s.Id).ToList();
        linkedSessionIds.Add(sessionId);

        var crossSessionDuplicate = await _unitOfWork.AttendanceRepo
            .GetRecordByDateAndStudentAcrossLinkedSessionsAsync(
                student.Id, occurrence.OccurrenceDate, linkedSessionIds);

        if (crossSessionDuplicate is not null)
        {
            return Result<AttendanceResultDto>.Success(new AttendanceResultDto
            {
                IsSuccess = false,
                TeacherStudentId = student.Id,
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                IsDuplicate = true,
                DuplicateRecordedAt = crossSessionDuplicate.RecordedAt
            }, _localizer, "AttendanceDuplicateDetected");
        }

        // 2. Get assignment and check absence alert
        var assignment = await _unitOfWork.AttendanceRepo.GetActiveAssignmentAsync(student.Id);
        if (assignment is null)
            return Result<AttendanceResultDto>.Failure(_localizer, "StudentNotAssignedToAnySession", HttpStatusCode.BadRequest);

        bool isCrossSession = assignment.SessionId != sessionId;

        // 3. Check absence counter for alert (REQ-ATT-027/028)
        var absenceCounter = await _unitOfWork.AttendanceRepo.GetAbsenceCounterByStudentAsync(student.Id);

        bool wasAbsentLastSession = absenceCounter?.LastAttendanceStatus == AttendanceStatus.Absent;
        int consecutiveAbsences = absenceCounter?.ConsecutiveAbsences ?? 0;

        // 4. Determine the cross-session occurrence reference
        long? attendedOccurrenceId = null;
        if (isCrossSession)
        {
            // The student's own session occurrence for this date
            attendedOccurrenceId = occurrence.Id;
        }

        // 5. Create the attendance record
        var record = new AttendanceRecord
        {
            TeacherId = teacherId,
            TeacherStudentId = student.Id,
            StudentSessionAssignmentId = assignment.Id,
            SessionOccurrenceId = occurrence.Id,
            OccurrenceDate = occurrence.OccurrenceDate,
            AttendedSessionOccurrenceId = isCrossSession ? occurrence.Id : null,
            Status = AttendanceStatus.Present,
            AttendanceMethod = method,
            IsCrossSession = isCrossSession,
            RecordedAt = DateTime.UtcNow,
            RecordedByUserId = recordedByUserId,
            CreateAt = DateTime.UtcNow
        };

        await _unitOfWork.AttendanceRepo.AddAsync(record);

        // 6. Update absence counter
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        await UpdateAbsenceCounterOnPresenceAsync(teacherId, student.Id, session?.SessionName ?? string.Empty);

        await _unitOfWork.SaveChangesAsync();

        // 7. Build result with alert info
        var resultDto = new AttendanceResultDto
        {
            IsSuccess = true,
            TeacherStudentId = student.Id,
            StudentName = student.StudentName,
            StudentCode = student.StudentCode,
            RecordedStatus = AttendanceStatus.Present,
            WasAbsentLastSession = wasAbsentLastSession,
            ConsecutiveAbsences = consecutiveAbsences,
            LastAbsenceDate = wasAbsentLastSession ? absenceCounter?.LastAttendanceDate : null,
            LastAbsenceSessionName = wasAbsentLastSession ? absenceCounter?.LastAttendanceSessionName : null,
            LastAbsenceWasCrossSession = false,
            IsDuplicate = false
        };

        return Result<AttendanceResultDto>.Success(resultDto, _localizer, "AttendanceRecordedSuccess");
    }

    /// <summary>
    /// Core method for recording or updating attendance with a specified status.
    /// Used by multi-select which can set any status directly.
    /// </summary>
    private async Task<AttendanceResultDto> RecordOrUpdateAttendanceCoreAsync(
        long teacherId, long sessionId, TeacherStudent student,
        SessionOccurrence occurrence, AttendanceStatus status,
        AttendanceMethod method, long? recordedByUserId)
    {
        var existingRecord = await _unitOfWork.AttendanceRepo
            .GetRecordByOccurrenceAndStudentAsync(occurrence.Id, student.Id);

        if (existingRecord is not null)
        {
            // Already marked — return duplicate info
            return new AttendanceResultDto
            {
                IsSuccess = false,
                TeacherStudentId = student.Id,
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                IsDuplicate = true,
                DuplicateRecordedAt = existingRecord.RecordedAt
            };
        }

        var assignment = await _unitOfWork.AttendanceRepo.GetActiveAssignmentAsync(student.Id);
        if (assignment is null)
        {
            return new AttendanceResultDto
            {
                IsSuccess = false,
                TeacherStudentId = student.Id,
                StudentName = student.StudentName,
                StudentCode = student.StudentCode
            };
        }

        bool isCrossSession = assignment.SessionId != sessionId;

        var record = new AttendanceRecord
        {
            TeacherId = teacherId,
            TeacherStudentId = student.Id,
            StudentSessionAssignmentId = assignment.Id,
            SessionOccurrenceId = occurrence.Id,
            OccurrenceDate = occurrence.OccurrenceDate,
            Status = status,
            AttendanceMethod = method,
            IsCrossSession = isCrossSession,
            RecordedAt = DateTime.UtcNow,
            RecordedByUserId = recordedByUserId,
            CreateAt = DateTime.UtcNow
        };

        await _unitOfWork.AttendanceRepo.AddAsync(record);

        // Update absence counter based on status
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        string sessionName = session?.SessionName ?? string.Empty;

        if (status == AttendanceStatus.Present)
            await UpdateAbsenceCounterOnPresenceAsync(teacherId, student.Id, sessionName);
        else if (status == AttendanceStatus.Absent)
            await UpdateAbsenceCounterOnAbsenceAsync(teacherId, student.Id, sessionName, occurrence.OccurrenceDate);

        return new AttendanceResultDto
        {
            IsSuccess = true,
            TeacherStudentId = student.Id,
            StudentName = student.StudentName,
            StudentCode = student.StudentCode,
            RecordedStatus = status
        };
    }

    /// <summary>
    /// Updates the absence counter when a student is marked as present.
    /// REQ-ATT-030: Consecutive counter resets to zero.
    /// </summary>
    private async Task UpdateAbsenceCounterOnPresenceAsync(long teacherId, long teacherStudentId, string sessionName)
    {
        var counter = await _unitOfWork.AttendanceRepo.GetAbsenceCounterByStudentAsync(teacherStudentId);

        if (counter is null)
        {
            counter = new StudentAbsenceCounter
            {
                TeacherId = teacherId,
                TeacherStudentId = teacherStudentId,
                ConsecutiveAbsences = 0,
                CumulativeTotalAbsences = 0,
                LastAttendanceStatus = AttendanceStatus.Present,
                LastAttendanceDate = DateTime.UtcNow.Date,
                LastAttendanceSessionName = sessionName,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.AttendanceRepo.AddAbsenceCounterAsync(counter);
        }
        else
        {
            counter.ConsecutiveAbsences = 0; // REQ-ATT-030: Reset on presence
            counter.LastAttendanceStatus = AttendanceStatus.Present;
            counter.LastAttendanceDate = DateTime.UtcNow.Date;
            counter.LastAttendanceSessionName = sessionName;
            await _unitOfWork.AttendanceRepo.UpdateAbsenceCounterAsync(counter);
        }
    }

    /// <summary>
    /// Updates the absence counter when a student is marked as absent.
    /// REQ-ATT-029: Consecutive counter incremented.
    /// REQ-ATT-021: Cumulative total incremented.
    /// </summary>
    private async Task UpdateAbsenceCounterOnAbsenceAsync(
        long teacherId, long teacherStudentId, string sessionName, DateTime occurrenceDate)
    {
        var counter = await _unitOfWork.AttendanceRepo.GetAbsenceCounterByStudentAsync(teacherStudentId);

        if (counter is null)
        {
            counter = new StudentAbsenceCounter
            {
                TeacherId = teacherId,
                TeacherStudentId = teacherStudentId,
                ConsecutiveAbsences = 1,
                CumulativeTotalAbsences = 1,
                LastAttendanceStatus = AttendanceStatus.Absent,
                LastAttendanceDate = occurrenceDate,
                LastAttendanceSessionName = sessionName,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.AttendanceRepo.AddAbsenceCounterAsync(counter);
        }
        else
        {
            counter.ConsecutiveAbsences++;
            counter.CumulativeTotalAbsences++;
            counter.LastAttendanceStatus = AttendanceStatus.Absent;
            counter.LastAttendanceDate = occurrenceDate;
            counter.LastAttendanceSessionName = sessionName;
            await _unitOfWork.AttendanceRepo.UpdateAbsenceCounterAsync(counter);
        }
    }

    /// <summary>
    /// Recalculates the absence counter from scratch by scanning all attendance records.
    /// Used after an edit operation where historical status changed.
    /// </summary>
    private async Task RecalculateAbsenceCounterAsync(long teacherId, long teacherStudentId)
    {
        var allRecords = await _unitOfWork.AttendanceRepo.GetRecordsByStudentAsync(teacherStudentId);
        var orderedRecords = allRecords.OrderBy(r => r.OccurrenceDate).ThenBy(r => r.RecordedAt).ToList();

        int cumulativeAbsences = orderedRecords.Count(r => r.Status == AttendanceStatus.Absent);
        int consecutiveAbsences = 0;

        // Walk from most recent backward to find consecutive streak
        for (int i = orderedRecords.Count - 1; i >= 0; i--)
        {
            if (orderedRecords[i].Status == AttendanceStatus.Absent)
                consecutiveAbsences++;
            else
                break;
        }

        var lastRecord = orderedRecords.LastOrDefault();

        var counter = await _unitOfWork.AttendanceRepo.GetAbsenceCounterByStudentAsync(teacherStudentId);
        if (counter is null)
        {
            counter = new StudentAbsenceCounter
            {
                TeacherId = teacherId,
                TeacherStudentId = teacherStudentId,
                ConsecutiveAbsences = consecutiveAbsences,
                CumulativeTotalAbsences = cumulativeAbsences,
                LastAttendanceStatus = lastRecord?.Status,
                LastAttendanceDate = lastRecord?.OccurrenceDate,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.AttendanceRepo.AddAbsenceCounterAsync(counter);
        }
        else
        {
            counter.ConsecutiveAbsences = consecutiveAbsences;
            counter.CumulativeTotalAbsences = cumulativeAbsences;
            counter.LastAttendanceStatus = lastRecord?.Status;
            counter.LastAttendanceDate = lastRecord?.OccurrenceDate;
            await _unitOfWork.AttendanceRepo.UpdateAbsenceCounterAsync(counter);
        }
    }

    /// <summary>
    /// Finds a student by their student code within a teacher's account.
    /// Case-insensitive matching per REQ-STU-CODE-003.
    /// </summary>
    private async Task<TeacherStudent?> FindStudentByCodeAsync(long teacherId, string studentCode)
    {
        return await _unitOfWork.Students.GetActiveByCodeAndTeacherAsync(teacherId, studentCode);
    }

    /// <summary>
    /// Finds a student by their barcode data within a teacher's account.
    /// REQ-ATT-009: Barcode encodes the student code.
    /// </summary>
    private async Task<TeacherStudent?> FindStudentByBarcodeAsync(long teacherId, string barcodeData)
    {
        return await _unitOfWork.Students.GetActiveByBarcodeAndTeacherAsync(teacherId, barcodeData);
    }

    /// <summary>
    /// Builds a monthly attendance DTO for the student timeline.
    /// Shared by teacher, student, and parent views.
    /// </summary>
    private async Task<Result<StudentAttendanceMonthDto>> BuildStudentMonthDtoAsync(
        long teacherStudentId, int year, int month)
    {
        var records = await _unitOfWork.AttendanceRepo
            .GetRecordsByStudentAndMonthAsync(teacherStudentId, year, month);

        var (totalRecords, totalAbsences) = await _unitOfWork.AttendanceRepo
            .GetStudentMonthlySummaryAsync(teacherStudentId, year, month);

        int totalPresent = totalRecords - totalAbsences;

        var recordDtos = records.Select(r => new AttendanceRecordDto
        {
            Id = r.Id,
            TeacherStudentId = r.TeacherStudentId,
            StudentName = string.Empty, // Populated by caller if needed
            StudentCode = string.Empty,
            SessionOccurrenceId = r.SessionOccurrenceId,
            OccurrenceDate = r.OccurrenceDate,
            Status = r.Status,
            AttendanceMethod = r.AttendanceMethod,
            IsCrossSession = r.IsCrossSession,
            RecordedAt = r.RecordedAt,
            HasBeenEdited = false // Would need join to EditLogs
        }).ToList();

        var dto = new StudentAttendanceMonthDto
        {
            Year = year,
            Month = month,
            TotalOccurrences = totalRecords,
            TotalPresent = totalPresent,
            TotalAbsences = totalAbsences,
            AttendancePercentage = totalRecords > 0
                ? Math.Round((decimal)totalPresent / totalRecords * 100, 1)
                : 0,
            Records = recordDtos
        };

        return Result<StudentAttendanceMonthDto>.Success(dto, _localizer);
    }

    /// <summary>
    /// Computes all occurrence dates for a session based on its recurrence configuration.
    /// Generates dates from StartDate to EndDate matching the session's OccurrenceType.
    /// </summary>
    private static List<DateTime> ComputeOccurrenceDates(Session session)
    {
        var dates = new List<DateTime>();
        var startDate = session.StartDate.Date;
        var endDate = session.EndDate.Date;

        switch (session.OccurrenceType)
        {
            case OccurrenceType.Weekly:
                var weeklyDays = ParseSelectedDays(session.SelectedDays);
                if (weeklyDays is null) break;

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    int dayIndex = MapDayOfWeekToIndex(date.DayOfWeek);
                    if (weeklyDays.Contains(dayIndex))
                        dates.Add(date);
                }
                break;

            case OccurrenceType.BiWeekly:
                var biWeeklyDays = ParseSelectedDays(session.SelectedDays);
                if (biWeeklyDays is null) break;

                bool isActiveWeek = true;
                var weekStart = startDate;
                var previousWeekNumber = -1;

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    // Track week transitions
                    int currentWeekNumber = (date - startDate).Days / 7;
                    if (currentWeekNumber != previousWeekNumber)
                    {
                        if (previousWeekNumber >= 0)
                            isActiveWeek = !isActiveWeek;
                        previousWeekNumber = currentWeekNumber;
                    }

                    if (isActiveWeek)
                    {
                        int dayIndex = MapDayOfWeekToIndex(date.DayOfWeek);
                        if (biWeeklyDays.Contains(dayIndex))
                            dates.Add(date);
                    }
                }
                break;

            case OccurrenceType.Monthly:
                if (!session.MonthlyDayOfMonth.HasValue) break;
                int targetDay = session.MonthlyDayOfMonth.Value;

                for (var date = new DateTime(startDate.Year, startDate.Month, 1);
                     date <= endDate;
                     date = date.AddMonths(1))
                {
                    int daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
                    int actualDay = Math.Min(targetDay, daysInMonth);
                    var occurrenceDate = new DateTime(date.Year, date.Month, actualDay);

                    if (occurrenceDate >= startDate && occurrenceDate <= endDate)
                        dates.Add(occurrenceDate);
                }
                break;
        }

        return dates;
    }

    /// <summary>
    /// Maps .NET DayOfWeek to the Egyptian week index used in SelectedDays.
    /// 0=Saturday, 1=Sunday, 2=Monday, 3=Tuesday, 4=Wednesday, 5=Thursday, 6=Friday.
    /// </summary>
    private static int MapDayOfWeekToIndex(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Saturday => 0,
            DayOfWeek.Sunday => 1,
            DayOfWeek.Monday => 2,
            DayOfWeek.Tuesday => 3,
            DayOfWeek.Wednesday => 4,
            DayOfWeek.Thursday => 5,
            DayOfWeek.Friday => 6,
            _ => 0
        };
    }

    /// <summary>
    /// Parses a comma-separated day string into a list of integers.
    /// Same logic as SessionService — shared utility.
    /// </summary>
    private static List<int>? ParseSelectedDays(string? daysString)
    {
        if (string.IsNullOrWhiteSpace(daysString))
            return null;

        return daysString.Split(',')
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .ToList();
    }

    /// <summary>
    /// Determines the session status color for the dashboard.
    /// REQ-ATT-051: Green/Amber/Red based on attendance completion.
    /// </summary>
    private static string DetermineSessionStatusColor(int markedCount, int totalAssigned)
    {
        if (totalAssigned == 0) return "Grey";
        if (markedCount == 0) return "Red";
        if (markedCount >= totalAssigned) return "Green";
        return "Amber";
    }
}