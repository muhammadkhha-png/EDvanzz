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
/// Implements all Attendance Module (Module 3) operations.
/// Covers: session occurrence management, attendance taking (all three methods),
/// edit attendance, absence detection, cross-session attendance, absence overview,
/// student attendance timeline, reporting, and student/parent view access.
///
/// All database access goes through IUnitOfWork.AttendanceRepo (IAttendanceRepo),
/// IUnitOfWork.SessionsRepo (ISessionRepo), and IUnitOfWork.Students (ITeacherStudentRepo)
/// — no direct GetRepository calls with raw expression predicates.
///
/// ARCHITECTURAL NOTE:
/// All query logic is encapsulated in IAttendanceRepo named methods.
/// If a query changes, you edit the repo method — not this service.
///
/// TRANSACTION SAFETY:
/// All transactional methods use the ownsTransaction pattern:
///   bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
/// This makes them safe for both standalone calls and nested calls
/// from other modules (e.g., SessionService calling OnStudentAssignedToSessionAsync).
/// </summary>
public class AttendanceService : IAttendanceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOccurrenceGeneratorService _occurrenceGenerator;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;
    private readonly ITimeZoneService _timeZoneService;

    public AttendanceService(
        IUnitOfWork unitOfWork,
        IOccurrenceGeneratorService occurrenceGenerator,
        IStringLocalizer<Domain.Resources.Messages> localizer,
        ITimeZoneService timeZoneService)
    {
        _unitOfWork = unitOfWork;
        _occurrenceGenerator = occurrenceGenerator;
        _localizer = localizer;
        _timeZoneService = timeZoneService;
    }

    // ══════════════════════════════════════════════
    // SESSION OCCURRENCE MANAGEMENT
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<int>> GenerateOccurrencesAsync(long teacherId, long sessionId)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<int>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        // Compute dates from recurrence rules
        var dates = _occurrenceGenerator.ComputeOccurrenceDates(session);
        if (dates.Count == 0)
            return Result<int>.Success(0, _localizer, "AttendanceNoOccurrenceDates");

        // FIX 6.2: Load only dates (not full entities) for efficient duplicate checking
        var existingDates = await _unitOfWork.AttendanceRepo.GetExistingOccurrenceDatesAsync(sessionId);

        var newOccurrences = dates
            .Where(d => !existingDates.Contains(d.Date))
            .Select(d => new SessionOccurrence
            {
                TeacherId = teacherId,
                SessionId = sessionId,
                OccurrenceDate = d.Date,
                Status = OccurrenceStatus.Pending,
                CreateAt = DateTime.UtcNow
            })
            .ToList();

        if (newOccurrences.Count == 0)
            return Result<int>.Success(0, _localizer, "AttendanceOccurrencesAlreadyGenerated");

        await _unitOfWork.AttendanceRepo.AddOccurrencesRangeAsync(newOccurrences);
        await _unitOfWork.SaveChangesAsync();

        return Result<int>.Success(newOccurrences.Count, _localizer, "AttendanceOccurrencesGenerated");
    }

    // ══════════════════════════════════════════════
    // ATTENDANCE DASHBOARD (REQ-ATT-049 through 052)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<AttendanceDashboardDto>> GetDashboardAsync(
        long teacherId, AttendanceDashboardRequest request)
    {
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<AttendanceDashboardDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // FIX 1.5: Use teacher's local timezone instead of UTC
        var date = request.Date?.Date ?? _timeZoneService.GetTeacherLocalDate(teacherId);

        // REQ-ATT-049: Get all occurrences for today
        var todayOccurrences = await _unitOfWork.AttendanceRepo
            .GetOccurrencesByTeacherAndDateAsync(teacherId, date);

        // Build session cards including sessions that DON'T occur today (grey)
        var sessionCards = new List<AttendanceSessionCardDto>();

        foreach (var occurrence in todayOccurrences)
        {
            // Load session details
            var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(occurrence.SessionId, teacherId);
            if (session is null) continue;

            // Count marked and total students
            var records = await _unitOfWork.AttendanceRepo.GetRecordsByOccurrenceAsync(occurrence.Id);
            var activeAssignments = await _unitOfWork.AttendanceRepo
                .GetActiveAssignmentsBySessionAsync(occurrence.SessionId);
            int totalStudents = activeAssignments.Count;
            int markedCount = records.Count;

            // Load group name
            string? groupName = null;
            if (session.SessionGroupId.HasValue)
            {
                var group = await _unitOfWork.SessionsRepo.GetGroupByIdAndTeacherAsync(
                    session.SessionGroupId.Value, teacherId);
                groupName = group?.GroupName;
            }

            sessionCards.Add(new AttendanceSessionCardDto
            {
                SessionId = session.Id,
                SessionName = session.SessionName,
                SessionGroupId = session.SessionGroupId,
                SessionGroupName = groupName,
                IsToday = true,
                TodayOccurrenceId = occurrence.Id,
                Status = occurrence.Status,
                MarkedCount = markedCount,
                TotalStudents = totalStudents,
                StartTime = session.StartTime
            });
        }

        // REQ-ATT-052: Today's sessions at top, sorted by start time
        sessionCards = sessionCards.OrderBy(c => c.StartTime).ToList();

        var dashboard = new AttendanceDashboardDto
        {
            Date = date,
            TotalSessionsToday = todayOccurrences.Count,
            CompletedSessions = sessionCards.Count(c => c.Status == OccurrenceStatus.Completed),
            PendingSessions = sessionCards.Count(c => c.Status == OccurrenceStatus.Pending),
            InProgressSessions = sessionCards.Count(c => c.Status == OccurrenceStatus.InProgress),
            SessionCards = sessionCards
        };

        return Result<AttendanceDashboardDto>.Success(dashboard, _localizer, "Success");
    }

    // ══════════════════════════════════════════════
    // TAKE ATTENDANCE (REQ-ATT-006 through 018)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    /// FIX 1.2: Replaced in-memory pagination with database-side pagination.
    /// FIX 1.5: Uses teacher timezone for default date.
    public async Task<Result<PaginatedResponse<List<AttendanceStudentRowDto>>>> GetAttendanceStudentListAsync(
        long teacherId, long sessionId, DateTime? occurrenceDate,
        AttendanceStudentListRequest request)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<PaginatedResponse<List<AttendanceStudentRowDto>>>.Failure(
                _localizer, "SessionNotFound", HttpStatusCode.NotFound);

        // FIX 1.5: Use teacher's local timezone for "today" default
        var date = occurrenceDate?.Date ?? _timeZoneService.GetTeacherLocalDate(teacherId);

        // Get linked session Ids for cross-session student display (REQ-ATT-014/015)
        var linkedSessions = await _unitOfWork.SessionsRepo.GetLinkedSessionsAsync(sessionId);
        var linkedSessionIds = linkedSessions.Select(s => s.Id).ToList();

        // FIX 1.2: Database-side pagination — replaces in-memory loading of all students.
        // Previously loaded ALL students (primary + linked) into memory, filtered/sorted in C#,
        // then paginated. For REQ-ATT-036's target of 50,000 students, this caused OOM/timeout.
        // Now pushes search, filter, sort, and pagination to SQL via GetPagedAttendanceStudentListAsync.
        var (items, totalCount) = await _unitOfWork.AttendanceRepo.GetPagedAttendanceStudentListAsync(
            teacherId, sessionId, date, linkedSessionIds,
            request.Search, request.UnmarkedOnly,
            request.Page, request.PageSize);

        var dtos = items.Select(row => new AttendanceStudentRowDto
        {
            TeacherStudentId = row.TeacherStudentId,
            StudentName = row.StudentName,
            StudentCode = row.StudentCode,
            CurrentStatus = row.CurrentStatus,
            IsMarked = row.IsMarked,
            IsHeld = false,
            IsCrossSessionStudent = row.IsFromLinkedSession,
            SourceSessionName = row.SourceSessionName,
            ConsecutiveAbsences = row.ConsecutiveAbsences,
            TotalAbsences = row.TotalAbsences
        }).ToList();

        var response = new PaginatedResponse<List<AttendanceStudentRowDto>>
        {
            totalCount = totalCount,
            page = request.Page,
            pageSize = request.PageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
            data = dtos
        };

        return Result<PaginatedResponse<List<AttendanceStudentRowDto>>>.Success(response, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<MarkAttendanceResultDto>> MarkAttendanceAsync(MarkAttendanceDto dto)
    {
        // 1. Validate teacher
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(dto.TeacherId);
        if (teacher is null)
            return Result<MarkAttendanceResultDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // 2. Validate session
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionId, dto.TeacherId);
        if (session is null)
            return Result<MarkAttendanceResultDto>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        // 3. Validate student exists
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(dto.TeacherStudentId, dto.TeacherId);
        if (student is null)
            return Result<MarkAttendanceResultDto>.Failure(_localizer, "StudentNotFound", HttpStatusCode.NotFound);

        // FIX 1.5: Use teacher's local timezone instead of UTC
        var date = dto.OccurrenceDate?.Date ?? _timeZoneService.GetTeacherLocalDate(dto.TeacherId);

        // 4. Get or validate occurrence
        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(dto.SessionId, date);
        if (occurrence is null)
            return Result<MarkAttendanceResultDto>.Failure(_localizer, "AttendanceNoOccurrenceToday", HttpStatusCode.BadRequest);

        // 5. BR-ATT-002 / REQ-ATT-069: Check for duplicate attendance
        var existingRecord = await _unitOfWork.AttendanceRepo
            .GetExistingAttendanceAsync(dto.TeacherStudentId, occurrence.Id);
        if (existingRecord is not null)
        {
            return Result<MarkAttendanceResultDto>.Success(new MarkAttendanceResultDto
            {
                Record = null,
                IsDuplicate = true,
                DuplicateSessionName = existingRecord.SessionName,
                DuplicateRecordedAt = existingRecord.RecordedAt
            }, _localizer, "AttendanceDuplicateDetected");
        }

        // REQ-ATT-069: Check cross-session duplicates across linked sessions
        var linkedSessions = await _unitOfWork.SessionsRepo.GetLinkedSessionsAsync(dto.SessionId);
        // FIX 3.3: Include the student's own assigned session in the duplicate check set
        var allLinkedSessionIds = linkedSessions.Select(ls => ls.Id).Append(dto.SessionId).ToList();

        // 7. Determine if this is a cross-session attendance (check assignment BEFORE dup check)
        var activeAssignment = await _unitOfWork.AttendanceRepo
            .GetActiveAssignmentAsync(dto.TeacherStudentId);

        bool isCrossSession = activeAssignment is not null
            && activeAssignment.SessionId.HasValue
            && activeAssignment.SessionId.Value != dto.SessionId;

        // FIX 3.3: Also include the student's own assigned session in duplicate check
        if (isCrossSession && activeAssignment!.SessionId.HasValue
            && !allLinkedSessionIds.Contains(activeAssignment.SessionId.Value))
        {
            allLinkedSessionIds.Add(activeAssignment.SessionId.Value);
        }

        var crossDuplicate = await _unitOfWork.AttendanceRepo
            .GetExistingAttendanceByStudentAndDateAsync(dto.TeacherStudentId, date, allLinkedSessionIds);
        if (crossDuplicate is not null)
        {
            return Result<MarkAttendanceResultDto>.Success(new MarkAttendanceResultDto
            {
                Record = null,
                IsDuplicate = true,
                DuplicateSessionName = crossDuplicate.SessionName,
                DuplicateRecordedAt = crossDuplicate.RecordedAt
            }, _localizer, "AttendanceDuplicateDetected");
        }

        // 6. REQ-ATT-027/028/029: Check absence history for alert
        var absenceCounter = await _unitOfWork.AttendanceRepo
            .GetAbsenceCounterAsync(dto.TeacherId, dto.TeacherStudentId);

        var result = new MarkAttendanceResultDto();
        if (absenceCounter is not null && absenceCounter.ConsecutiveAbsences > 0)
        {
            result.HasAbsenceAlert = true;
            result.ConsecutiveAbsences = absenceCounter.ConsecutiveAbsences;
            result.LastAbsenceDate = absenceCounter.LastAbsenceDate;
            result.LastAbsenceSessionName = absenceCounter.LastAbsenceSessionName;
            result.LastAbsenceWasCrossSession = absenceCounter.LastAbsenceSessionId.HasValue
                && absenceCounter.LastAbsenceSessionId != dto.SessionId;

            // If alert not confirmed and student was absent, require confirmation
            if (!dto.AbsenceAlertConfirmed && dto.Status == AttendanceStatus.Present)
            {
                result.Record = null;
                return Result<MarkAttendanceResultDto>.Success(result, _localizer, "AttendanceAbsenceAlertPending");
            }
        }

        // Validate cross-session: BR-ATT-003 — only allowed between linked sessions
        if (isCrossSession)
        {
            bool isLinked = linkedSessions.Any(ls => ls.Id == activeAssignment!.SessionId!.Value);
            if (!isLinked)
                return Result<MarkAttendanceResultDto>.Failure(
                    _localizer, "AttendanceCrossSessionNotLinked", HttpStatusCode.BadRequest);
        }

        // 8. Create attendance record — transactional with counter update
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Find assignment for this session (primary or cross-session)
            long assignmentId;
            if (isCrossSession && activeAssignment is not null)
            {
                assignmentId = activeAssignment.Id;
            }
            else
            {
                var directAssignment = await _unitOfWork.AttendanceRepo.GetActiveAssignmentAsync(dto.TeacherStudentId);
                if (directAssignment is null)
                    return Result<MarkAttendanceResultDto>.Failure(
                        _localizer, "AttendanceStudentNotAssigned", HttpStatusCode.BadRequest);
                assignmentId = directAssignment.Id;
            }

            var attendanceStatus = isCrossSession && dto.Status == AttendanceStatus.Present
                ? AttendanceStatus.CrossSessionPresent
                : dto.Status;

            // FIX 3.1: Remap OccurrenceDate to student's own session's next occurrence (REQ-ATT-018).
            // For cross-session: if student from Session B (Sunday) attends Session A (Saturday),
            // the record should appear on Sunday's date slot in Session B's history.
            var recordOccurrenceDate = date;
            if (isCrossSession && activeAssignment is not null && activeAssignment.SessionId.HasValue)
            {
                var nextOcc = await _unitOfWork.AttendanceRepo
                    .GetNextOccurrenceAsync(activeAssignment.SessionId.Value, date);
                if (nextOcc is not null)
                    recordOccurrenceDate = nextOcc.OccurrenceDate;
            }

            var record = new AttendanceRecord
            {
                TeacherId = dto.TeacherId,
                SessionOccurrenceId = occurrence.Id,
                TeacherStudentId = dto.TeacherStudentId,
                StudentSessionAssignmentId = assignmentId,
                // FIX 3.1: For cross-session, store the student's ASSIGNED session (not the physical one)
                // so timeline queries filtering by SessionId show this record correctly.
                SessionId = isCrossSession && activeAssignment is not null
                    ? activeAssignment.SessionId : dto.SessionId,
                SessionName = isCrossSession && activeAssignment is not null
                    ? activeAssignment.SessionName : session.SessionName,
                // FIX 3.1: Use the remapped date (student's own session occurrence) not the physical date
                OccurrenceDate = recordOccurrenceDate,
                Status = attendanceStatus,
                AttendanceMethod = dto.AttendanceMethod,
                IsCrossSession = isCrossSession,
                // Cross-session fields: the session the student PHYSICALLY attended
                CrossSessionId = isCrossSession ? dto.SessionId : null,
                CrossSessionName = isCrossSession ? session.SessionName : null,
                CrossSessionOccurrenceDate = isCrossSession ? date : null,
                RecordedAt = DateTime.UtcNow,
                RecordedByUserId = dto.RecordedByUserId,
                IsEdited = false,
                CreateAt = DateTime.UtcNow
            };

            await _unitOfWork.AttendanceRepo.AddAttendanceRecordAsync(record);

            // 9. Update absence counter
            await UpdateAbsenceCounterForNewRecord(dto.TeacherId, dto.TeacherStudentId,
                attendanceStatus, date, session.SessionName, dto.SessionId);

            // 10. Update occurrence status
            await UpdateOccurrenceStatusAsync(occurrence, dto.SessionId);

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            result.Record = MapToRecordDto(record, student.StudentName, student.StudentCode);

            return Result<MarkAttendanceResultDto>.Success(result, _localizer, "AttendanceMarkedSuccess");
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<BulkMarkAttendanceResultDto>> BulkMarkAttendanceAsync(BulkMarkAttendanceDto dto)
    {
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(dto.TeacherId);
        if (teacher is null)
            return Result<BulkMarkAttendanceResultDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionId, dto.TeacherId);
        if (session is null)
            return Result<BulkMarkAttendanceResultDto>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        // FIX 1.5: Use teacher's local timezone
        var date = dto.OccurrenceDate?.Date ?? _timeZoneService.GetTeacherLocalDate(dto.TeacherId);
        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(dto.SessionId, date);
        if (occurrence is null)
            return Result<BulkMarkAttendanceResultDto>.Failure(
                _localizer, "AttendanceNoOccurrenceToday", HttpStatusCode.BadRequest);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            int successCount = 0;
            int skippedCount = 0;
            var absenceAlerts = new List<AbsenceAlertStudentDto>();

            // FIX 2.2: Batch-load all data before the loop to eliminate N+1 queries.
            // Previously each iteration made 4 DB calls (student, existing, assignment, counter).
            // For "Mark All Present" with 500 students, that was 2000+ queries.
            // Now: 4 batch queries total regardless of student count.
            var allStudents = await _unitOfWork.Students
                .GetActiveByIdsAndTeacherAsync(dto.TeacherId, dto.TeacherStudentIds);
            var studentMap = allStudents.ToDictionary(s => s.Id);
            var existingStudentIds = await _unitOfWork.AttendanceRepo
                .GetExistingAttendanceBatchAsync(dto.TeacherStudentIds, occurrence.Id);
            var assignmentMap = await _unitOfWork.AttendanceRepo
                .GetActiveAssignmentsBatchAsync(dto.TeacherStudentIds);
            var counterMap = await _unitOfWork.AttendanceRepo
                .GetAbsenceCountersBatchAsync(dto.TeacherId, dto.TeacherStudentIds);

            foreach (var studentId in dto.TeacherStudentIds)
            {
                // All lookups are O(1) dictionary hits — no DB calls inside the loop
                if (!studentMap.TryGetValue(studentId, out var student))
                {
                    skippedCount++;
                    continue;
                }

                // Check duplicate
                if (existingStudentIds.Contains(studentId))
                {
                    skippedCount++;
                    continue;
                }

                // Get assignment
                if (!assignmentMap.TryGetValue(studentId, out var assignment))
                {
                    skippedCount++;
                    continue;
                }

                // Check absence counter for alerts (from pre-loaded map)
                counterMap.TryGetValue(studentId, out var counter);
                if (counter is not null && counter.ConsecutiveAbsences > 0)
                {
                    absenceAlerts.Add(new AbsenceAlertStudentDto
                    {
                        TeacherStudentId = studentId,
                        StudentName = student.StudentName,
                        StudentCode = student.StudentCode,
                        ConsecutiveAbsences = counter.ConsecutiveAbsences,
                        LastAbsenceDate = counter.LastAbsenceDate,
                        LastAbsenceSessionName = counter.LastAbsenceSessionName,
                        WasCrossSession = counter.LastAbsenceSessionId.HasValue
                            && counter.LastAbsenceSessionId != dto.SessionId
                    });
                }

                var record = new AttendanceRecord
                {
                    TeacherId = dto.TeacherId,
                    SessionOccurrenceId = occurrence.Id,
                    TeacherStudentId = studentId,
                    StudentSessionAssignmentId = assignment.Id,
                    SessionId = dto.SessionId,
                    SessionName = session.SessionName,
                    OccurrenceDate = date,
                    Status = dto.Status,
                    AttendanceMethod = dto.AttendanceMethod,
                    IsCrossSession = false,
                    RecordedAt = DateTime.UtcNow,
                    RecordedByUserId = dto.RecordedByUserId,
                    IsEdited = false,
                    CreateAt = DateTime.UtcNow
                };

                await _unitOfWork.AttendanceRepo.AddAttendanceRecordAsync(record);
                await UpdateAbsenceCounterForNewRecord(dto.TeacherId, studentId,
                    dto.Status, date, session.SessionName, dto.SessionId);

                successCount++;
            }

            // Update occurrence status
            await UpdateOccurrenceStatusAsync(occurrence, dto.SessionId);

            // FIX 3.4: Count totals BEFORE commit to avoid race condition.
            // Previously the query ran after CommitAsync, so another concurrent request
            // could modify the data between commit and count.
            await _unitOfWork.SaveChangesAsync();

            var allRecords = await _unitOfWork.AttendanceRepo.GetRecordsByOccurrenceAsync(occurrence.Id);
            int totalPresent = allRecords.Count(r => r.Status == AttendanceStatus.Present
                || r.Status == AttendanceStatus.CrossSessionPresent);
            int totalAbsent = allRecords.Count(r => r.Status == AttendanceStatus.Absent);

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var resultDto = new BulkMarkAttendanceResultDto
            {
                SuccessCount = successCount,
                SkippedCount = skippedCount,
                AbsenceAlertCount = absenceAlerts.Count,
                AbsenceAlerts = absenceAlerts,
                TotalPresent = totalPresent,
                TotalAbsent = totalAbsent
            };

            return Result<BulkMarkAttendanceResultDto>.Success(resultDto, _localizer, "AttendanceBulkMarkedSuccess");
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════
    // EDIT ATTENDANCE (REQ-ATT-023 through 026)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    /// FIX 2.1: Replaced N+1 loop with single batch query for record counts.
    public async Task<Result<List<OccurrenceCalendarItemDto>>> GetOccurrenceCalendarAsync(
        long teacherId, long sessionId)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<List<OccurrenceCalendarItemDto>>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        var occurrences = await _unitOfWork.AttendanceRepo.GetOccurrencesBySessionAsync(sessionId);
        var activeAssignments = await _unitOfWork.AttendanceRepo.GetActiveAssignmentsBySessionAsync(sessionId);
        int totalStudents = activeAssignments.Count;

        // FIX 2.1: Single batch query instead of N+1 per occurrence.
        // Previously each occurrence was individually queried for its record count in a foreach loop.
        // For a session with 52 weekly occurrences, that was 52 additional DB queries.
        var occurrenceIds = occurrences.Select(o => o.Id).ToList();
        var recordCounts = await _unitOfWork.AttendanceRepo
            .CountRecordsByOccurrenceBatchAsync(occurrenceIds);

        var items = occurrences.Select(occ => new OccurrenceCalendarItemDto
        {
            OccurrenceId = occ.Id,
            OccurrenceDate = occ.OccurrenceDate,
            Status = occ.Status,
            MarkedCount = recordCounts.GetValueOrDefault(occ.Id, 0),
            TotalStudents = totalStudents
        }).ToList();

        return Result<List<OccurrenceCalendarItemDto>>.Success(items, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<AttendanceRecordDto>> EditAttendanceAsync(EditAttendanceDto dto)
    {
        var record = await _unitOfWork.AttendanceRepo
            .GetAttendanceRecordByIdAsync(dto.AttendanceRecordId, dto.TeacherId);
        if (record is null)
            return Result<AttendanceRecordDto>.Failure(_localizer, "AttendanceRecordNotFound", HttpStatusCode.NotFound);

        var previousStatus = record.Status;
        var previousMethod = record.AttendanceMethod;

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // REQ-ATT-025: Log the edit
            var editLog = new AttendanceEditLog
            {
                AttendanceRecordId = record.Id,
                PreviousStatus = previousStatus,
                NewStatus = dto.NewStatus,
                PreviousAttendanceMethod = previousMethod,
                NewAttendanceMethod = previousMethod, // Method doesn't change on edit
                EditedAt = DateTime.UtcNow,
                EditedByUserId = dto.EditedByUserId,
                EditReason = dto.EditReason,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.AttendanceRepo.AddEditLogAsync(editLog);

            // Update the record
            record.Status = dto.NewStatus;
            record.IsEdited = true;
            record.LastEditedAt = DateTime.UtcNow;
            record.LastEditedByUserId = dto.EditedByUserId;
            await _unitOfWork.AttendanceRepo.UpdateAttendanceRecordAsync(record);

            // Recalculate absence counter (since edits can change history mid-stream)
            await RecalculateAbsenceCounterAfterEdit(
                dto.TeacherId, record.TeacherStudentId, previousStatus, dto.NewStatus);

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
                record.TeacherStudentId, dto.TeacherId);
            var resultDto = MapToRecordDto(record,
                student?.StudentName ?? "Unknown", student?.StudentCode ?? "");

            return Result<AttendanceRecordDto>.Success(resultDto, _localizer, "AttendanceEditedSuccess");
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<AttendanceRecordDto>> AddAttendanceRecordAsync(AddAttendanceRecordDto dto)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionId, dto.TeacherId);
        if (session is null)
            return Result<AttendanceRecordDto>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(dto.TeacherStudentId, dto.TeacherId);
        if (student is null)
            return Result<AttendanceRecordDto>.Failure(_localizer, "StudentNotFound", HttpStatusCode.NotFound);

        // Get or create occurrence
        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(dto.SessionId, dto.OccurrenceDate);

        if (occurrence is null)
        {
            // REQ-ATT-026: Create occurrence for future/past dates if it doesn't exist
            occurrence = new SessionOccurrence
            {
                TeacherId = dto.TeacherId,
                SessionId = dto.SessionId,
                OccurrenceDate = dto.OccurrenceDate.Date,
                Status = OccurrenceStatus.Pending,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.AttendanceRepo.AddOccurrenceAsync(occurrence);
            await _unitOfWork.SaveChangesAsync(); // Save to get the Id
        }

        // Check duplicate
        var existing = await _unitOfWork.AttendanceRepo
            .GetExistingAttendanceAsync(dto.TeacherStudentId, occurrence.Id);
        if (existing is not null)
            return Result<AttendanceRecordDto>.Failure(_localizer, "AttendanceDuplicateDetected", HttpStatusCode.Conflict);

        var assignment = await _unitOfWork.AttendanceRepo.GetActiveAssignmentAsync(dto.TeacherStudentId);
        if (assignment is null)
            return Result<AttendanceRecordDto>.Failure(
                _localizer, "AttendanceStudentNotAssigned", HttpStatusCode.BadRequest);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            var record = new AttendanceRecord
            {
                TeacherId = dto.TeacherId,
                SessionOccurrenceId = occurrence.Id,
                TeacherStudentId = dto.TeacherStudentId,
                StudentSessionAssignmentId = assignment.Id,
                SessionId = dto.SessionId,
                SessionName = session.SessionName,
                OccurrenceDate = dto.OccurrenceDate.Date,
                Status = dto.Status,
                AttendanceMethod = AttendanceMethod.MultiSelect, // Via Edit Attendance
                RecordedAt = DateTime.UtcNow,
                RecordedByUserId = dto.RecordedByUserId,
                IsEdited = false,
                CreateAt = DateTime.UtcNow
            };

            await _unitOfWork.AttendanceRepo.AddAttendanceRecordAsync(record);

            // FIX 3.2: Use recalculation instead of simple increment for Edit Attendance.
            // Records added via Edit Attendance can be for past or future dates (out of chronological order).
            // Simple increment of ConsecutiveAbsences would be wrong — must recalculate from actual records.
            await UpdateAbsenceCounterForAddedRecord(dto.TeacherId, dto.TeacherStudentId,
                dto.Status, dto.OccurrenceDate.Date, session.SessionName, dto.SessionId);

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var resultDto = MapToRecordDto(record, student.StudentName, student.StudentCode);
            return Result<AttendanceRecordDto>.Success(resultDto, _localizer, "AttendanceAddedSuccess");
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteAttendanceRecordAsync(DeleteAttendanceRecordDto dto)
    {
        var record = await _unitOfWork.AttendanceRepo
            .GetAttendanceRecordByIdAsync(dto.AttendanceRecordId, dto.TeacherId);
        if (record is null)
            return Result<bool>.Failure(_localizer, "AttendanceRecordNotFound", HttpStatusCode.NotFound);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _unitOfWork.AttendanceRepo.DeleteAttendanceRecordAsync(record);

            // Recalculate counter after deletion
            await RecalculateAbsenceCounterAfterEdit(
                dto.TeacherId, record.TeacherStudentId, record.Status, null);

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<bool>.Success(true, _localizer, "AttendanceDeletedSuccess");
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<AttendanceEditLogDto>>> GetEditHistoryAsync(
        long teacherId, long attendanceRecordId)
    {
        var record = await _unitOfWork.AttendanceRepo
            .GetAttendanceRecordByIdAsync(attendanceRecordId, teacherId);
        if (record is null)
            return Result<List<AttendanceEditLogDto>>.Failure(
                _localizer, "AttendanceRecordNotFound", HttpStatusCode.NotFound);

        var logs = await _unitOfWork.AttendanceRepo.GetEditLogsByRecordAsync(attendanceRecordId);
        var dtos = logs.Select(l => new AttendanceEditLogDto
        {
            Id = l.Id,
            PreviousStatus = l.PreviousStatus,
            NewStatus = l.NewStatus,
            EditedAt = l.EditedAt,
            EditedByUserId = l.EditedByUserId,
            EditReason = l.EditReason
        }).ToList();

        return Result<List<AttendanceEditLogDto>>.Success(dtos, _localizer, "Success");
    }

    // ══════════════════════════════════════════════
    // ABSENCE OVERVIEW (REQ-ATT-032 through 035)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<AbsenceOverviewStudentDto>>>> GetAbsenceOverviewAsync(
        long teacherId, long sessionId, AbsenceOverviewRequest request)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<PaginatedResponse<List<AbsenceOverviewStudentDto>>>.Failure(
                _localizer, "SessionNotFound", HttpStatusCode.NotFound);

        // Count via dedicated repo method (avoids EF Core in Application layer)
        int totalCount = await _unitOfWork.AttendanceRepo.CountAbsenceOverviewAsync(
            teacherId,
            sessionId: request.SessionId ?? sessionId,
            search: request.Search,
            missingStudentPhone: request.MissingStudentPhone ? true : null,
            missingParentPhone: request.MissingParentPhone ? true : null);

        // Get paged results via dedicated repo method (Include + Skip/Take executed in Infrastructure)
        var pagedCounters = await _unitOfWork.AttendanceRepo.GetPagedAbsenceOverviewAsync(
            teacherId,
            request.Page,
            request.PageSize,
            sessionId: request.SessionId ?? sessionId,
            search: request.Search,
            missingStudentPhone: request.MissingStudentPhone ? true : null,
            missingParentPhone: request.MissingParentPhone ? true : null);

        var dtos = new List<AbsenceOverviewStudentDto>();
        foreach (var counter in pagedCounters)
        {
            // REQ-ATT-068: Last 5 occurrence statuses
            var recentRecords = await _unitOfWork.AttendanceRepo
                .GetRecentRecordsByStudentAsync(counter.TeacherStudentId, 5);

            dtos.Add(new AbsenceOverviewStudentDto
            {
                TeacherStudentId = counter.TeacherStudentId,
                StudentName = counter.TeacherStudent.StudentName,
                StudentCode = counter.TeacherStudent.StudentCode,
                SessionId = counter.TeacherStudent.SessionId,
                ConsecutiveAbsences = counter.ConsecutiveAbsences,
                TotalAbsences = counter.TotalAbsences,
                LastAbsenceDate = counter.LastAbsenceDate,
                RecentStatuses = recentRecords.Select(r => r.Status).ToList()
            });
        }

        var response = new PaginatedResponse<List<AbsenceOverviewStudentDto>>
        {
            totalCount = totalCount,
            page = request.Page,
            pageSize = request.PageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
            data = dtos
        };

        return Result<PaginatedResponse<List<AbsenceOverviewStudentDto>>>.Success(response, _localizer, "Success");
    }

    // ══════════════════════════════════════════════
    // STUDENT ATTENDANCE TIMELINE (REQ-ATT-072-081)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<StudentAttendanceSummaryDto>>>> GetTimelineStudentListAsync(
        long teacherId, AttendanceTimelineRequest request)
    {
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<PaginatedResponse<List<StudentAttendanceSummaryDto>>>.Failure(
                _localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // Get distinct student Ids via repo method (executes EF Core in Infrastructure layer)
        var studentIds = (await _unitOfWork.AttendanceRepo.GetDistinctStudentIdsFromAssignmentsAsync(
            teacherId,
            sessionId: request.SessionId,
            sessionGroupId: request.SessionGroupId,
            studentName: request.StudentName,
            studentCode: request.StudentCode)).ToList();

        int totalCount = studentIds.Count;
        var pagedStudentIds = studentIds
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var dtos = new List<StudentAttendanceSummaryDto>();
        foreach (var studentId in pagedStudentIds)
        {
            var summary = await BuildStudentSummary(teacherId, studentId);
            if (summary is not null)
                dtos.Add(summary);
        }

        var response = new PaginatedResponse<List<StudentAttendanceSummaryDto>>
        {
            totalCount = totalCount,
            page = request.Page,
            pageSize = request.PageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
            data = dtos
        };

        return Result<PaginatedResponse<List<StudentAttendanceSummaryDto>>>.Success(
            response, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<StudentAttendanceSummaryDto>> GetStudentAttendanceSummaryAsync(
        long teacherId, long teacherStudentId)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(teacherStudentId, teacherId);
        if (student is null)
            return Result<StudentAttendanceSummaryDto>.Failure(
                _localizer, "StudentNotFound", HttpStatusCode.NotFound);

        var summary = await BuildStudentSummary(teacherId, teacherStudentId);
        if (summary is null)
            return Result<StudentAttendanceSummaryDto>.Failure(
                _localizer, "AttendanceNoAssignmentHistory", HttpStatusCode.NotFound);

        return Result<StudentAttendanceSummaryDto>.Success(summary, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<MonthlyAttendanceSummaryDto>> GetStudentTimelineMonthAsync(
        long teacherId, long teacherStudentId, StudentTimelineMonthRequest request)
    {
        var startDate = new DateTime(request.Year, request.Month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var records = await _unitOfWork.AttendanceRepo
            .GetRecordsByStudentAndDateRangeAsync(teacherStudentId, startDate, endDate);

        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(teacherStudentId, teacherId);

        var recordDtos = records.Select(r => MapToRecordDto(r,
            student?.StudentName ?? "Unknown", student?.StudentCode ?? "")).ToList();

        int totalPresent = records.Count(r => r.Status == AttendanceStatus.Present
            || r.Status == AttendanceStatus.CrossSessionPresent);
        int totalAbsent = records.Count(r => r.Status == AttendanceStatus.Absent);
        int totalOccurrences = records.Count;

        var monthSummary = new MonthlyAttendanceSummaryDto
        {
            Year = request.Year,
            Month = request.Month,
            TotalOccurrences = totalOccurrences,
            TotalPresent = totalPresent,
            TotalAbsences = totalAbsent,
            AttendancePercentage = totalOccurrences > 0
                ? Math.Round((decimal)totalPresent / totalOccurrences * 100, 1)
                : 0,
            Records = recordDtos
        };

        return Result<MonthlyAttendanceSummaryDto>.Success(monthSummary, _localizer, "Success");
    }

    // ══════════════════════════════════════════════
    // REPORTING (REQ-ATT-040 through 042)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<List<AttendanceRecordDto>>> GenerateReportAsync(
        long teacherId, AttendanceReportRequest request)
    {
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<List<AttendanceRecordDto>>.Failure(
                _localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // Determine student filter for Type 1
        long? studentFilter = null;
        if (request.ReportType == AttendanceReportType.SingleStudentAbsence
            && request.TeacherStudentId.HasValue)
        {
            studentFilter = request.TeacherStudentId.Value;
        }

        // Determine linked session Ids for Type 6
        IEnumerable<long>? linkedSessionIds = null;
        if (request.ReportType == AttendanceReportType.LinkedSessionsAttendance
            && request.SessionId.HasValue)
        {
            var linked = await _unitOfWork.SessionsRepo.GetLinkedSessionsAsync(request.SessionId.Value);
            linkedSessionIds = linked.Select(s => s.Id).Append(request.SessionId.Value).ToList();
        }

        // Execute query via repo method (Include + ToListAsync in Infrastructure layer)
        var records = await _unitOfWork.AttendanceRepo.ExecuteReportQueryAsync(
            teacherId,
            sessionId: request.SessionId,
            sessionGroupId: request.SessionGroupId,
            startDate: request.StartDate,
            endDate: request.EndDate,
            status: request.ReportType == AttendanceReportType.SingleStudentAbsence
                || request.ReportType == AttendanceReportType.SessionAbsence
                || request.ReportType == AttendanceReportType.AllSessionsAbsence
                    ? AttendanceStatus.Absent
                    : null,
            teacherStudentId: studentFilter,
            sessionIds: linkedSessionIds);

        var dtos = records.Select(r => MapToRecordDto(r,
            r.TeacherStudent?.StudentName ?? "Unknown",
            r.TeacherStudent?.StudentCode ?? "")).ToList();

        return Result<List<AttendanceRecordDto>>.Success(dtos, _localizer, "AttendanceReportGenerated");
    }

    // ══════════════════════════════════════════════
    // STUDENT/PARENT VIEW ACCESS
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<MonthlyAttendanceSummaryDto>> GetStudentViewAttendanceAsync(
        long teacherId, long teacherStudentId, StudentTimelineMonthRequest request)
    {
        // Check visibility
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        if (config is null || (!config.StudentVisibilityAttendance && !config.ParentVisibilityAttendance))
            return Result<MonthlyAttendanceSummaryDto>.Failure(
                _localizer, "AttendanceVisibilityDisabled", HttpStatusCode.Forbidden);

        return await GetStudentTimelineMonthAsync(teacherId, teacherStudentId, request);
    }

    /// <inheritdoc />
    public async Task<Result<StudentAttendanceSummaryDto>> GetStudentViewAttendanceSummaryAsync(
        long teacherId, long teacherStudentId)
    {
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        if (config is null || (!config.StudentVisibilityAttendance && !config.ParentVisibilityAttendance))
            return Result<StudentAttendanceSummaryDto>.Failure(
                _localizer, "AttendanceVisibilityDisabled", HttpStatusCode.Forbidden);

        return await GetStudentAttendanceSummaryAsync(teacherId, teacherStudentId);
    }

    // ══════════════════════════════════════════════
    // HOLD STATUS (FIX 4.1 — REQ-ATT-061)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<MarkAttendanceResultDto>> HoldStudentAsync(HoldStudentDto dto)
    {
        // TODO: FIX 4.1 — Full implementation pending
        // 1. Validate teacher, session, student
        // 2. Get or validate occurrence for the date
        // 3. Check if student is already marked (if so, reject)
        // 4. Create AttendanceRecord with Status = AttendanceStatus.Held
        // 5. Do NOT update absence counter (held is not a final state)
        // 6. Return result with hold confirmation
        return Result<MarkAttendanceResultDto>.Failure(
            _localizer, "FeatureNotImplemented", HttpStatusCode.NotImplemented);
    }

    /// <inheritdoc />
    public async Task<Result<MarkAttendanceResultDto>> ReleaseHoldAsync(ReleaseHoldDto dto)
    {
        // TODO: FIX 4.1 — Full implementation pending
        // 1. Find the held record for this student + session + occurrence
        // 2. If MarkAsPresent: change status to Present, update absence counter
        // 3. If not: delete the held record (return to unmarked)
        // 4. Return result with release confirmation
        return Result<MarkAttendanceResultDto>.Failure(
            _localizer, "FeatureNotImplemented", HttpStatusCode.NotImplemented);
    }

    // ══════════════════════════════════════════════
    // EXPORT (FIX 4.2 — REQ-ATT-041/081)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<byte[]>> ExportReportAsync(
        long teacherId, AttendanceReportRequest request, string format)
    {
        // TODO: FIX 4.2 — Implement with IAttendanceReportExportService
        // 1. Call GenerateReportAsync to get the data
        // 2. Pass to IAttendanceReportExportService for PDF/Excel generation
        // 3. Return byte array
        return Result<byte[]>.Failure(
            _localizer, "FeatureNotImplemented", HttpStatusCode.NotImplemented);
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> ExportTimelineAsync(
        long teacherId, long teacherStudentId,
        DateTime? startDate, DateTime? endDate, string format)
    {
        // TODO: FIX 4.2 — Implement with IAttendanceReportExportService
        // 1. Get student summary and monthly data for the date range
        // 2. Pass to IAttendanceReportExportService for PDF/Excel generation
        // 3. Return byte array
        return Result<byte[]>.Failure(
            _localizer, "FeatureNotImplemented", HttpStatusCode.NotImplemented);
    }

    // ══════════════════════════════════════════════
    // OFFLINE SYNC (FIX 4.3 — REQ-ATT-084/085)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<SyncResultDto>> SyncOfflineRecordsAsync(OfflineSyncRequestDto dto)
    {
        // TODO: FIX 4.3 — Full implementation pending
        // 1. For each entry in dto.Entries:
        //    a. Get or create the SessionOccurrence for the entry's date
        //    b. Check if a record already exists for this student + occurrence
        //    c. If no existing record: create normally (call MarkAttendanceAsync logic)
        //    d. If existing record with different status: return as conflict
        //    e. If existing record with same status: skip (already synced)
        // 2. Return SyncResultDto with per-entry results
        return Result<SyncResultDto>.Failure(
            _localizer, "FeatureNotImplemented", HttpStatusCode.NotImplemented);
    }

    // ══════════════════════════════════════════════
    // INTEGRATION HOOKS
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<bool>> OnStudentAssignedToSessionAsync(
        long teacherId, long teacherStudentId, long sessionId, string sessionName)
    {
        // Deactivate any existing active assignment
        var existingAssignment = await _unitOfWork.AttendanceRepo
            .GetActiveAssignmentAsync(teacherStudentId);
        if (existingAssignment is not null)
        {
            existingAssignment.IsActive = false;
            existingAssignment.UnassignedAt = DateTime.UtcNow;
            await _unitOfWork.AttendanceRepo.UpdateAssignmentAsync(existingAssignment);
        }

        // Create new assignment
        var assignment = new StudentSessionAssignment
        {
            TeacherId = teacherId,
            TeacherStudentId = teacherStudentId,
            SessionId = sessionId,
            SessionName = sessionName,
            AssignedAt = DateTime.UtcNow,
            IsActive = true,
            CreateAt = DateTime.UtcNow
        };
        await _unitOfWork.AttendanceRepo.AddAssignmentAsync(assignment);

        // Initialize absence counter if it doesn't exist
        var counter = await _unitOfWork.AttendanceRepo
            .GetAbsenceCounterAsync(teacherId, teacherStudentId);
        if (counter is null)
        {
            counter = new StudentAbsenceCounter
            {
                TeacherId = teacherId,
                TeacherStudentId = teacherStudentId,
                ConsecutiveAbsences = 0,
                TotalAbsences = 0,
                TotalPresent = 0,
                TotalOccurrences = 0,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.AttendanceRepo.AddAbsenceCounterAsync(counter);
        }

        return Result<bool>.Success(true, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<bool>> OnStudentUnassignedFromSessionAsync(
        long teacherId, long teacherStudentId)
    {
        var activeAssignment = await _unitOfWork.AttendanceRepo
            .GetActiveAssignmentAsync(teacherStudentId);
        if (activeAssignment is not null)
        {
            activeAssignment.IsActive = false;
            activeAssignment.UnassignedAt = DateTime.UtcNow;
            await _unitOfWork.AttendanceRepo.UpdateAssignmentAsync(activeAssignment);
        }

        return Result<bool>.Success(true, _localizer, "Success");
    }

    /// <inheritdoc />
    /// FIX 1.4: Added NullifySessionIdOnRecordsForSessionAsync call.
    public async Task<Result<bool>> OnSessionDeletingAsync(long teacherId, long sessionId)
    {
        // BR-ATT-005: Nullify occurrence references but preserve records
        await _unitOfWork.AttendanceRepo.NullifyOccurrenceReferencesForSessionAsync(sessionId);

        // FIX 1.4: Nullify the denormalized SessionId on AttendanceRecords.
        // Without this, SessionId points to a nonexistent row after hard-delete,
        // causing silent JOIN failures in reporting queries.
        // SessionName and OccurrenceDate remain intact for historical display.
        await _unitOfWork.AttendanceRepo.NullifySessionIdOnRecordsForSessionAsync(sessionId);

        // Deactivate all student assignments for this session
        await _unitOfWork.AttendanceRepo.DeactivateAssignmentsBySessionAsync(sessionId);

        // Delete the occurrences (records already have denormalized data)
        await _unitOfWork.AttendanceRepo.DeleteOccurrencesBySessionAsync(sessionId);

        return Result<bool>.Success(true, _localizer, "Success");
    }

    /// <inheritdoc />
    /// FIX 1.1: Previously passed teacherId=0 to GetAbsenceCounterAsync which always returned null.
    /// The entire method was a no-op. Now properly cleans up all student-related attendance metadata.
    public async Task<Result<bool>> OnStudentPermanentlyDeletedAsync(long teacherStudentId)
    {
        // FIX 1.1: Delete all absence counters for this student
        await _unitOfWork.AttendanceRepo.DeleteAbsenceCountersByStudentAsync(teacherStudentId);

        // FIX 1.1: Deactivate all session assignments for this student
        await _unitOfWork.AttendanceRepo.DeactivateAssignmentsByStudentAsync(teacherStudentId);

        // Note: AttendanceRecord rows are intentionally preserved (BR-ATT-005).
        // They retain denormalized StudentName/StudentCode for historical display
        // even after the TeacherStudent row is permanently deleted.

        return Result<bool>.Success(true, _localizer, "Success");
    }

    // ══════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Updates the StudentAbsenceCounter after a new attendance record is created.
    /// Used during LIVE attendance taking where records are always in chronological order.
    /// REQ-ATT-029/030: Consecutive counter management.
    /// REQ-ATT-021/047: Cumulative total management.
    /// </summary>
    private async Task UpdateAbsenceCounterForNewRecord(
        long teacherId, long teacherStudentId,
        AttendanceStatus status, DateTime date, string sessionName, long sessionId)
    {
        var counter = await _unitOfWork.AttendanceRepo
            .GetAbsenceCounterAsync(teacherId, teacherStudentId);

        if (counter is null)
        {
            counter = new StudentAbsenceCounter
            {
                TeacherId = teacherId,
                TeacherStudentId = teacherStudentId,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.AttendanceRepo.AddAbsenceCounterAsync(counter);
        }

        counter.TotalOccurrences++;

        if (status == AttendanceStatus.Absent)
        {
            counter.ConsecutiveAbsences++;
            counter.TotalAbsences++;
            counter.LastAbsenceDate = date;
            counter.LastAbsenceSessionName = sessionName;
            counter.LastAbsenceSessionId = sessionId;
        }
        else // Present or CrossSessionPresent
        {
            // REQ-ATT-030: Reset consecutive on any presence
            counter.ConsecutiveAbsences = 0;
            counter.TotalPresent++;
            counter.LastAttendanceDate = date;
        }

        await _unitOfWork.AttendanceRepo.UpdateAbsenceCounterAsync(counter);
    }

    /// <summary>
    /// FIX 3.2: Updates the StudentAbsenceCounter after a record is added via Edit Attendance.
    /// Unlike UpdateAbsenceCounterForNewRecord (used during live attendance taking where records
    /// are always in chronological order), this method recalculates ConsecutiveAbsences from
    /// actual records because Edit Attendance can add records for past or future dates.
    /// REQ-ATT-026: Pre-record attendance for future/past dates.
    /// </summary>
    private async Task UpdateAbsenceCounterForAddedRecord(
        long teacherId, long teacherStudentId,
        AttendanceStatus status, DateTime date, string sessionName, long sessionId)
    {
        var counter = await _unitOfWork.AttendanceRepo
            .GetAbsenceCounterAsync(teacherId, teacherStudentId);

        if (counter is null)
        {
            counter = new StudentAbsenceCounter
            {
                TeacherId = teacherId,
                TeacherStudentId = teacherStudentId,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.AttendanceRepo.AddAbsenceCounterAsync(counter);
        }

        // Update totals (these are always correct with simple increment)
        counter.TotalOccurrences++;
        if (status == AttendanceStatus.Absent)
        {
            counter.TotalAbsences++;
            // Update LastAbsenceDate only if this record is more recent
            if (!counter.LastAbsenceDate.HasValue || date > counter.LastAbsenceDate.Value)
            {
                counter.LastAbsenceDate = date;
                counter.LastAbsenceSessionName = sessionName;
                counter.LastAbsenceSessionId = sessionId;
            }
        }
        else
        {
            counter.TotalPresent++;
            if (!counter.LastAttendanceDate.HasValue || date > counter.LastAttendanceDate.Value)
                counter.LastAttendanceDate = date;
        }

        // FIX 3.2: Recalculate consecutive from actual records (not simple increment)
        // because the added record may be out of chronological order
        counter.ConsecutiveAbsences = await _unitOfWork.AttendanceRepo
            .RecalculateConsecutiveAbsencesAsync(teacherStudentId);

        await _unitOfWork.AttendanceRepo.UpdateAbsenceCounterAsync(counter);
    }

    /// <summary>
    /// Recalculates absence counter after an edit operation.
    /// Cannot use simple increment/decrement — must re-scan records.
    /// </summary>
    private async Task RecalculateAbsenceCounterAfterEdit(
        long teacherId, long teacherStudentId,
        AttendanceStatus previousStatus, AttendanceStatus? newStatus)
    {
        var counter = await _unitOfWork.AttendanceRepo
            .GetAbsenceCounterAsync(teacherId, teacherStudentId);
        if (counter is null) return;

        // Adjust totals based on status change
        if (previousStatus == AttendanceStatus.Absent && newStatus == AttendanceStatus.Present)
        {
            counter.TotalAbsences = Math.Max(0, counter.TotalAbsences - 1);
            counter.TotalPresent++;
        }
        else if (previousStatus == AttendanceStatus.Present && newStatus == AttendanceStatus.Absent)
        {
            counter.TotalPresent = Math.Max(0, counter.TotalPresent - 1);
            counter.TotalAbsences++;
        }
        else if (newStatus is null) // Deletion
        {
            counter.TotalOccurrences = Math.Max(0, counter.TotalOccurrences - 1);
            if (previousStatus == AttendanceStatus.Absent)
                counter.TotalAbsences = Math.Max(0, counter.TotalAbsences - 1);
            else
                counter.TotalPresent = Math.Max(0, counter.TotalPresent - 1);
        }

        // Recalculate consecutive from actual records
        counter.ConsecutiveAbsences = await _unitOfWork.AttendanceRepo
            .RecalculateConsecutiveAbsencesAsync(teacherStudentId);

        await _unitOfWork.AttendanceRepo.UpdateAbsenceCounterAsync(counter);
    }

    /// <summary>
    /// Updates the OccurrenceStatus based on how many students have been marked.
    /// REQ-ATT-049/051: Pending → InProgress → Completed.
    /// </summary>
    private async Task UpdateOccurrenceStatusAsync(SessionOccurrence occurrence, long sessionId)
    {
        var records = await _unitOfWork.AttendanceRepo.GetRecordsByOccurrenceAsync(occurrence.Id);
        var assignments = await _unitOfWork.AttendanceRepo.GetActiveAssignmentsBySessionAsync(sessionId);

        int markedCount = records.Count;
        int totalStudents = assignments.Count;

        if (markedCount == 0)
            occurrence.Status = OccurrenceStatus.Pending;
        else if (markedCount >= totalStudents && totalStudents > 0)
            occurrence.Status = OccurrenceStatus.Completed;
        else
            occurrence.Status = OccurrenceStatus.InProgress;

        await _unitOfWork.AttendanceRepo.UpdateOccurrenceAsync(occurrence);
    }

    /// <summary>
    /// Builds a student's full attendance summary including all assignment periods.
    /// REQ-ATT-078: All-time summary.
    /// REQ-ATT-046: Chronological assignment periods.
    /// </summary>
    private async Task<StudentAttendanceSummaryDto?> BuildStudentSummary(
        long teacherId, long teacherStudentId)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(teacherStudentId, teacherId);
        if (student is null) return null;

        var counter = await _unitOfWork.AttendanceRepo
            .GetAbsenceCounterAsync(teacherId, teacherStudentId);

        var assignments = await _unitOfWork.AttendanceRepo
            .GetAssignmentsByStudentAsync(teacherStudentId);

        var periods = assignments.Select(a => new AssignmentPeriodDto
        {
            StudentSessionAssignmentId = a.Id,
            SessionId = a.SessionId,
            SessionName = a.SessionName,
            AssignedAt = a.AssignedAt,
            UnassignedAt = a.UnassignedAt,
            IsActive = a.IsActive
        }).ToList();

        int totalOcc = counter?.TotalOccurrences ?? 0;
        int totalPresent = counter?.TotalPresent ?? 0;

        return new StudentAttendanceSummaryDto
        {
            TeacherStudentId = teacherStudentId,
            StudentName = student.StudentName,
            StudentCode = student.StudentCode,
            TotalOccurrences = totalOcc,
            TotalAbsences = counter?.TotalAbsences ?? 0,
            AttendancePercentage = totalOcc > 0
                ? Math.Round((decimal)totalPresent / totalOcc * 100, 1)
                : 0,
            ConsecutiveAbsences = counter?.ConsecutiveAbsences ?? 0,
            AssignmentPeriods = periods
        };
    }

    /// <summary>
    /// Maps an AttendanceRecord entity to its output DTO.
    /// </summary>
    private static AttendanceRecordDto MapToRecordDto(
        AttendanceRecord record, string studentName, string studentCode)
    {
        return new AttendanceRecordDto
        {
            Id = record.Id,
            TeacherStudentId = record.TeacherStudentId,
            StudentName = studentName,
            StudentCode = studentCode,
            SessionOccurrenceId = record.SessionOccurrenceId,
            SessionId = record.SessionId,
            SessionName = record.SessionName,
            OccurrenceDate = record.OccurrenceDate,
            Status = record.Status,
            AttendanceMethod = record.AttendanceMethod,
            IsCrossSession = record.IsCrossSession,
            CrossSessionId = record.CrossSessionId,
            CrossSessionName = record.CrossSessionName,
            CrossSessionOccurrenceDate = record.CrossSessionOccurrenceDate,
            RecordedAt = record.RecordedAt,
            IsEdited = record.IsEdited,
            LastEditedAt = record.LastEditedAt
        };
    }
}