using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Session;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements all Session Module (Module 2) operations.
/// Manages teacher-scoped sessions: CRUD, groups, membership links,
/// student assignment, search, filter, and session name generation.
/// 
/// All database access goes through IUnitOfWork.SessionsRepo (ISessionRepo)
/// and IUnitOfWork.Users (IUserRepo for teacher validation) — no direct
/// GetRepository calls with raw expression predicates.
/// 
/// ARCHITECTURAL NOTE:
/// All query logic is encapsulated in ISessionRepo named methods.
/// If a query changes, you edit the repo method — not this service.
/// 
/// TRANSACTION SAFETY:
/// All transactional methods use the ownsTransaction pattern:
///   bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
/// This makes them safe for both standalone calls and nested calls.
/// 
/// HARD DELETE:
/// REQ-SES-041: Sessions use hard delete — no soft-delete or recycle bin.
/// </summary>
public class SessionService : ISessionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionNameGenerator _nameGenerator;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    /// <summary>
    /// Maximum number of days selectable for Weekly/BiWeekly occurrence types.
    /// REQ-SES-008: Minimum 1, maximum 7 days per week.
    /// </summary>
    private const int MaxSelectedDays = 7;

    /// <summary>
    /// Valid day-of-week indices. 0=Saturday through 6=Friday.
    /// REQ-SES-008: Day-of-week selector values.
    /// </summary>
    private static readonly HashSet<int> ValidDayIndices = new() { 0, 1, 2, 3, 4, 5, 6 };

    public SessionService(
        IUnitOfWork unitOfWork,
        ISessionNameGenerator nameGenerator,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _nameGenerator = nameGenerator;
        _localizer = localizer;
    }

    // ══════════════════════════════════════════════
    // SESSION CRUD
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<SessionDto>> CreateSessionAsync(CreateSessionDto dto)
    {
        // 1. Validate teacher exists
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(dto.TeacherId);
        if (teacher is null)
            return Result<SessionDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // 2. Resolve session name: auto-generate or use provided
        string sessionName;
        if (string.IsNullOrWhiteSpace(dto.SessionName))
        {
            // Auto-generate: check teacher config for session name settings
            var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(dto.TeacherId);
            var language = config?.SessionNameLanguage ?? GenerationLanguage.English;
            sessionName = await _nameGenerator.GenerateNextNameAsync(dto.TeacherId, language);
        }
        else
        {
            sessionName = dto.SessionName.Trim();
        }

        // 3. Validate unique session name per teacher (BR-SES-001)
        bool nameExists = await _unitOfWork.SessionsRepo.SessionNameExistsAsync(dto.TeacherId, sessionName);
        if (nameExists)
            return Result<SessionDto>.Failure(_localizer, "SessionNameDuplicate", HttpStatusCode.Conflict);

        // 4. Validate occurrence type configuration
        var occurrenceError = ValidateOccurrenceConfiguration(dto.OccurrenceType, dto.SelectedDays, dto.MonthlyDayOfMonth);
        if (occurrenceError is not null)
            return Result<SessionDto>.Failure(occurrenceError, HttpStatusCode.BadRequest);

        // 5. Validate date range (REQ-SES-014)
        if (dto.EndDate <= dto.StartDate)
            return Result<SessionDto>.Failure(_localizer, "SessionEndDateMustBeAfterStartDate", HttpStatusCode.BadRequest);

        // 6. Validate group exists if provided
        if (dto.SessionGroupId.HasValue)
        {
            var group = await _unitOfWork.SessionsRepo.GetGroupByIdAndTeacherAsync(dto.SessionGroupId.Value, dto.TeacherId);
            if (group is null)
                return Result<SessionDto>.Failure(_localizer, "SessionGroupNotFound", HttpStatusCode.NotFound);
        }

        // 7. Create the session entity
        var session = new Session
        {
            TeacherId = dto.TeacherId,
            SessionName = sessionName,
            OccurrenceType = dto.OccurrenceType,
            SelectedDays = FormatSelectedDays(dto.SelectedDays),
            MonthlyDayOfMonth = dto.OccurrenceType == OccurrenceType.Monthly ? dto.MonthlyDayOfMonth : null,
            PaymentType = dto.PaymentType,
            SessionAmount = dto.SessionAmount,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            StartTime = dto.StartTime,
            DurationMinutes = dto.DurationMinutes,
            SessionGroupId = dto.SessionGroupId,
            CreateAt = DateTime.UtcNow
        };

        await _unitOfWork.SessionsRepo.AddAsync(session);
        await _unitOfWork.SaveChangesAsync();

        var resultDto = await BuildSessionDtoAsync(session);
        return Result<SessionDto>.Success(resultDto, _localizer, "SessionCreatedSuccess", HttpStatusCode.Created);
    }

    /// <inheritdoc />
    public async Task<Result<SessionDto>> GetSessionByIdAsync(long teacherId, long sessionId)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<SessionDto>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        var dto = await BuildSessionDtoAsync(session);
        return Result<SessionDto>.Success(dto, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<SessionDto>> UpdateSessionAsync(long teacherId, long sessionId, UpdateSessionDto dto)
    {
        // 1. Validate session exists and belongs to teacher
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<SessionDto>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        // 2. Validate unique name (excluding self) — BR-SES-001
        string trimmedName = dto.SessionName.Trim();
        bool nameExists = await _unitOfWork.SessionsRepo.SessionNameExistsExcludingAsync(teacherId, trimmedName, sessionId);
        if (nameExists)
            return Result<SessionDto>.Failure(_localizer, "SessionNameDuplicate", HttpStatusCode.Conflict);

        // 3. Validate occurrence type change restriction (REQ-SES-009)
        bool occurrenceChanged = session.OccurrenceType != dto.OccurrenceType
                              || session.SelectedDays != FormatSelectedDays(dto.SelectedDays);
        if (occurrenceChanged)
        {
            bool hasConstraints = await _unitOfWork.SessionsRepo.HasStudentsOrLinksAsync(sessionId);
            if (hasConstraints)
                return Result<SessionDto>.Failure(_localizer, "SessionOccurrenceNotEditable", HttpStatusCode.BadRequest);
        }

        // 4. Validate occurrence configuration
        var occurrenceError = ValidateOccurrenceConfiguration(dto.OccurrenceType, dto.SelectedDays, dto.MonthlyDayOfMonth);
        if (occurrenceError is not null)
            return Result<SessionDto>.Failure(occurrenceError, HttpStatusCode.BadRequest);

        // 5. Validate date range (REQ-SES-014)
        if (dto.EndDate <= dto.StartDate)
            return Result<SessionDto>.Failure(_localizer, "SessionEndDateMustBeAfterStartDate", HttpStatusCode.BadRequest);

        // 6. Validate group if provided
        if (dto.SessionGroupId.HasValue)
        {
            var group = await _unitOfWork.SessionsRepo.GetGroupByIdAndTeacherAsync(dto.SessionGroupId.Value, teacherId);
            if (group is null)
                return Result<SessionDto>.Failure(_localizer, "SessionGroupNotFound", HttpStatusCode.NotFound);
        }

        // 7. Apply updates
        session.SessionName = trimmedName;
        session.OccurrenceType = dto.OccurrenceType;
        session.SelectedDays = FormatSelectedDays(dto.SelectedDays);
        session.MonthlyDayOfMonth = dto.OccurrenceType == OccurrenceType.Monthly ? dto.MonthlyDayOfMonth : null;
        session.PaymentType = dto.PaymentType;
        session.SessionAmount = dto.SessionAmount;
        session.StartDate = dto.StartDate;
        session.EndDate = dto.EndDate;
        session.StartTime = dto.StartTime;
        session.DurationMinutes = dto.DurationMinutes;
        session.SessionGroupId = dto.SessionGroupId;

        await _unitOfWork.SessionsRepo.UpdateAsync(session);
        await _unitOfWork.SaveChangesAsync();

        var resultDto = await BuildSessionDtoAsync(session);
        return Result<SessionDto>.Success(resultDto, _localizer, "SessionUpdatedSuccess");
    }

    /// <inheritdoc />
    public async Task<Result<SessionDeleteConfirmationDto>> GetDeleteConfirmationAsync(long teacherId, long sessionId)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<SessionDeleteConfirmationDto>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        // REQ-SES-040: Student count
        int studentCount = await _unitOfWork.SessionsRepo.CountStudentsBySessionAsync(sessionId);

        // REQ-SES-047: Linked session names
        var linkedSessions = await _unitOfWork.SessionsRepo.GetLinkedSessionsAsync(sessionId);

        var confirmation = new SessionDeleteConfirmationDto
        {
            SessionId = session.Id,
            SessionName = session.SessionName,
            AssignedStudentCount = studentCount,
            AffectedLinkedSessions = linkedSessions.Select(ls => new LinkedSessionInfo
            {
                Id = ls.Id,
                SessionName = ls.SessionName
            }).ToList()
        };

        return Result<SessionDeleteConfirmationDto>.Success(confirmation, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteSessionAsync(long teacherId, long sessionId)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<bool>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // REQ-SES-043: Remove all links where this session is on the Restrict side
            // (the Cascade side is handled by the DB, but the Restrict side needs manual cleanup)
            var linksAsTarget = await _unitOfWork.SessionsRepo.GetLinksBySessionAsync(sessionId);
            foreach (var link in linksAsTarget)
            {
                await _unitOfWork.SessionsRepo.DeleteLinkAsync(link);
            }

            // REQ-SES-041: Hard delete the session
            // REQ-SES-042: DB cascade SetNull clears TeacherStudents.SessionId automatically
            await _unitOfWork.SessionsRepo.DeleteAsync(session);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<bool>.Success(true, _localizer, "SessionDeletedSuccess");
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<SessionDto>> DuplicateSessionAsync(long teacherId, long sessionId)
    {
        // 1. Load source session
        var source = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (source is null)
            return Result<SessionDto>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        // 2. Generate a new name for the duplicate
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        var language = config?.SessionNameLanguage ?? GenerationLanguage.English;
        string newName = await _nameGenerator.GenerateNextNameAsync(teacherId, language);

        // 3. REQ-SES-046: Copy configuration fields, blank start/end dates
        var duplicate = new Session
        {
            TeacherId = teacherId,
            SessionName = newName,
            OccurrenceType = source.OccurrenceType,
            SelectedDays = source.SelectedDays,
            MonthlyDayOfMonth = source.MonthlyDayOfMonth,
            PaymentType = source.PaymentType,
            SessionAmount = source.SessionAmount,
            // REQ-SES-046: Blank start and end dates — use today as placeholder
            // The tutor is expected to set the actual dates after duplicating
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddMonths(1),
            StartTime = source.StartTime,
            DurationMinutes = source.DurationMinutes,
            SessionGroupId = source.SessionGroupId,
            CreateAt = DateTime.UtcNow
        };

        await _unitOfWork.SessionsRepo.AddAsync(duplicate);
        await _unitOfWork.SaveChangesAsync();

        var dto = await BuildSessionDtoAsync(duplicate);
        return Result<SessionDto>.Success(dto, _localizer, "SessionDuplicatedSuccess", HttpStatusCode.Created);
    }

    // ══════════════════════════════════════════════
    // SESSION LIST (SEARCH + FILTER + PAGINATION)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<SessionDto>>>> GetSessionListAsync(
        long teacherId, SessionListRequest request)
    {
        // Validate teacher exists
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<PaginatedResponse<List<SessionDto>>>.Failure(
                _localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // Build the filtered, sorted query via repo
        var query = _unitOfWork.SessionsRepo.BuildSessionListQuery(
            teacherId,
            request.Search,
            request.GroupId,
            request.OccurrenceType,
            request.ActiveOnly,
            request.ExpiredOnly,
            request.SortBy,
            request.SortDirection);

        // Get total count AFTER filters
        int totalCount = await _unitOfWork.SessionsRepo.CountAsync(query);

        // Get the paginated results
        var sessions = await _unitOfWork.SessionsRepo.GetPagedAsync(query, request.Page, request.PageSize);

        // Build DTOs with student counts and linked session info
        var today = DateTime.UtcNow.Date;
        var dtos = new List<SessionDto>();
        foreach (var session in sessions)
        {
            var dto = await BuildSessionDtoAsync(session);
            dtos.Add(dto);
        }

        var response = new PaginatedResponse<List<SessionDto>>
        {
            totalCount = totalCount,
            page = request.Page,
            pageSize = request.PageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
            data = dtos
        };

        return Result<PaginatedResponse<List<SessionDto>>>.Success(response, _localizer, "Success");
    }

    // ══════════════════════════════════════════════
    // SESSION GROUPS
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<SessionGroupDto>> CreateGroupAsync(CreateSessionGroupDto dto)
    {
        // Validate teacher
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(dto.TeacherId);
        if (teacher is null)
            return Result<SessionGroupDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // Validate unique group name
        string trimmedName = dto.GroupName.Trim();
        bool nameExists = await _unitOfWork.SessionsRepo.GroupNameExistsAsync(dto.TeacherId, trimmedName);
        if (nameExists)
            return Result<SessionGroupDto>.Failure(_localizer, "SessionGroupNameDuplicate", HttpStatusCode.Conflict);

        var group = new SessionGroup
        {
            TeacherId = dto.TeacherId,
            GroupName = trimmedName,
            CreateAt = DateTime.UtcNow
        };

        await _unitOfWork.SessionsRepo.AddGroupAsync(group);
        await _unitOfWork.SaveChangesAsync();

        var resultDto = new SessionGroupDto
        {
            Id = group.Id,
            TeacherId = group.TeacherId,
            GroupName = group.GroupName,
            SessionCount = 0,
            CreatedAt = group.CreateAt
        };

        return Result<SessionGroupDto>.Success(resultDto, _localizer, "SessionGroupCreatedSuccess", HttpStatusCode.Created);
    }

    /// <inheritdoc />
    public async Task<Result<List<SessionGroupDto>>> GetGroupsAsync(long teacherId)
    {
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<List<SessionGroupDto>>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        var groups = await _unitOfWork.SessionsRepo.GetGroupsByTeacherAsync(teacherId);

        // Build DTOs with session counts
        var dtos = new List<SessionGroupDto>();
        foreach (var group in groups)
        {
            // Count sessions in each group via the repo's session list query
            var sessionQuery = _unitOfWork.SessionsRepo.BuildSessionListQuery(
                teacherId, groupId: group.Id);
            int sessionCount = await _unitOfWork.SessionsRepo.CountAsync(sessionQuery);

            dtos.Add(new SessionGroupDto
            {
                Id = group.Id,
                TeacherId = group.TeacherId,
                GroupName = group.GroupName,
                SessionCount = sessionCount,
                CreatedAt = group.CreateAt
            });
        }

        return Result<List<SessionGroupDto>>.Success(dtos, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<SessionGroupDto>> RenameGroupAsync(long teacherId, long groupId, RenameSessionGroupDto dto)
    {
        var group = await _unitOfWork.SessionsRepo.GetGroupByIdAndTeacherAsync(groupId, teacherId);
        if (group is null)
            return Result<SessionGroupDto>.Failure(_localizer, "SessionGroupNotFound", HttpStatusCode.NotFound);

        string trimmedName = dto.GroupName.Trim();
        bool nameExists = await _unitOfWork.SessionsRepo.GroupNameExistsExcludingAsync(teacherId, trimmedName, groupId);
        if (nameExists)
            return Result<SessionGroupDto>.Failure(_localizer, "SessionGroupNameDuplicate", HttpStatusCode.Conflict);

        group.GroupName = trimmedName;
        await _unitOfWork.SessionsRepo.UpdateGroupAsync(group);
        await _unitOfWork.SaveChangesAsync();

        // Count sessions for DTO
        var sessionQuery = _unitOfWork.SessionsRepo.BuildSessionListQuery(teacherId, groupId: groupId);
        int sessionCount = await _unitOfWork.SessionsRepo.CountAsync(sessionQuery);

        var resultDto = new SessionGroupDto
        {
            Id = group.Id,
            TeacherId = group.TeacherId,
            GroupName = group.GroupName,
            SessionCount = sessionCount,
            CreatedAt = group.CreateAt
        };

        return Result<SessionGroupDto>.Success(resultDto, _localizer, "SessionGroupRenamedSuccess");
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteGroupAsync(long teacherId, long groupId)
    {
        var group = await _unitOfWork.SessionsRepo.GetGroupByIdAndTeacherAsync(groupId, teacherId);
        if (group is null)
            return Result<bool>.Failure(_localizer, "SessionGroupNotFound", HttpStatusCode.NotFound);

        // REQ-SES-031: Deleting group does NOT delete sessions.
        // Sessions become ungrouped via DB SetNull cascade on SessionGroupId FK.
        await _unitOfWork.SessionsRepo.DeleteGroupAsync(group);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, "SessionGroupDeletedSuccess");
    }

    // ══════════════════════════════════════════════
    // SESSION LINKING (MEMBERSHIP)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<bool>> CreateLinkAsync(CreateSessionLinkDto dto)
    {
        // 1. Validate both sessions exist and belong to the teacher
        if (dto.SessionIdA == dto.SessionIdB)
            return Result<bool>.Failure(_localizer, "SessionLinkSameSession", HttpStatusCode.BadRequest);

        var sessionA = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionIdA, dto.TeacherId);
        if (sessionA is null)
            return Result<bool>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        var sessionB = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionIdB, dto.TeacherId);
        if (sessionB is null)
            return Result<bool>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        // 2. BR-SES-003: Validate identical occurrence type AND day configuration
        if (sessionA.OccurrenceType != sessionB.OccurrenceType)
            return Result<bool>.Failure(_localizer, "SessionLinkOccurrenceMismatch", HttpStatusCode.BadRequest);

        if (sessionA.SelectedDays != sessionB.SelectedDays)
            return Result<bool>.Failure(_localizer, "SessionLinkDaysMismatch", HttpStatusCode.BadRequest);

        // 3. Check link doesn't already exist
        bool linkExists = await _unitOfWork.SessionsRepo.LinkExistsAsync(dto.SessionIdA, dto.SessionIdB);
        if (linkExists)
            return Result<bool>.Failure(_localizer, "SessionLinkAlreadyExists", HttpStatusCode.Conflict);

        // 4. Create the link with canonical ordering (lower Id first)
        long lower = Math.Min(dto.SessionIdA, dto.SessionIdB);
        long upper = Math.Max(dto.SessionIdA, dto.SessionIdB);

        var link = new SessionLink
        {
            SessionId = lower,
            LinkedSessionId = upper,
            CreateAt = DateTime.UtcNow
        };

        await _unitOfWork.SessionsRepo.AddLinkAsync(link);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, "SessionLinkCreatedSuccess", HttpStatusCode.Created);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> RemoveLinkAsync(long teacherId, long sessionIdA, long sessionIdB)
    {
        // Validate at least one session belongs to the teacher (security check)
        var sessionA = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionIdA, teacherId);
        if (sessionA is null)
            return Result<bool>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        // Find the link
        var link = await _unitOfWork.SessionsRepo.GetLinkAsync(sessionIdA, sessionIdB);
        if (link is null)
            return Result<bool>.Failure(_localizer, "SessionLinkNotFound", HttpStatusCode.NotFound);

        // REQ-SES-037: Remove link — does not affect sessions or student assignments
        await _unitOfWork.SessionsRepo.DeleteLinkAsync(link);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, "SessionLinkRemovedSuccess");
    }

    // ══════════════════════════════════════════════
    // STUDENT ASSIGNMENT
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<AssignStudentsResultDto>> AssignStudentsAsync(AssignStudentsToSessionDto dto)
    {
        // 1. Validate session exists and belongs to teacher
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionId, dto.TeacherId);
        if (session is null)
            return Result<AssignStudentsResultDto>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        // 2. Load all requested students
        var students = await _unitOfWork.Students.GetActiveByIdsAndTeacherAsync(dto.TeacherId, dto.StudentIds);
        if (students.Count == 0)
            return Result<AssignStudentsResultDto>.Failure(_localizer, "NoValidStudentsFound", HttpStatusCode.BadRequest);

        var result = new AssignStudentsResultDto();
        int assignedCount = 0;

        foreach (var student in students)
        {
            // REQ-SES-018: Check if student is already assigned to another session
            if (student.SessionId.HasValue && student.SessionId.Value != dto.SessionId)
            {
                // Load the current session to get its name for the warning
                var currentSession = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(student.SessionId.Value, dto.TeacherId);
                string currentSessionName = currentSession?.SessionName ?? "Unknown";

                result.Warnings.Add(new StudentReassignmentWarning
                {
                    StudentId = student.Id,
                    StudentName = student.StudentName,
                    StudentCode = student.StudentCode,
                    CurrentSessionId = student.SessionId.Value,
                    CurrentSessionName = currentSessionName,
                    NewSessionId = dto.SessionId,
                    NewSessionName = session.SessionName
                });
            }
            else
            {
                // Assign directly — student has no current session or is already in this session
                student.SessionId = dto.SessionId;
                await _unitOfWork.Students.UpdateAsync(student);
                assignedCount++;
            }
        }

        if (assignedCount > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        result.AssignedCount = assignedCount;
        return Result<AssignStudentsResultDto>.Success(result, _localizer, "StudentsAssignedSuccess");
    }

    /// <inheritdoc />
    public async Task<Result<int>> ConfirmReassignStudentsAsync(long teacherId, long sessionId, List<long> studentIds)
    {
        // Validate session
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<int>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);

        // Load students
        var students = await _unitOfWork.Students.GetActiveByIdsAndTeacherAsync(teacherId, studentIds);
        if (students.Count == 0)
            return Result<int>.Failure(_localizer, "NoValidStudentsFound", HttpStatusCode.BadRequest);

        // REQ-SES-019: Override previous session assignment
        foreach (var student in students)
        {
            student.SessionId = sessionId;
            await _unitOfWork.Students.UpdateAsync(student);
        }

        await _unitOfWork.SaveChangesAsync();
        return Result<int>.Success(students.Count, _localizer, "StudentsReassignedSuccess");
    }

    /// <inheritdoc />
    public async Task<Result<bool>> UnassignStudentAsync(long teacherId, long sessionId, long studentId)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(studentId, teacherId);
        if (student is null)
            return Result<bool>.Failure(_localizer, "StudentNotFound", HttpStatusCode.NotFound);

        if (student.SessionId != sessionId)
            return Result<bool>.Failure(_localizer, "StudentNotInSession", HttpStatusCode.BadRequest);

        student.SessionId = null;
        await _unitOfWork.Students.UpdateAsync(student);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, "StudentUnassignedSuccess");
    }

    // ══════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Validates the occurrence type configuration (selected days for Weekly/BiWeekly,
    /// monthly day for Monthly). Returns a localized error message if invalid, null if valid.
    /// REQ-SES-007/008: Occurrence type rules.
    /// </summary>
    private string? ValidateOccurrenceConfiguration(
        OccurrenceType occurrenceType,
        List<int>? selectedDays,
        byte? monthlyDayOfMonth)
    {
        switch (occurrenceType)
        {
            case OccurrenceType.Weekly:
            case OccurrenceType.BiWeekly:
                // REQ-SES-008: At least one day required, max 7
                if (selectedDays is null || selectedDays.Count == 0)
                    return _localizer["SessionSelectedDaysRequired"];

                if (selectedDays.Count > MaxSelectedDays)
                    return _localizer["SessionSelectedDaysTooMany"];

                // Validate each day index is 0-6
                if (selectedDays.Any(d => !ValidDayIndices.Contains(d)))
                    return _localizer["SessionSelectedDaysInvalid"];

                // Check for duplicate day indices
                if (selectedDays.Distinct().Count() != selectedDays.Count)
                    return _localizer["SessionSelectedDaysDuplicate"];

                break;

            case OccurrenceType.Monthly:
                // Monthly requires a day-of-month
                if (!monthlyDayOfMonth.HasValue || monthlyDayOfMonth.Value < 1 || monthlyDayOfMonth.Value > 31)
                    return _localizer["SessionMonthlyDayRequired"];

                break;

            default:
                return _localizer["SessionOccurrenceTypeInvalid"];
        }

        return null;
    }

    /// <summary>
    /// Formats a list of day indices into a comma-separated string for database storage.
    /// Returns null if the list is null or empty (for Monthly occurrence type).
    /// Sorts the indices to ensure consistent storage and comparison for BR-SES-003.
    /// </summary>
    private static string? FormatSelectedDays(List<int>? days)
    {
        if (days is null || days.Count == 0)
            return null;

        // Sort for consistent storage — critical for BR-SES-003 membership matching
        return string.Join(",", days.OrderBy(d => d));
    }

    /// <summary>
    /// Parses a comma-separated day string back into a list of integers.
    /// Returns null if the input is null or empty.
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
    /// Builds a complete SessionDto from a Session entity, including:
    /// - Student count (REQ-SES-NFR-009)
    /// - Group name (for display)
    /// - Linked sessions (REQ-SES-034)
    /// - Expired status (REQ-SES-015)
    /// </summary>
    private async Task<SessionDto> BuildSessionDtoAsync(Session session)
    {
        int studentCount = await _unitOfWork.SessionsRepo.CountStudentsBySessionAsync(session.Id);
        var linkedSessions = await _unitOfWork.SessionsRepo.GetLinkedSessionsAsync(session.Id);

        // Load group name if grouped
        string? groupName = null;
        if (session.SessionGroupId.HasValue)
        {
            var group = await _unitOfWork.SessionsRepo.GetGroupByIdAndTeacherAsync(
                session.SessionGroupId.Value, session.TeacherId);
            groupName = group?.GroupName;
        }

        return new SessionDto
        {
            Id = session.Id,
            TeacherId = session.TeacherId,
            SessionName = session.SessionName,
            OccurrenceType = session.OccurrenceType,
            SelectedDays = ParseSelectedDays(session.SelectedDays),
            MonthlyDayOfMonth = session.MonthlyDayOfMonth,
            PaymentType = session.PaymentType,
            SessionAmount = session.SessionAmount,
            StartDate = session.StartDate,
            EndDate = session.EndDate,
            StartTime = session.StartTime,
            DurationMinutes = session.DurationMinutes,
            SessionGroupId = session.SessionGroupId,
            SessionGroupName = groupName,
            StudentCount = studentCount,
            IsExpired = session.EndDate < DateTime.UtcNow.Date,
            LinkedSessions = linkedSessions.Select(ls => new LinkedSessionInfo
            {
                Id = ls.Id,
                SessionName = ls.SessionName
            }).ToList(),
            CreatedAt = session.CreateAt
        };
    }
}