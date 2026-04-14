using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Attendance;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements all Attendance Module (Module 3) operations.
///
/// CHANGES FROM ORIGINAL — ALL STEPS CONSOLIDATED:
/// Step 1.1: OnStudentPermanentlyDeletedAsync now nullifies FK references on AttendanceRecords.
/// Step 2.1: BulkMarkAttendanceAsync includes cross-session duplicate detection.
/// Step 2.3: MarkAttendanceAsync/BulkMarkAttendanceAsync/AddAttendanceRecordAsync validate status.
/// Step 3.1: HoldStudentAsync and ReleaseHoldAsync fully implemented.
/// Step 3.2: ExportReportAsync and ExportTimelineAsync fully implemented.
/// Step 3.3: SyncOfflineRecordsAsync fully implemented.
/// Step 4.1: BulkMarkAttendanceAsync uses in-memory counter mutation + batch save.
/// Step 4.2: Cross-session date remapping handles null next occurrence explicitly.
/// Step 4.3: MarkAttendanceAsync returns redirect hint when no occurrence for today.
/// Step 7.2: All record creation populates denormalized StudentName/StudentCode.
/// Step 8.1: Uses AttendanceConstants.Messages for localization keys.
///
/// AUDIT FIX CHANGES:
/// - Step 1: OnStudentAssignedToSessionAsync populates denormalized StudentName/StudentCode.
/// - Step 2: BR-ATT-001 retroactive attendance guard in MarkAttendanceAsync/AddAttendanceRecordAsync.
/// - Step 3: BulkMarkAttendanceAsync fires messaging notifications for absent students.
/// - Step 4: BulkMarkAttendanceAsync validates student assignment exists before creating record.
/// - Step 5: SyncOfflineRecordsAsync no longer auto-confirms absence alerts.
/// - Step 6: MarkAttendanceAsync adds remapped-date duplicate check for cross-session.
/// - Step 7: MarkAttendanceResultDto populated with AssignedSessionId/name for cross-session warning.
/// - Step 8: ReleaseHoldAsync guards against deleted session while student on hold.
/// - Step 9: Counter updates wrapped with DbUpdateConcurrencyException retry.
/// - Step 11: RecordedByUserId defaults to TeacherId when not provided.
/// - Step 12: GetAbsenceOverviewAsync uses OccurrenceDate for date-specific absence view.
/// - Step 14: OnStudentAssignedToSessionAsync deactivates existing assignment first (idempotent).
/// - Step 15: DeleteAttendanceRecordAsync logs deletion before removing record.
/// - Step 17: GetUnmarkedCountAsync added for REQ-ATT-055 confirmation prompt.
/// - Step 19: MarkAttendanceAsync/HoldStudentAsync distinguish soft-deleted students.
///
/// TRANSACTION SAFETY: All transactional methods use the ownsTransaction pattern.
/// </summary>
public class AttendanceService : IAttendanceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOccurrenceGeneratorService _occurrenceGenerator;
    private readonly IAttendanceReportExportService _exportService;
    private readonly IMessagingIntegrationService _messagingService;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;
    private readonly ITimeZoneService _timeZoneService;
    // FIX L1: Logger for capturing messaging errors instead of silently swallowing them.
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(
        IUnitOfWork unitOfWork,
        IOccurrenceGeneratorService occurrenceGenerator,
        IAttendanceReportExportService exportService,
        IMessagingIntegrationService messagingService,
        IStringLocalizer<Domain.Resources.Messages> localizer,
        ITimeZoneService timeZoneService,
        ILogger<AttendanceService> logger)
    {
        _unitOfWork = unitOfWork;
        _occurrenceGenerator = occurrenceGenerator;
        _exportService = exportService;
        _messagingService = messagingService;
        _localizer = localizer;
        _timeZoneService = timeZoneService;
        _logger = logger;
    }

    // ══════════════════════════════════════════════
    // SESSION OCCURRENCE MANAGEMENT
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<int>> GenerateOccurrencesAsync(long teacherId, long sessionId)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<int>.Failure(_localizer, AttendanceConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        var dates = _occurrenceGenerator.ComputeOccurrenceDates(session);
        var existingDates = await _unitOfWork.AttendanceRepo.GetExistingOccurrenceDatesAsync(sessionId);

        var newOccurrences = dates
            .Where(d => !existingDates.Contains(d))
            .Select(d => new SessionOccurrence
            {
                TeacherId = teacherId,
                SessionId = sessionId,
                OccurrenceDate = d,
                Status = OccurrenceStatus.Pending,
                CreateAt = DateTime.UtcNow
            })
            .ToList();

        if (newOccurrences.Count > 0)
        {
            await _unitOfWork.AttendanceRepo.AddOccurrencesRangeAsync(newOccurrences);
            await _unitOfWork.SaveChangesAsync();
        }

        return Result<int>.Success(newOccurrences.Count, _localizer, AttendanceConstants.Messages.Success);
    }

    // ══════════════════════════════════════════════
    // ATTENDANCE DASHBOARD
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<AttendanceDashboardDto>> GetDashboardAsync(
        long teacherId, AttendanceDashboardRequest request)
    {
        var date = request.Date?.Date ?? _timeZoneService.GetTeacherLocalDate(teacherId);

        var todayOccurrences = await _unitOfWork.AttendanceRepo
            .GetOccurrencesByTeacherAndDateAsync(teacherId, date);

        // FIX C1 (REQ-ATT-050): Batch-load MarkedCount and TotalStudents for all today's occurrences.
        // Previously hardcoded to 0 which violated REQ-ATT-050 (live counter "18 / 34").
        var occurrenceIds = todayOccurrences.Select(o => o.Id).ToList();
        var recordCounts = occurrenceIds.Count > 0
            ? await _unitOfWork.AttendanceRepo.CountRecordsByOccurrenceBatchAsync(occurrenceIds)
            : new Dictionary<long, int>();

        var sessionIds = todayOccurrences.Select(o => o.SessionId).Distinct().ToList();
        var studentCounts = sessionIds.Count > 0
            ? await _unitOfWork.AttendanceRepo.CountActiveAssignmentsBySessionBatchAsync(sessionIds)
            : new Dictionary<long, int>();

        var sessionCards = new List<AttendanceSessionCardDto>();
        foreach (var o in todayOccurrences)
        {
            var sess = o.Session;
            recordCounts.TryGetValue(o.Id, out int markedCount);
            studentCounts.TryGetValue(o.SessionId, out int totalStudents);

            sessionCards.Add(new AttendanceSessionCardDto
            {
                SessionId = o.SessionId,
                SessionName = sess?.SessionName ?? "Unknown",
                SessionGroupId = sess?.SessionGroupId,
                IsToday = true,
                TodayOccurrenceId = o.Id,
                Status = o.Status,
                MarkedCount = markedCount,
                TotalStudents = totalStudents,
                StartTime = sess?.StartTime ?? TimeSpan.Zero
            });
        }

        var dashboard = new AttendanceDashboardDto
        {
            Date = date,
            TotalSessionsToday = todayOccurrences.Count,
            CompletedSessions = todayOccurrences.Count(o => o.Status == OccurrenceStatus.Completed),
            PendingSessions = todayOccurrences.Count(o => o.Status == OccurrenceStatus.Pending),
            InProgressSessions = todayOccurrences.Count(o => o.Status == OccurrenceStatus.InProgress),
            SessionCards = sessionCards
        };

        return Result<AttendanceDashboardDto>.Success(dashboard, _localizer, AttendanceConstants.Messages.Success);
    }

    // ══════════════════════════════════════════════
    // TAKE ATTENDANCE
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<AttendanceStudentRowDto>>>> GetAttendanceStudentListAsync(
        long teacherId, long sessionId, DateTime? occurrenceDate,
        AttendanceStudentListRequest request)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<PaginatedResponse<List<AttendanceStudentRowDto>>>.Failure(
                _localizer, AttendanceConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        var date = occurrenceDate?.Date ?? _timeZoneService.GetTeacherLocalDate(teacherId);

        var linkedSessions = await _unitOfWork.SessionsRepo.GetLinkedSessionsAsync(sessionId);
        var linkedSessionIds = linkedSessions.Select(s => s.Id).ToList();

        var (items, totalCount) = await _unitOfWork.AttendanceRepo.GetPagedAttendanceStudentListAsync(
            teacherId, sessionId, date, linkedSessionIds,
            request.Search, request.UnmarkedOnly,
            request.Page, request.PageSize);

        var dtos = items.Select(row => new AttendanceStudentRowDto
        {
            TeacherStudentId = row.TeacherStudentId,
            StudentName = row.StudentName,
            StudentCode = row.StudentCode,
            Barcode = row.StudentCode, // FIX L7: REQ-ATT-009 — barcode encodes the student's unique code
            CurrentStatus = row.CurrentStatus,
            IsMarked = row.IsMarked,
            IsHeld = row.CurrentStatus == AttendanceStatus.Held, // Step 3.1: Held indicator
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

        return Result<PaginatedResponse<List<AttendanceStudentRowDto>>>.Success(
            response, _localizer, AttendanceConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<MarkAttendanceResultDto>> MarkAttendanceAsync(MarkAttendanceDto dto)
    {
        // AUDIT FIX Step 11: Default RecordedByUserId to TeacherId for audit trail
        dto.RecordedByUserId ??= dto.TeacherId;

        // Step 2.3: Validate status — only Present and Absent allowed via this endpoint
        if (dto.Status == AttendanceStatus.Held || dto.Status == AttendanceStatus.CrossSessionPresent)
            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.InvalidAttendanceStatus, HttpStatusCode.BadRequest);

        // 1. Validate teacher
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(dto.TeacherId);
        if (teacher is null)
            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.TeacherNotFound, HttpStatusCode.NotFound);

        // 2. Validate session
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionId, dto.TeacherId);
        if (session is null)
            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        // 3. Validate student exists
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
            dto.TeacherStudentId, dto.TeacherId);
        // AUDIT FIX Step 19: Distinguish "not found" from "in recycle bin"
        if (student is null)
        {
            var deletedStudent = await _unitOfWork.Students.GetByIdAndTeacherIgnoreFiltersAsync(
                dto.TeacherStudentId, dto.TeacherId);
            if (deletedStudent is not null && deletedStudent.IsDeleted)
                return Result<MarkAttendanceResultDto>.Failure(
                    _localizer, AttendanceConstants.Messages.AttendanceStudentInRecycleBin,
                    HttpStatusCode.BadRequest);

            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.StudentNotFound, HttpStatusCode.NotFound);
        }

        var date = dto.OccurrenceDate?.Date ?? _timeZoneService.GetTeacherLocalDate(dto.TeacherId);

        // 4. Get or validate occurrence
        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(dto.SessionId, date);

        // Step 4.3: Return redirect hint when no occurrence for today
        if (occurrence is null)
            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.NoOccurrenceRedirectToEdit, HttpStatusCode.BadRequest);

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
            }, _localizer, AttendanceConstants.Messages.AttendanceDuplicateDetected);
        }

        // REQ-ATT-069: Check cross-session duplicates across linked sessions
        var linkedSessions = await _unitOfWork.SessionsRepo.GetLinkedSessionsAsync(dto.SessionId);
        var allLinkedSessionIds = linkedSessions.Select(ls => ls.Id).Append(dto.SessionId).ToList();

        // 7. Determine if this is cross-session attendance
        var activeAssignment = await _unitOfWork.AttendanceRepo
            .GetActiveAssignmentAsync(dto.TeacherStudentId);

        bool isCrossSession = activeAssignment is not null
            && activeAssignment.SessionId.HasValue
            && activeAssignment.SessionId.Value != dto.SessionId;

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
            }, _localizer, AttendanceConstants.Messages.AttendanceDuplicateDetected);
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

            if (!dto.AbsenceAlertConfirmed && dto.Status == AttendanceStatus.Present)
            {
                result.Record = null;
                return Result<MarkAttendanceResultDto>.Success(
                    result, _localizer, AttendanceConstants.Messages.AttendanceAbsenceAlertPending);
            }
        }

        // Validate cross-session: BR-ATT-003
        if (isCrossSession)
        {
            bool isLinked = linkedSessions.Any(ls => ls.Id == activeAssignment!.SessionId!.Value);
            if (!isLinked)
                return Result<MarkAttendanceResultDto>.Failure(
                    _localizer, AttendanceConstants.Messages.AttendanceCrossSessionNotLinked,
                    HttpStatusCode.BadRequest);
        }

        // AUDIT FIX Step 7 (REQ-ATT-013): Populate assigned session info for cross-session warning
        if (isCrossSession && activeAssignment?.SessionId != null)
        {
            result.AssignedSessionId = activeAssignment.SessionId;
            result.AssignedSessionName = activeAssignment.SessionName;
        }

        // 8. Determine the record's occurrence date (cross-session remapping)
        var attendanceStatus = isCrossSession ? AttendanceStatus.CrossSessionPresent : dto.Status;
        var recordOccurrenceDate = date;

        if (isCrossSession && activeAssignment?.SessionId != null)
        {
            var nextOccurrence = await _unitOfWork.AttendanceRepo
                .GetNextOccurrenceAsync(activeAssignment.SessionId.Value, date);

            // Step 4.2: Explicit null handling for cross-session date remapping
            if (nextOccurrence is null)
                return Result<MarkAttendanceResultDto>.Failure(
                    _localizer, AttendanceConstants.Messages.CrossSessionNoFutureOccurrence,
                    HttpStatusCode.BadRequest);

            recordOccurrenceDate = nextOccurrence.OccurrenceDate;

            // AUDIT FIX Step 6: Check for duplicate on the REMAPPED date.
            // The earlier cross-session check used the physical date. But records are stored
            // with the remapped date. This catches duplicates stored under the remapped date.
            var remappedDuplicate = await _unitOfWork.AttendanceRepo
                .GetExistingAttendanceByStudentSessionAndDateAsync(
                    dto.TeacherStudentId, activeAssignment.SessionId!.Value, recordOccurrenceDate);
            if (remappedDuplicate is not null)
            {
                return Result<MarkAttendanceResultDto>.Success(new MarkAttendanceResultDto
                {
                    Record = null,
                    IsDuplicate = true,
                    DuplicateSessionName = remappedDuplicate.SessionName,
                    DuplicateRecordedAt = remappedDuplicate.RecordedAt
                }, _localizer, AttendanceConstants.Messages.AttendanceDuplicateDetected);
            }
        }

        // Get assignment for non-cross-session case
        var assignment = isCrossSession ? activeAssignment
            : await _unitOfWork.AttendanceRepo.GetActiveAssignmentAsync(dto.TeacherStudentId);

        if (assignment is null)
            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceStudentNotAssigned,
                HttpStatusCode.BadRequest);

        // AUDIT FIX Step 2 (BR-ATT-001): No retroactive attendance before assignment date
        if (recordOccurrenceDate.Date < assignment.AssignedAt.Date)
            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceBeforeAssignmentDate,
                HttpStatusCode.BadRequest);

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
                // Step 7.2: Denormalized student fields
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                SessionId = isCrossSession && activeAssignment is not null
                    ? activeAssignment.SessionId : dto.SessionId,
                SessionName = isCrossSession && activeAssignment is not null
                    ? activeAssignment.SessionName : session.SessionName,
                // FIX H3: Denormalized SessionGroupId survives session hard-delete (BR-ATT-005).
                // Enables Report Type 5 (SessionGroupAttendance) for deleted sessions.
                SessionGroupId = session.SessionGroupId,
                OccurrenceDate = recordOccurrenceDate,
                Status = attendanceStatus,
                AttendanceMethod = dto.AttendanceMethod,
                IsCrossSession = isCrossSession,
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

            // Step 7.1: Fire messaging notification for absence events
            if (attendanceStatus == AttendanceStatus.Absent)
            {
                var updatedCounter = await _unitOfWork.AttendanceRepo
                    .GetAbsenceCounterAsync(dto.TeacherId, dto.TeacherStudentId);
                // FIX L1: Log errors from messaging instead of silently discarding.
                _ = SafeFireMessagingAsync(() => _messagingService.NotifyStudentAbsenceAsync(
                    dto.TeacherId, dto.TeacherStudentId, student.StudentName,
                    updatedCounter?.ConsecutiveAbsences ?? 1, session.SessionName, date));

                // FIX M1: Fire consecutive absence threshold notification.
                // REQ-ATT-029: The system shall track consecutive absence streaks.
                // NotifyConsecutiveAbsenceThresholdAsync was defined but never called.
                if (updatedCounter is not null && updatedCounter.ConsecutiveAbsences >= 3)
                {
                    _ = SafeFireMessagingAsync(() => _messagingService.NotifyConsecutiveAbsenceThresholdAsync(
                        dto.TeacherId, dto.TeacherStudentId, student.StudentName,
                        updatedCounter.ConsecutiveAbsences, session.SessionName));
                }
            }

            // FIX H8: SaveChanges BEFORE UpdateOccurrenceStatusAsync.
            // Previously the occurrence status query ran before SaveChanges,
            // so the just-added record wasn't yet in the database and the
            // AsNoTracking query couldn't see it. This caused the status
            // to always be "one mark behind".
            await _unitOfWork.SaveChangesAsync();

            // 10. Update occurrence status — now sees the newly saved record
            await UpdateOccurrenceStatusAsync(occurrence, dto.SessionId);

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            result.Record = MapToRecordDto(record, student.StudentName, student.StudentCode);

            return Result<MarkAttendanceResultDto>.Success(
                result, _localizer, AttendanceConstants.Messages.AttendanceMarkedSuccess);
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
        // AUDIT FIX Step 11: Default RecordedByUserId to TeacherId for audit trail
        dto.RecordedByUserId ??= dto.TeacherId;

        // Step 2.3: Validate status
        if (dto.Status == AttendanceStatus.Held || dto.Status == AttendanceStatus.CrossSessionPresent)
            return Result<BulkMarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.InvalidAttendanceStatus, HttpStatusCode.BadRequest);

        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(dto.TeacherId);
        if (teacher is null)
            return Result<BulkMarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.TeacherNotFound, HttpStatusCode.NotFound);

        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionId, dto.TeacherId);
        if (session is null)
            return Result<BulkMarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        var date = dto.OccurrenceDate?.Date ?? _timeZoneService.GetTeacherLocalDate(dto.TeacherId);
        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(dto.SessionId, date);
        if (occurrence is null)
            return Result<BulkMarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceNoOccurrenceToday, HttpStatusCode.BadRequest);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            int successCount = 0;
            int skippedCount = 0;
            var absenceAlerts = new List<AbsenceAlertStudentDto>();

            // FIX 2.2: Batch-load all data before the loop
            var allStudents = await _unitOfWork.Students
                .GetActiveByIdsAndTeacherAsync(dto.TeacherId, dto.TeacherStudentIds);
            var studentMap = allStudents.ToDictionary(s => s.Id);
            var existingStudentIds = await _unitOfWork.AttendanceRepo
                .GetExistingAttendanceBatchAsync(dto.TeacherStudentIds, occurrence.Id);
            var assignmentMap = await _unitOfWork.AttendanceRepo
                .GetActiveAssignmentsBatchAsync(dto.TeacherStudentIds);
            var counterMap = await _unitOfWork.AttendanceRepo
                .GetAbsenceCountersBatchAsync(dto.TeacherId, dto.TeacherStudentIds);

            // Step 2.1: Batch cross-session duplicate check
            var linkedSessions = await _unitOfWork.SessionsRepo.GetLinkedSessionsAsync(dto.SessionId);
            var allLinkedSessionIds = linkedSessions.Select(ls => ls.Id).Append(dto.SessionId).ToList();
            var crossSessionDuplicates = await _unitOfWork.AttendanceRepo
                .GetExistingAttendanceByStudentsAndDateAsync(dto.TeacherStudentIds, date, allLinkedSessionIds);

            // Step 4.1: Track modified counters for batch save
            var modifiedCounters = new List<StudentAbsenceCounter>();

            foreach (var studentId in dto.TeacherStudentIds)
            {
                if (!studentMap.TryGetValue(studentId, out var student))
                {
                    skippedCount++;
                    continue;
                }

                // Check same-occurrence duplicate
                if (existingStudentIds.Contains(studentId))
                {
                    skippedCount++;
                    continue;
                }

                // Step 2.1: Check cross-session duplicate
                if (crossSessionDuplicates.ContainsKey(studentId))
                {
                    skippedCount++;
                    continue;
                }

                // AUDIT FIX Step 4 (BR-ATT-001): Skip students with no active assignment
                if (!assignmentMap.TryGetValue(studentId, out var assignment))
                {
                    skippedCount++;
                    continue;
                }

                // FIX H6 (BR-ATT-001): No retroactive attendance before assignment date.
                // The single-mark path (MarkAttendanceAsync) had this guard but bulk path did not.
                if (date.Date < assignment.AssignedAt.Date)
                {
                    skippedCount++;
                    continue;
                }

                // Check absence counter for alerts
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
                    // Step 7.2: Denormalized student fields
                    StudentName = student.StudentName,
                    StudentCode = student.StudentCode,
                    SessionId = dto.SessionId,
                    SessionName = session.SessionName,
                    // FIX H3: Denormalized SessionGroupId survives session hard-delete (BR-ATT-005).
                    SessionGroupId = session.SessionGroupId,
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

                // Step 4.1: Update counter in memory instead of DB call
                if (counter is null)
                {
                    counter = new StudentAbsenceCounter
                    {
                        TeacherId = dto.TeacherId,
                        TeacherStudentId = studentId,
                        CreateAt = DateTime.UtcNow
                    };
                    await _unitOfWork.AttendanceRepo.AddAbsenceCounterAsync(counter);
                }

                counter.TotalOccurrences++;
                if (dto.Status == AttendanceStatus.Absent)
                {
                    counter.ConsecutiveAbsences++;
                    counter.TotalAbsences++;
                    counter.LastAbsenceDate = date;
                    counter.LastAbsenceSessionName = session.SessionName;
                    counter.LastAbsenceSessionId = dto.SessionId;
                }
                else
                {
                    counter.ConsecutiveAbsences = 0;
                    counter.TotalPresent++;
                    counter.LastAttendanceDate = date;
                }

                if (!modifiedCounters.Contains(counter))
                    modifiedCounters.Add(counter);

                successCount++;
            }

            // Step 4.1: Batch save all modified counters
            if (modifiedCounters.Count > 0)
                await _unitOfWork.AttendanceRepo.UpdateAbsenceCountersRangeAsync(modifiedCounters);

            // AUDIT FIX Step 3: Fire messaging notifications for absent students
            // REQ-ATT-027/028: Absence alerts must fire for all attendance methods including multi-select
            if (dto.Status == AttendanceStatus.Absent)
            {
                foreach (var alert in absenceAlerts)
                {
                    // FIX L1: Log errors from messaging instead of silently discarding.
                    _ = SafeFireMessagingAsync(() => _messagingService.NotifyStudentAbsenceAsync(
                        dto.TeacherId, alert.TeacherStudentId, alert.StudentName,
                        alert.ConsecutiveAbsences, session.SessionName, date));
                }
            }

            // FIX H8: SaveChanges BEFORE UpdateOccurrenceStatusAsync.
            // The occurrence status query uses AsNoTracking, so it needs the records
            // flushed to the database first to get an accurate count.
            await _unitOfWork.SaveChangesAsync();

            // Update occurrence status — now sees all newly saved records
            await UpdateOccurrenceStatusAsync(occurrence, dto.SessionId);

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

            return Result<BulkMarkAttendanceResultDto>.Success(
                resultDto, _localizer, AttendanceConstants.Messages.AttendanceBulkMarkedSuccess);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════
    // UNMARKED COUNT (AUDIT FIX Step 17 — REQ-ATT-055)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<int>> GetUnmarkedCountAsync(
        long teacherId, long sessionId, DateTime? occurrenceDate)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<int>.Failure(
                _localizer, AttendanceConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        var date = occurrenceDate?.Date ?? _timeZoneService.GetTeacherLocalDate(teacherId);

        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(sessionId, date);
        if (occurrence is null)
            return Result<int>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceNoOccurrenceToday, HttpStatusCode.BadRequest);

        var assignments = await _unitOfWork.AttendanceRepo
            .GetActiveAssignmentsBySessionAsync(sessionId);
        int totalStudents = assignments.Count;

        var records = await _unitOfWork.AttendanceRepo
            .GetRecordsByOccurrenceAsync(occurrence.Id);
        // Step 3.1: Exclude Held records from "marked" count
        int markedCount = records.Count(r => r.Status != AttendanceStatus.Held);

        int unmarkedCount = Math.Max(0, totalStudents - markedCount);
        return Result<int>.Success(unmarkedCount, _localizer, AttendanceConstants.Messages.Success);
    }

    // ══════════════════════════════════════════════
    // HOLD STATUS (Step 3.1)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<MarkAttendanceResultDto>> HoldStudentAsync(HoldStudentDto dto)
    {
        // AUDIT FIX Step 11: Default RecordedByUserId to TeacherId for audit trail
        dto.RecordedByUserId ??= dto.TeacherId;

        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(dto.TeacherId);
        if (teacher is null)
            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.TeacherNotFound, HttpStatusCode.NotFound);

        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionId, dto.TeacherId);
        if (session is null)
            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
            dto.TeacherStudentId, dto.TeacherId);
        // AUDIT FIX Step 19: Distinguish "not found" from "in recycle bin"
        if (student is null)
        {
            var deletedStudent = await _unitOfWork.Students.GetByIdAndTeacherIgnoreFiltersAsync(
                dto.TeacherStudentId, dto.TeacherId);
            if (deletedStudent is not null && deletedStudent.IsDeleted)
                return Result<MarkAttendanceResultDto>.Failure(
                    _localizer, AttendanceConstants.Messages.AttendanceStudentInRecycleBin,
                    HttpStatusCode.BadRequest);

            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.StudentNotFound, HttpStatusCode.NotFound);
        }

        var date = dto.OccurrenceDate?.Date ?? _timeZoneService.GetTeacherLocalDate(dto.TeacherId);
        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(dto.SessionId, date);
        if (occurrence is null)
            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceNoOccurrenceToday, HttpStatusCode.BadRequest);

        // Check if student is already marked (any status including Held)
        var existing = await _unitOfWork.AttendanceRepo
            .GetExistingAttendanceAsync(dto.TeacherStudentId, occurrence.Id);
        if (existing is not null)
            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceAlreadyMarked, HttpStatusCode.Conflict);

        var assignment = await _unitOfWork.AttendanceRepo.GetActiveAssignmentAsync(dto.TeacherStudentId);
        if (assignment is null)
            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceStudentNotAssigned, HttpStatusCode.BadRequest);

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
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                SessionId = dto.SessionId,
                SessionName = session.SessionName,
                OccurrenceDate = date,
                Status = AttendanceStatus.Held,
                AttendanceMethod = AttendanceMethod.MultiSelect, // Holds come from UI interaction
                IsCrossSession = false,
                RecordedAt = DateTime.UtcNow,
                RecordedByUserId = dto.RecordedByUserId,
                IsEdited = false,
                CreateAt = DateTime.UtcNow
            };

            await _unitOfWork.AttendanceRepo.AddAttendanceRecordAsync(record);
            // NOTE: Do NOT update absence counter — Held is not a final state

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<MarkAttendanceResultDto>.Success(new MarkAttendanceResultDto
            {
                Record = MapToRecordDto(record, student.StudentName, student.StudentCode)
            }, _localizer, AttendanceConstants.Messages.AttendanceHeldSuccess);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<MarkAttendanceResultDto>> ReleaseHoldAsync(ReleaseHoldDto dto)
    {
        var date = dto.OccurrenceDate?.Date ?? _timeZoneService.GetTeacherLocalDate(dto.TeacherId);
        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(dto.SessionId, date);
        if (occurrence is null)
            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceNoOccurrenceToday, HttpStatusCode.BadRequest);

        var heldRecord = await _unitOfWork.AttendanceRepo
            .GetHeldRecordAsync(dto.TeacherStudentId, occurrence.Id);
        if (heldRecord is null)
            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceHoldNotFound, HttpStatusCode.NotFound);

        // AUDIT FIX Step 8: Guard against session deleted while student on hold.
        // If the session was deleted, the occurrence FK was set to null during cleanup.
        if (heldRecord.SessionOccurrenceId is null)
        {
            await _unitOfWork.AttendanceRepo.DeleteAttendanceRecordAsync(heldRecord);
            await _unitOfWork.SaveChangesAsync();
            return Result<MarkAttendanceResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceSessionDeletedWhileHeld,
                HttpStatusCode.Gone);
        }

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            if (dto.MarkAsPresent)
            {
                // Release as Present
                heldRecord.Status = AttendanceStatus.Present;
                heldRecord.IsEdited = true;
                heldRecord.LastEditedAt = DateTime.UtcNow;
                heldRecord.LastEditedByUserId = dto.RecordedByUserId;
                await _unitOfWork.AttendanceRepo.UpdateAttendanceRecordAsync(heldRecord);

                // Log the edit
                var editLog = new AttendanceEditLog
                {
                    AttendanceRecordId = heldRecord.Id,
                    PreviousStatus = AttendanceStatus.Held,
                    NewStatus = AttendanceStatus.Present,
                    PreviousAttendanceMethod = heldRecord.AttendanceMethod,
                    NewAttendanceMethod = heldRecord.AttendanceMethod,
                    EditedAt = DateTime.UtcNow,
                    EditedByUserId = dto.RecordedByUserId,
                    EditReason = "Released from hold — marked as present",
                    CreateAt = DateTime.UtcNow
                };
                await _unitOfWork.AttendanceRepo.AddEditLogAsync(editLog);

                // Update absence counter (Held → Present)
                var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(
                    dto.SessionId, dto.TeacherId);
                await UpdateAbsenceCounterForNewRecord(dto.TeacherId, dto.TeacherStudentId,
                    AttendanceStatus.Present, date,
                    session?.SessionName ?? heldRecord.SessionName, dto.SessionId);
            }
            else
            {
                // Discard hold — delete the record, return to unmarked
                await _unitOfWork.AttendanceRepo.DeleteAttendanceRecordAsync(heldRecord);
            }

            await _unitOfWork.SaveChangesAsync();

            // FIX H2: Recalculate occurrence status after hold release.
            // Previously, releasing a hold (mark as present or discard) did not
            // update the occurrence's Pending/InProgress/Completed status.
            await UpdateOccurrenceStatusAsync(occurrence, dto.SessionId);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<MarkAttendanceResultDto>.Success(new MarkAttendanceResultDto
            {
                Record = dto.MarkAsPresent
                    ? MapToRecordDto(heldRecord,
                        heldRecord.StudentName ?? "Unknown",
                        heldRecord.StudentCode ?? "")
                    : null
            }, _localizer, AttendanceConstants.Messages.AttendanceHoldReleasedSuccess);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════
    // EDIT ATTENDANCE
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<List<OccurrenceCalendarItemDto>>> GetOccurrenceCalendarAsync(
        long teacherId, long sessionId)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<List<OccurrenceCalendarItemDto>>.Failure(
                _localizer, AttendanceConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        var occurrences = await _unitOfWork.AttendanceRepo.GetOccurrencesBySessionAsync(sessionId);
        var activeAssignments = await _unitOfWork.AttendanceRepo
            .GetActiveAssignmentsBySessionAsync(sessionId);
        int totalStudents = activeAssignments.Count;

        var occurrenceIds = occurrences.Select(o => o.Id).ToList();
        var recordCounts = await _unitOfWork.AttendanceRepo
            .CountRecordsByOccurrenceBatchAsync(occurrenceIds);

        var items = occurrences.Select(o =>
        {
            recordCounts.TryGetValue(o.Id, out int markedCount);

            return new OccurrenceCalendarItemDto
            {
                OccurrenceId = o.Id,
                OccurrenceDate = o.OccurrenceDate,
                Status = o.Status,
                MarkedCount = markedCount,
                TotalStudents = totalStudents
            };
        }).ToList();

        return Result<List<OccurrenceCalendarItemDto>>.Success(
            items, _localizer, AttendanceConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<List<AttendanceRecordDto>>> GetOccurrenceStudentsAsync(
        long teacherId, long sessionId, DateTime occurrenceDate)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<List<AttendanceRecordDto>>.Failure(
                _localizer, AttendanceConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(sessionId, occurrenceDate);
        if (occurrence is null)
            return Result<List<AttendanceRecordDto>>.Success(
                new List<AttendanceRecordDto>(), _localizer, AttendanceConstants.Messages.Success);

        var records = await _unitOfWork.AttendanceRepo.GetRecordsByOccurrenceAsync(occurrence.Id);
        var dtos = records.Select(r => MapToRecordDto(r,
            r.StudentName ?? r.TeacherStudent?.StudentName ?? "Unknown",
            r.StudentCode ?? r.TeacherStudent?.StudentCode ?? "")).ToList();

        return Result<List<AttendanceRecordDto>>.Success(
            dtos, _localizer, AttendanceConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<AttendanceRecordDto>> EditAttendanceAsync(EditAttendanceDto dto)
    {
        // Step 2.3: Validate status
        if (dto.NewStatus == AttendanceStatus.Held || dto.NewStatus == AttendanceStatus.CrossSessionPresent)
            return Result<AttendanceRecordDto>.Failure(
                _localizer, AttendanceConstants.Messages.InvalidAttendanceStatus, HttpStatusCode.BadRequest);

        var record = await _unitOfWork.AttendanceRepo
            .GetAttendanceRecordByIdAsync(dto.AttendanceRecordId, dto.TeacherId);
        if (record is null)
            return Result<AttendanceRecordDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceRecordNotFound, HttpStatusCode.NotFound);

        var previousStatus = record.Status;

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Log the edit
            var editLog = new AttendanceEditLog
            {
                AttendanceRecordId = record.Id,
                PreviousStatus = previousStatus,
                NewStatus = dto.NewStatus,
                PreviousAttendanceMethod = record.AttendanceMethod,
                NewAttendanceMethod = record.AttendanceMethod,
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

            // Recalculate absence counter
            if (record.TeacherStudentId.HasValue)
            {
                await RecalculateAbsenceCounterAfterEdit(
                    dto.TeacherId, record.TeacherStudentId.Value,
                    previousStatus, dto.NewStatus);
            }

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var resultDto = MapToRecordDto(record,
                record.StudentName ?? "Unknown",
                record.StudentCode ?? "");
            return Result<AttendanceRecordDto>.Success(
                resultDto, _localizer, AttendanceConstants.Messages.AttendanceEditedSuccess);
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
        // AUDIT FIX Step 11: Default RecordedByUserId to TeacherId for audit trail
        dto.RecordedByUserId ??= dto.TeacherId;

        // Step 2.3: Validate status
        if (dto.Status == AttendanceStatus.Held || dto.Status == AttendanceStatus.CrossSessionPresent)
            return Result<AttendanceRecordDto>.Failure(
                _localizer, AttendanceConstants.Messages.InvalidAttendanceStatus, HttpStatusCode.BadRequest);

        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionId, dto.TeacherId);
        if (session is null)
            return Result<AttendanceRecordDto>.Failure(
                _localizer, AttendanceConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
            dto.TeacherStudentId, dto.TeacherId);
        if (student is null)
            return Result<AttendanceRecordDto>.Failure(
                _localizer, AttendanceConstants.Messages.StudentNotFound, HttpStatusCode.NotFound);

        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(dto.SessionId, dto.OccurrenceDate.Date);
        if (occurrence is null)
            return Result<AttendanceRecordDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceNoOccurrenceToday, HttpStatusCode.BadRequest);

        // FIX H1 (BR-ATT-002): Check for duplicate before inserting.
        // Previously this check was missing — the DB unique index would catch it
        // with an unhandled DbUpdateException instead of a clean Result failure.
        var existingRecord = await _unitOfWork.AttendanceRepo
            .GetExistingAttendanceAsync(dto.TeacherStudentId, occurrence.Id);
        if (existingRecord is not null)
            return Result<AttendanceRecordDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceDuplicateRecordExists,
                HttpStatusCode.Conflict);

        var assignment = await _unitOfWork.AttendanceRepo.GetActiveAssignmentAsync(dto.TeacherStudentId);
        if (assignment is null)
            return Result<AttendanceRecordDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceStudentNotAssigned, HttpStatusCode.BadRequest);

        // AUDIT FIX Step 2 (BR-ATT-001): No retroactive attendance before assignment date
        if (dto.OccurrenceDate.Date < assignment.AssignedAt.Date)
            return Result<AttendanceRecordDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceBeforeAssignmentDate,
                HttpStatusCode.BadRequest);

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
                // Step 7.2: Denormalized student fields
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                SessionId = dto.SessionId,
                SessionName = session.SessionName,
                // FIX H3: Denormalized SessionGroupId survives session hard-delete (BR-ATT-005).
                SessionGroupId = session.SessionGroupId,
                OccurrenceDate = dto.OccurrenceDate.Date,
                Status = dto.Status,
                AttendanceMethod = AttendanceMethod.MultiSelect, // Via Edit Attendance
                RecordedAt = DateTime.UtcNow,
                RecordedByUserId = dto.RecordedByUserId,
                IsEdited = false,
                CreateAt = DateTime.UtcNow
            };

            await _unitOfWork.AttendanceRepo.AddAttendanceRecordAsync(record);

            await UpdateAbsenceCounterForAddedRecord(dto.TeacherId, dto.TeacherStudentId,
                dto.Status, dto.OccurrenceDate.Date, session.SessionName, dto.SessionId);

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var resultDto = MapToRecordDto(record, student.StudentName, student.StudentCode);
            return Result<AttendanceRecordDto>.Success(
                resultDto, _localizer, AttendanceConstants.Messages.AttendanceAddedSuccess);
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
            return Result<bool>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceRecordNotFound, HttpStatusCode.NotFound);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // AUDIT FIX Step 15 (BR-ATT-006): Log deletion before removing the record.
            // With SetNull FK on AttendanceEditLog, the log survives with AttendanceRecordId = null.
            var deletionLog = new AttendanceEditLog
            {
                AttendanceRecordId = record.Id,
                PreviousStatus = record.Status,
                NewStatus = record.Status, // Same — this is a deletion, not a status change
                PreviousAttendanceMethod = record.AttendanceMethod,
                NewAttendanceMethod = record.AttendanceMethod,
                EditedAt = DateTime.UtcNow,
                EditedByUserId = dto.TeacherId,
                EditReason = "Record deleted via Edit Attendance (REQ-ATT-024)",
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.AttendanceRepo.AddEditLogAsync(deletionLog);

            await _unitOfWork.AttendanceRepo.DeleteAttendanceRecordAsync(record);

            if (record.TeacherStudentId.HasValue)
            {
                await RecalculateAbsenceCounterAfterEdit(
                    dto.TeacherId, record.TeacherStudentId.Value,
                    record.Status, null);
            }

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<bool>.Success(
                true, _localizer, AttendanceConstants.Messages.AttendanceRecordDeletedSuccess);
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
                _localizer, AttendanceConstants.Messages.AttendanceRecordNotFound, HttpStatusCode.NotFound);

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

        return Result<List<AttendanceEditLogDto>>.Success(
            dtos, _localizer, AttendanceConstants.Messages.Success);
    }

    // ══════════════════════════════════════════════
    // ABSENCE OVERVIEW
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<AbsenceOverviewStudentDto>>>> GetAbsenceOverviewAsync(
        long teacherId, long sessionId, AbsenceOverviewRequest request)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<PaginatedResponse<List<AbsenceOverviewStudentDto>>>.Failure(
                _localizer, AttendanceConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        // Include linked sessions for cross-session absence view (REQ-ATT-033)
        var linkedSessions = await _unitOfWork.SessionsRepo.GetLinkedSessionsAsync(sessionId);
        var allSessionIds = linkedSessions.Select(s => s.Id).Append(sessionId).ToList();

        // AUDIT FIX Step 12 (REQ-ATT-035): If a specific date is requested,
        // query AttendanceRecords for that date instead of global counters
        if (request.OccurrenceDate.HasValue)
        {
            // FIX H4 (REQ-ATT-034): Pass phone filters to the date-specific path.
            // Previously MissingStudentPhone and MissingParentPhone were ignored here.
            var dateRecordCount = await _unitOfWork.AttendanceRepo.CountAbsentStudentsByDateAsync(
                teacherId, allSessionIds, request.OccurrenceDate.Value, request.Search,
                missingStudentPhone: request.MissingStudentPhone ? true : null,
                missingParentPhone: request.MissingParentPhone ? true : null);

            var dateRecords = await _unitOfWork.AttendanceRepo.GetAbsentStudentsByDateAsync(
                teacherId, allSessionIds, request.OccurrenceDate.Value,
                request.Search, request.Page, request.PageSize,
                missingStudentPhone: request.MissingStudentPhone ? true : null,
                missingParentPhone: request.MissingParentPhone ? true : null);

            var dateDtos = dateRecords.Select(r => new AbsenceOverviewStudentDto
            {
                TeacherStudentId = r.TeacherStudentId ?? 0,
                StudentName = r.StudentName ?? r.TeacherStudent?.StudentName ?? "Unknown",
                StudentCode = r.StudentCode ?? r.TeacherStudent?.StudentCode ?? "",
                SessionId = r.SessionId,
                SessionName = r.SessionName,
                ConsecutiveAbsences = 0, // Not available from a single-date query
                TotalAbsences = 0,
                LastAbsenceDate = r.OccurrenceDate,
                RecentStatuses = new List<AttendanceStatus> { AttendanceStatus.Absent }
            }).ToList();

            var dateResponse = new PaginatedResponse<List<AbsenceOverviewStudentDto>>
            {
                totalCount = dateRecordCount,
                page = request.Page,
                pageSize = request.PageSize,
                totalPages = (int)Math.Ceiling(dateRecordCount / (double)request.PageSize),
                data = dateDtos
            };

            return Result<PaginatedResponse<List<AbsenceOverviewStudentDto>>>.Success(
                dateResponse, _localizer, AttendanceConstants.Messages.Success);
        }

        // Default path: latest occurrence — uses global absence counters
        int totalCount = await _unitOfWork.AttendanceRepo.CountAbsenceOverviewAsync(
            teacherId,
            sessionId: request.SessionId ?? sessionId,
            search: request.Search,
            missingStudentPhone: request.MissingStudentPhone ? true : null,
            missingParentPhone: request.MissingParentPhone ? true : null);

        var pagedCounters = await _unitOfWork.AttendanceRepo.GetPagedAbsenceOverviewAsync(
            teacherId,
            request.Page,
            request.PageSize,
            sessionId: request.SessionId ?? sessionId,
            search: request.Search,
            missingStudentPhone: request.MissingStudentPhone ? true : null,
            missingParentPhone: request.MissingParentPhone ? true : null);

        // FIX M3: Batch-load recent statuses for all students in one query
        // instead of N+1 individual queries inside the loop.
        var studentIds = pagedCounters
            .Select(c => c.TeacherStudentId).ToList();
        var recentStatusesMap = studentIds.Count > 0
            ? await _unitOfWork.AttendanceRepo.GetRecentRecordsByStudentsBatchAsync(
                studentIds, AttendanceConstants.RecentStatusIndicatorCount)
            : new Dictionary<long, IReadOnlyList<AttendanceStatus>>();

        var dtos = new List<AbsenceOverviewStudentDto>();
        foreach (var counter in pagedCounters)
        {
            recentStatusesMap.TryGetValue(counter.TeacherStudentId, out var recentStatuses);

            dtos.Add(new AbsenceOverviewStudentDto
            {
                TeacherStudentId = counter.TeacherStudentId,
                StudentName = counter.TeacherStudent?.StudentName ?? "Unknown",
                StudentCode = counter.TeacherStudent?.StudentCode ?? "",
                SessionId = counter.TeacherStudent?.SessionId,
                // FIX M2: Populate SessionName — was always null in the default path.
                SessionName = counter.LastAbsenceSessionName,
                ConsecutiveAbsences = counter.ConsecutiveAbsences,
                TotalAbsences = counter.TotalAbsences,
                LastAbsenceDate = counter.LastAbsenceDate,
                RecentStatuses = recentStatuses?.ToList() ?? new List<AttendanceStatus>()
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

        return Result<PaginatedResponse<List<AbsenceOverviewStudentDto>>>.Success(
            response, _localizer, AttendanceConstants.Messages.Success);
    }

    // ══════════════════════════════════════════════
    // STUDENT ATTENDANCE TIMELINE
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<StudentAttendanceSummaryDto>>>> GetTimelineStudentListAsync(
        long teacherId, AttendanceTimelineRequest request)
    {
        // FIX M4: Use DB-level pagination instead of loading ALL student IDs into memory.
        // Previously loaded all distinct IDs then paginated with LINQ Skip/Take in memory.
        // For a teacher with 50K students, this loaded 50K longs on every request.
        var (pagedIds, totalCount) = await _unitOfWork.AttendanceRepo.GetPagedTimelineStudentIdsAsync(
            teacherId,
            request.Page,
            request.PageSize,
            sessionId: request.SessionId,
            sessionGroupId: request.SessionGroupId,
            studentName: request.StudentName,
            studentCode: request.StudentCode);

        // FIX L8: Batch-load counters and assignments for the page to reduce N+1.
        // Previously BuildStudentSummary made 3 DB calls per student (student, counter, assignments).
        var counterMap = pagedIds.Count > 0
            ? await _unitOfWork.AttendanceRepo.GetAbsenceCountersBatchAsync(teacherId, pagedIds)
            : new Dictionary<long, StudentAbsenceCounter>();

        // FIX P5: Batch-load assignments for all paged students in one query.
        // Previously called GetAssignmentsByStudentAsync per student = N extra DB queries.
        var assignmentMap = pagedIds.Count > 0
            ? await _unitOfWork.AttendanceRepo.GetAssignmentsByStudentsBatchAsync(pagedIds)
            : new Dictionary<long, IReadOnlyList<StudentSessionAssignment>>();

        // Batch-load students
        var students = pagedIds.Count > 0
            ? await _unitOfWork.Students.GetActiveByIdsAndTeacherAsync(teacherId, pagedIds)
            : new List<TeacherStudent>();
        var studentMap = students.ToDictionary(s => s.Id);

        var summaries = new List<StudentAttendanceSummaryDto>();
        foreach (var studentId in pagedIds)
        {
            if (!studentMap.TryGetValue(studentId, out var student))
                continue;

            counterMap.TryGetValue(studentId, out var counter);
            assignmentMap.TryGetValue(studentId, out var assignments);

            var periods = (assignments ?? Array.Empty<StudentSessionAssignment>()).Select(a => new AssignmentPeriodDto
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

            summaries.Add(new StudentAttendanceSummaryDto
            {
                TeacherStudentId = studentId,
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                TotalOccurrences = totalOcc,
                TotalAbsences = counter?.TotalAbsences ?? 0,
                AttendancePercentage = totalOcc > 0
                    ? Math.Round((decimal)totalPresent / totalOcc * 100, 1) : 0,
                ConsecutiveAbsences = counter?.ConsecutiveAbsences ?? 0,
                AssignmentPeriods = periods
            });
        }

        var response = new PaginatedResponse<List<StudentAttendanceSummaryDto>>
        {
            totalCount = totalCount,
            page = request.Page,
            pageSize = request.PageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
            data = summaries
        };

        return Result<PaginatedResponse<List<StudentAttendanceSummaryDto>>>.Success(
            response, _localizer, AttendanceConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<StudentAttendanceSummaryDto>> GetStudentAttendanceSummaryAsync(
        long teacherId, long teacherStudentId)
    {
        var summary = await BuildStudentSummary(teacherId, teacherStudentId);
        if (summary is null)
            return Result<StudentAttendanceSummaryDto>.Failure(
                _localizer, AttendanceConstants.Messages.StudentNotFound, HttpStatusCode.NotFound);

        return Result<StudentAttendanceSummaryDto>.Success(
            summary, _localizer, AttendanceConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<MonthlyAttendanceSummaryDto>> GetStudentTimelineMonthAsync(
        long teacherId, long teacherStudentId, StudentTimelineMonthRequest request)
    {
        var startDate = new DateTime(request.Year, request.Month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var records = await _unitOfWork.AttendanceRepo
            .GetRecordsByStudentAndDateRangeAsync(teacherStudentId, startDate, endDate);

        var recordDtos = records.Select(r => MapToRecordDto(r,
            r.StudentName ?? r.TeacherStudent?.StudentName ?? "Unknown",
            r.StudentCode ?? r.TeacherStudent?.StudentCode ?? "")).ToList();

        int totalOccurrences = recordDtos.Count;
        int totalPresent = recordDtos.Count(r =>
            r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.CrossSessionPresent);
        int totalAbsences = recordDtos.Count(r => r.Status == AttendanceStatus.Absent);

        var monthSummary = new MonthlyAttendanceSummaryDto
        {
            Year = request.Year,
            Month = request.Month,
            TotalOccurrences = totalOccurrences,
            TotalPresent = totalPresent,
            TotalAbsences = totalAbsences,
            AttendancePercentage = totalOccurrences > 0
                ? Math.Round((decimal)totalPresent / totalOccurrences * 100, 1) : 0,
            Records = recordDtos
        };

        return Result<MonthlyAttendanceSummaryDto>.Success(
            monthSummary, _localizer, AttendanceConstants.Messages.Success);
    }

    // ══════════════════════════════════════════════
    // REPORTING
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<List<AttendanceRecordDto>>> GenerateReportAsync(
        long teacherId, AttendanceReportRequest request)
    {
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<List<AttendanceRecordDto>>.Failure(
                _localizer, AttendanceConstants.Messages.TeacherNotFound, HttpStatusCode.NotFound);

        long? studentFilter = null;
        if (request.ReportType == AttendanceReportType.SingleStudentAbsence
            && request.TeacherStudentId.HasValue)
            studentFilter = request.TeacherStudentId.Value;

        // Step 6.1: Handle all 6 report types
        IEnumerable<long>? linkedSessionIds = null;
        if (request.ReportType == AttendanceReportType.LinkedSessionsAttendance
            && request.SessionId.HasValue)
        {
            var linked = await _unitOfWork.SessionsRepo.GetLinkedSessionsAsync(request.SessionId.Value);
            linkedSessionIds = linked.Select(s => s.Id).Append(request.SessionId.Value).ToList();
        }

        var records = await _unitOfWork.AttendanceRepo.ExecuteReportQueryAsync(
            teacherId,
            sessionId: request.SessionId,
            sessionGroupId: request.SessionGroupId,
            startDate: request.StartDate,
            endDate: request.EndDate,
            status: request.ReportType == AttendanceReportType.SingleStudentAbsence
                || request.ReportType == AttendanceReportType.SessionAbsence
                || request.ReportType == AttendanceReportType.AllSessionsAbsence
                    ? AttendanceStatus.Absent : null,
            teacherStudentId: studentFilter,
            sessionIds: linkedSessionIds);

        var dtos = records.Select(r => MapToRecordDto(r,
            r.StudentName ?? r.TeacherStudent?.StudentName ?? "Unknown",
            r.StudentCode ?? r.TeacherStudent?.StudentCode ?? "")).ToList();

        return Result<List<AttendanceRecordDto>>.Success(
            dtos, _localizer, AttendanceConstants.Messages.AttendanceReportGenerated);
    }

    // ══════════════════════════════════════════════
    // EXPORT (Step 3.2)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<byte[]>> ExportReportAsync(
        long teacherId, AttendanceReportRequest request, string format)
    {
        if (format.ToLower() != "xlsx" && format.ToLower() != "pdf")
            return Result<byte[]>.Failure(
                _localizer, AttendanceConstants.Messages.InvalidExportFormat, HttpStatusCode.BadRequest);

        var reportResult = await GenerateReportAsync(teacherId, request);
        if (!reportResult.IsSuccess)
            return Result<byte[]>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceReportGenerationFailed, reportResult.StatusCode);

        byte[] fileBytes = format.ToLower() == "xlsx"
            ? await _exportService.ExportReportToExcelAsync(reportResult.Data!, request.ReportType)
            : await _exportService.ExportReportToPdfAsync(reportResult.Data!, request.ReportType);

        return Result<byte[]>.Success(
            fileBytes, _localizer, AttendanceConstants.Messages.ExportCompleted);
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> ExportTimelineAsync(
        long teacherId, long teacherStudentId,
        DateTime? startDate, DateTime? endDate, string format)
    {
        if (format.ToLower() != "xlsx" && format.ToLower() != "pdf")
            return Result<byte[]>.Failure(
                _localizer, AttendanceConstants.Messages.InvalidExportFormat, HttpStatusCode.BadRequest);

        var summaryResult = await GetStudentAttendanceSummaryAsync(teacherId, teacherStudentId);
        if (!summaryResult.IsSuccess)
            return Result<byte[]>.Failure(
                _localizer, AttendanceConstants.Messages.StudentNotFound, summaryResult.StatusCode);

        // Determine date range
        // FIX R3: When no assignments exist, skip querying empty range entirely.
        var firstAssignment = summaryResult.Data!.AssignmentPeriods
            .OrderBy(p => p.AssignedAt).FirstOrDefault();
        if (firstAssignment is null && !startDate.HasValue)
        {
            // No assignments and no explicit start date — return empty export
            byte[] emptyBytes = format.ToLower() == "xlsx"
                ? await _exportService.ExportTimelineToExcelAsync(summaryResult.Data!, new List<MonthlyAttendanceSummaryDto>())
                : await _exportService.ExportTimelineToPdfAsync(summaryResult.Data!, new List<MonthlyAttendanceSummaryDto>());
            return Result<byte[]>.Success(emptyBytes, _localizer, AttendanceConstants.Messages.ExportCompleted);
        }

        var effectiveStart = startDate ?? firstAssignment!.AssignedAt;
        var effectiveEnd = endDate ?? DateTime.UtcNow;

        // FIX P4: Load ALL records in a single query then group by month in memory.
        // Previously made N sequential GetStudentTimelineMonthAsync calls (one per month),
        // each hitting the database. For 24 months = 24 DB round-trips.
        var allRecords = await _unitOfWork.AttendanceRepo
            .GetRecordsByStudentAndDateRangeAsync(teacherStudentId, effectiveStart, effectiveEnd);

        var recordDtos = allRecords.Select(r => MapToRecordDto(r,
            r.StudentName ?? r.TeacherStudent?.StudentName ?? "Unknown",
            r.StudentCode ?? r.TeacherStudent?.StudentCode ?? "")).ToList();

        // Group by Year-Month and build monthly summaries
        var months = recordDtos
            .GroupBy(r => new { r.OccurrenceDate.Year, r.OccurrenceDate.Month })
            .Select(g =>
            {
                int totalOcc = g.Count();
                int totalPresent = g.Count(r =>
                    r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.CrossSessionPresent);
                int totalAbsences = g.Count(r => r.Status == AttendanceStatus.Absent);
                return new MonthlyAttendanceSummaryDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalOccurrences = totalOcc,
                    TotalPresent = totalPresent,
                    TotalAbsences = totalAbsences,
                    AttendancePercentage = totalOcc > 0
                        ? Math.Round((decimal)totalPresent / totalOcc * 100, 1) : 0,
                    Records = g.ToList()
                };
            })
            .Where(m => m.TotalOccurrences > 0)
            .ToList();

        byte[] fileBytes = format.ToLower() == "xlsx"
            ? await _exportService.ExportTimelineToExcelAsync(summaryResult.Data!, months)
            : await _exportService.ExportTimelineToPdfAsync(summaryResult.Data!, months);

        return Result<byte[]>.Success(
            fileBytes, _localizer, AttendanceConstants.Messages.ExportCompleted);
    }

    // ══════════════════════════════════════════════
    // OFFLINE SYNC (Step 3.3)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<SyncResultDto>> SyncOfflineRecordsAsync(OfflineSyncRequestDto dto)
    {
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(dto.TeacherId);
        if (teacher is null)
            return Result<SyncResultDto>.Failure(
                _localizer, AttendanceConstants.Messages.TeacherNotFound, HttpStatusCode.NotFound);

        var result = new SyncResultDto
        {
            TotalSubmitted = dto.Entries.Count,
            EntryResults = new List<SyncEntryResultDto>()
        };

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            int successCount = 0;
            int conflictCount = 0;
            int failedCount = 0;

            foreach (var entry in dto.Entries)
            {
                // Get or validate occurrence
                var occurrence = await _unitOfWork.AttendanceRepo
                    .GetOccurrenceBySessionAndDateAsync(entry.SessionId, entry.OccurrenceDate.Date);

                if (occurrence is null)
                {
                    failedCount++;
                    result.EntryResults.Add(new SyncEntryResultDto
                    {
                        ClientEntryId = entry.ClientEntryId,
                        Success = false,
                        IsConflict = false,
                        ErrorMessage = "No occurrence found for this session on this date"
                    });
                    continue;
                }

                // Check for existing record
                var existing = await _unitOfWork.AttendanceRepo
                    .GetExistingAttendanceAsync(entry.TeacherStudentId, occurrence.Id);

                if (existing is not null)
                {
                    if (existing.Status == entry.Status)
                    {
                        // Already synced with same status — treat as success
                        successCount++;
                        result.EntryResults.Add(new SyncEntryResultDto
                        {
                            ClientEntryId = entry.ClientEntryId,
                            Success = true,
                            IsConflict = false
                        });
                        continue;
                    }
                    else
                    {
                        // Conflict: server has different status
                        conflictCount++;
                        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
                            entry.TeacherStudentId, dto.TeacherId);
                        result.EntryResults.Add(new SyncEntryResultDto
                        {
                            ClientEntryId = entry.ClientEntryId,
                            Success = false,
                            IsConflict = true,
                            ServerRecord = MapToRecordDto(existing,
                                existing.StudentName ?? student?.StudentName ?? "Unknown",
                                existing.StudentCode ?? student?.StudentCode ?? "")
                        });
                        continue;
                    }
                }

                // No existing record — create via normal mark logic
                var markDto = new MarkAttendanceDto
                {
                    TeacherId = dto.TeacherId,
                    SessionId = entry.SessionId,
                    TeacherStudentId = entry.TeacherStudentId,
                    Status = entry.Status,
                    AttendanceMethod = entry.AttendanceMethod,
                    OccurrenceDate = entry.OccurrenceDate,
                    RecordedByUserId = dto.RecordedByUserId,
                    // AUDIT FIX Step 5: Do NOT auto-confirm absence alerts during sync.
                    // REQ-ATT-057/058: Tutor must explicitly confirm.
                    AbsenceAlertConfirmed = false
                };

                var markResult = await MarkAttendanceAsync(markDto);
                if (markResult.IsSuccess)
                {
                    var markData = markResult.Data!;

                    // AUDIT FIX Step 5: Detect absence-alert-pending returns.
                    // When MarkAttendanceAsync returns success with HasAbsenceAlert=true
                    // and Record=null, it means an absence alert is pending confirmation.
                    if (markData.HasAbsenceAlert && markData.Record is null)
                    {
                        // FIX R1: Look up actual student name for the alert.
                        // Previously used markData.LastAbsenceSessionName (a SESSION name)
                        // as the StudentName field — clearly wrong.
                        var alertStudent = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
                            entry.TeacherStudentId, dto.TeacherId);

                        result.RequiresConfirmationCount++;
                        result.EntryResults.Add(new SyncEntryResultDto
                        {
                            ClientEntryId = entry.ClientEntryId,
                            Success = false,
                            IsConflict = false,
                            RequiresAbsenceConfirmation = true,
                            AbsenceAlertInfo = new AbsenceAlertStudentDto
                            {
                                TeacherStudentId = entry.TeacherStudentId,
                                StudentName = alertStudent?.StudentName ?? "Unknown",
                                StudentCode = alertStudent?.StudentCode ?? "",
                                ConsecutiveAbsences = markData.ConsecutiveAbsences,
                                LastAbsenceDate = markData.LastAbsenceDate,
                                LastAbsenceSessionName = markData.LastAbsenceSessionName,
                                WasCrossSession = markData.LastAbsenceWasCrossSession
                            }
                        });
                    }
                    else
                    {
                        successCount++;
                        result.EntryResults.Add(new SyncEntryResultDto
                        {
                            ClientEntryId = entry.ClientEntryId,
                            Success = true,
                            IsConflict = false
                        });
                    }
                }
                else
                {
                    failedCount++;
                    result.EntryResults.Add(new SyncEntryResultDto
                    {
                        ClientEntryId = entry.ClientEntryId,
                        Success = false,
                        IsConflict = false,
                        ErrorMessage = markResult.Message ?? "Unknown error during sync"
                    });
                }
            }

            result.SuccessCount = successCount;
            result.ConflictCount = conflictCount;
            result.FailedCount = failedCount;

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<SyncResultDto>.Success(
                result, _localizer, AttendanceConstants.Messages.SyncCompleted);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════
    // STUDENT/PARENT VIEW ACCESS
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<MonthlyAttendanceSummaryDto>> GetStudentViewAttendanceAsync(
        long teacherId, long teacherStudentId, StudentTimelineMonthRequest request)
    {
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        if (config is null || (!config.StudentVisibilityAttendance && !config.ParentVisibilityAttendance))
            return Result<MonthlyAttendanceSummaryDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceVisibilityDisabled, HttpStatusCode.Forbidden);

        return await GetStudentTimelineMonthAsync(teacherId, teacherStudentId, request);
    }

    /// <inheritdoc />
    public async Task<Result<StudentAttendanceSummaryDto>> GetStudentViewAttendanceSummaryAsync(
        long teacherId, long teacherStudentId)
    {
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        if (config is null || (!config.StudentVisibilityAttendance && !config.ParentVisibilityAttendance))
            return Result<StudentAttendanceSummaryDto>.Failure(
                _localizer, AttendanceConstants.Messages.AttendanceVisibilityDisabled, HttpStatusCode.Forbidden);

        return await GetStudentAttendanceSummaryAsync(teacherId, teacherStudentId);
    }

    // ══════════════════════════════════════════════
    // INTEGRATION HOOKS
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<bool>> OnStudentAssignedToSessionAsync(
        long teacherId, long teacherStudentId, long sessionId, string sessionName)
    {
        // AUDIT FIX Step 14: Deactivate any existing active assignment first (idempotent).
        // Prevents dual active assignments even if caller forgot to unassign (REQ-ATT-048 safety).
        var existingAssignment = await _unitOfWork.AttendanceRepo
            .GetActiveAssignmentAsync(teacherStudentId);
        if (existingAssignment is not null)
        {
            existingAssignment.IsActive = false;
            existingAssignment.UnassignedAt = DateTime.UtcNow;
            await _unitOfWork.AttendanceRepo.UpdateAssignmentAsync(existingAssignment);
        }

        // AUDIT FIX Step 1: Fetch student to populate denormalized fields
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(teacherStudentId, teacherId);

        var assignment = new StudentSessionAssignment
        {
            TeacherId = teacherId,
            TeacherStudentId = teacherStudentId,
            SessionId = sessionId,
            SessionName = sessionName,
            // AUDIT FIX Step 1: Denormalized student fields — survive student permanent purge
            StudentName = student?.StudentName,
            StudentCode = student?.StudentCode,
            AssignedAt = DateTime.UtcNow,
            IsActive = true,
            CreateAt = DateTime.UtcNow
        };
        await _unitOfWork.AttendanceRepo.AddAssignmentAsync(assignment);

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

        return Result<bool>.Success(true, _localizer, AttendanceConstants.Messages.Success);
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

        return Result<bool>.Success(true, _localizer, AttendanceConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> OnSessionDeletingAsync(long teacherId, long sessionId)
    {
        // Step 1.2: All these now use ExecuteUpdateAsync — no in-memory loading
        await _unitOfWork.AttendanceRepo.NullifyOccurrenceReferencesForSessionAsync(sessionId);
        await _unitOfWork.AttendanceRepo.NullifySessionIdOnRecordsForSessionAsync(sessionId);
        await _unitOfWork.AttendanceRepo.DeactivateAssignmentsBySessionAsync(sessionId);
        await _unitOfWork.AttendanceRepo.DeleteOccurrencesBySessionAsync(sessionId);

        return Result<bool>.Success(true, _localizer, AttendanceConstants.Messages.Success);
    }

    /// <inheritdoc />
    /// Step 1.1: Nullifies FK references on AttendanceRecords before student hard-delete.
    public async Task<Result<bool>> OnStudentPermanentlyDeletedAsync(long teacherStudentId)
    {
        await _unitOfWork.AttendanceRepo.DeleteAbsenceCountersByStudentAsync(teacherStudentId);
        await _unitOfWork.AttendanceRepo.DeactivateAssignmentsByStudentAsync(teacherStudentId);

        // Step 1.1: Nullify FK references on AttendanceRecords.
        // This prevents FK violation when TeacherStudent row is hard-deleted.
        // Denormalized StudentName/StudentCode remain intact for historical display.
        await _unitOfWork.AttendanceRepo.NullifyStudentReferencesOnRecordsAsync(teacherStudentId);

        return Result<bool>.Success(true, _localizer, AttendanceConstants.Messages.Success);
    }

    // ══════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════

    private async Task UpdateAbsenceCounterForNewRecord(
        long teacherId, long teacherStudentId,
        AttendanceStatus status, DateTime date, string sessionName, long sessionId)
    {
        // FIX H7: Retry loop for optimistic concurrency on RowVersion.
        // StudentAbsenceCounter uses [Timestamp] RowVersion but previously had no retry logic.
        // Two concurrent requests (e.g., two assistants on linked sessions) would cause
        // DbUpdateConcurrencyException to propagate unhandled.
        for (int attempt = 0; attempt < AttendanceConstants.MaxConcurrencyRetries; attempt++)
        {
            try
            {
                // FIX R2: Use AsNoTracking fetch then re-attach to avoid stale RowVersion.
                // On retry, the previous attempt left the stale entity in the change tracker.
                // GetAbsenceCounterFreshAsync bypasses the tracker to get the latest RowVersion.
                var counter = await _unitOfWork.AttendanceRepo
                    .GetAbsenceCounterFreshAsync(teacherId, teacherStudentId);

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
                    counter.ConsecutiveAbsences = 0;
                    counter.TotalPresent++;
                    counter.LastAttendanceDate = date;
                }

                await _unitOfWork.AttendanceRepo.UpdateAbsenceCounterAsync(counter);
                return; // Success — exit retry loop
            }
            catch (Exception ex) when (IsConcurrencyException(ex))
            {
                if (attempt >= AttendanceConstants.MaxConcurrencyRetries - 1)
                {
                    _logger.LogError(ex,
                        "Concurrency conflict on absence counter for student {StudentId} exceeded max retries ({Max})",
                        teacherStudentId, AttendanceConstants.MaxConcurrencyRetries);
                    throw; // Final attempt — propagate to caller
                }

                _logger.LogWarning(
                    "Concurrency conflict updating absence counter for student {StudentId}, retry {Attempt}",
                    teacherStudentId, attempt + 1);
                await Task.Delay(50 * (attempt + 1)); // Brief backoff
            }
        }
    }

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

        counter.TotalOccurrences++;
        if (status == AttendanceStatus.Absent)
        {
            counter.TotalAbsences++;
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

        // Recalculate consecutive from actual records (out-of-order safe)
        counter.ConsecutiveAbsences = await _unitOfWork.AttendanceRepo
            .RecalculateConsecutiveAbsencesAsync(teacherStudentId);

        await _unitOfWork.AttendanceRepo.UpdateAbsenceCounterAsync(counter);
    }

    private async Task RecalculateAbsenceCounterAfterEdit(
        long teacherId, long teacherStudentId,
        AttendanceStatus previousStatus, AttendanceStatus? newStatus)
    {
        var counter = await _unitOfWork.AttendanceRepo
            .GetAbsenceCounterAsync(teacherId, teacherStudentId);
        if (counter is null) return;

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
            else if (previousStatus == AttendanceStatus.Present
                || previousStatus == AttendanceStatus.CrossSessionPresent)
                counter.TotalPresent = Math.Max(0, counter.TotalPresent - 1);
        }

        counter.ConsecutiveAbsences = await _unitOfWork.AttendanceRepo
            .RecalculateConsecutiveAbsencesAsync(teacherStudentId);

        await _unitOfWork.AttendanceRepo.UpdateAbsenceCounterAsync(counter);
    }

    private async Task UpdateOccurrenceStatusAsync(SessionOccurrence occurrence, long sessionId)
    {
        var records = await _unitOfWork.AttendanceRepo.GetRecordsByOccurrenceAsync(occurrence.Id);
        var assignments = await _unitOfWork.AttendanceRepo.GetActiveAssignmentsBySessionAsync(sessionId);

        // Step 3.1: Exclude Held records from "marked" count
        int markedCount = records.Count(r => r.Status != AttendanceStatus.Held);
        int totalStudents = assignments.Count;

        if (markedCount == 0)
            occurrence.Status = OccurrenceStatus.Pending;
        else if (markedCount >= totalStudents && totalStudents > 0)
            occurrence.Status = OccurrenceStatus.Completed;
        else
            occurrence.Status = OccurrenceStatus.InProgress;

        await _unitOfWork.AttendanceRepo.UpdateOccurrenceAsync(occurrence);
    }

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
                ? Math.Round((decimal)totalPresent / totalOcc * 100, 1) : 0,
            ConsecutiveAbsences = counter?.ConsecutiveAbsences ?? 0,
            AssignmentPeriods = periods
        };
    }

    /// <summary>
    /// Maps an AttendanceRecord entity to its output DTO.
    /// Step 7.2: Prefers denormalized fields, falls back to navigation property.
    /// </summary>
    private static AttendanceRecordDto MapToRecordDto(
        AttendanceRecord record, string studentName, string studentCode)
    {
        return new AttendanceRecordDto
        {
            Id = record.Id,
            TeacherStudentId = record.TeacherStudentId ?? 0,
            StudentName = record.StudentName ?? studentName,
            StudentCode = record.StudentCode ?? studentCode,
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

    /// <summary>
    /// FIX L1: Fire-and-forget wrapper that logs exceptions instead of silently discarding them.
    /// Previously messaging calls used the discard pattern (underscore) which swallowed all errors.
    /// When the real messaging module replaces the stub, exceptions would be lost.
    /// </summary>
    private async Task SafeFireMessagingAsync(Func<Task> messagingAction)
    {
        try
        {
            await messagingAction();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Messaging notification failed. The attendance record was saved successfully but the notification was not delivered.");
        }
    }

    /// <summary>
    /// FIX H7: Architecture-safe concurrency exception detection.
    /// The Application layer cannot reference Microsoft.EntityFrameworkCore directly
    /// (that would violate Onion Architecture — EF Core is an Infrastructure concern).
    /// Instead we check the exception type name to detect DbUpdateConcurrencyException
    /// without taking a compile-time dependency on the EF Core assembly.
    /// </summary>
    private static bool IsConcurrencyException(Exception ex)
    {
        // Walk the exception chain — EF Core may wrap the concurrency exception
        var current = ex;
        while (current is not null)
        {
            if (current.GetType().Name == "DbUpdateConcurrencyException")
                return true;
            current = current.InnerException;
        }
        return false;
    }
}