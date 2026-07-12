using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.StudentUser;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements all Student User module operations.
/// Follows the Result pattern for operation outcomes.
/// All database access goes through IUnitOfWork.Users (IUserRepo) — no direct
/// GetRepository calls with raw expression predicates.
/// 
/// ARCHITECTURAL NOTE:
/// All query logic is encapsulated in IUserRepo named methods.
/// If a query needs to change, you edit the repo method — not this service.
/// 
/// TRANSACTION SAFETY:
/// Methods that write multiple rows check _unitOfWork.HasActiveTransaction.
/// When called from User module registration (outer transaction active),
/// they participate in that transaction. When called standalone, they
/// manage their own. See ownsTransaction pattern in each write method.
/// 
/// FIX DB-1: Dashboard methods use batch loading via GetTeacherDashboardDataAsync
/// to eliminate N+1 query patterns. Previously each linked teacher caused 4 individual
/// DB round-trips; now all data is loaded in 4 total queries regardless of teacher count.
/// </summary>
public class StudentUserService : IStudentUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStudentAccountCodeGenerator _codeGenerator;
    private readonly IStudentLinkNotifier _linkNotifier;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public StudentUserService(
        IUnitOfWork unitOfWork,
        IStudentAccountCodeGenerator codeGenerator,
        IStudentLinkNotifier linkNotifier,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _codeGenerator = codeGenerator;
        _linkNotifier = linkNotifier;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<Result<StudentUserProfileDto>> InitializeStudentUserAsync(CreateStudentUserDto dto)
    {
        // Validate the user exists and is of type Student
        var user = await _unitOfWork.Users.GetByIdAndTypeAsync(dto.UserId, UserType.Student);
        if (user is null)
            return Result<StudentUserProfileDto>.Failure(_localizer, "UserNotFound", HttpStatusCode.NotFound);

        // Ensure no duplicate StudentUser record for this user
        bool alreadyExists = await _unitOfWork.Users.StudentUserExistsByUserIdAsync(dto.UserId);
        if (alreadyExists)
            return Result<StudentUserProfileDto>.Failure(_localizer, "StudentUserAlreadyInitialized", HttpStatusCode.Conflict);

        // If caller already started a transaction (User module registration),
        // we participate in it. Otherwise we manage our own.
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;

        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Generate unique student account code (AAM-FR-05.3 / AAM-NFR-03)
            string accountCode = await _codeGenerator.GenerateUniqueCodeAsync();

            // Create the StudentUser record
            var studentUser = new StudentUser
            {
                UserId = dto.UserId,
                StudentAccountCode = accountCode,
                LanguagePreference = dto.LanguagePreference,
                AccountStatus = AccountStatus.Active,
                IsFirstLogin = true,
                CreateAt = DateTime.UtcNow
            };

            await _unitOfWork.Users.AddStudentUserAsync(studentUser);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var profileDto = BuildProfileDto(studentUser, user, linkedTeacherCount: 0);
            return Result<StudentUserProfileDto>.Success(profileDto, _localizer, "StudentUserInitializedSuccess", HttpStatusCode.Created);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<StudentUserProfileDto>> GetStudentUserProfileAsync(long studentUserId)
    {
        var studentUser = await _unitOfWork.Users.GetActiveStudentUserByIdAsync(studentUserId);
        if (studentUser is null)
            return Result<StudentUserProfileDto>.Failure(_localizer, "StudentUserNotFound", HttpStatusCode.NotFound);

        var user = await _unitOfWork.Users.GetUserByIdAsync(studentUser.UserId);
        if (user is null)
            return Result<StudentUserProfileDto>.Failure(_localizer, "UserNotFound", HttpStatusCode.NotFound);

        int linkedCount = await _unitOfWork.Users.CountActiveStudentTeacherLinksAsync(studentUserId);

        var profileDto = BuildProfileDto(studentUser, user, linkedCount);

        return Result<StudentUserProfileDto>.Success(profileDto, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<StudentUserProfileDto>> UpdateStudentUserProfileAsync(
        long studentUserId, UpdateStudentUserProfileDto dto)
    {
        var studentUser = await _unitOfWork.Users.GetActiveStudentUserByIdAsync(studentUserId);
        if (studentUser is null)
            return Result<StudentUserProfileDto>.Failure(_localizer, "StudentUserNotFound", HttpStatusCode.NotFound);

        if (dto.LanguagePreference is not null &&
            dto.LanguagePreference != "en" && dto.LanguagePreference != "ar")
        {
            return Result<StudentUserProfileDto>.Failure(_localizer, "InvalidLanguagePreference", HttpStatusCode.BadRequest);
        }

        // Update language preference
        if (dto.LanguagePreference is not null)
            studentUser.LanguagePreference = dto.LanguagePreference;

        await _unitOfWork.Users.UpdateStudentUserAsync(studentUser);
        await _unitOfWork.SaveChangesAsync();

        // Reload user for profile DTO
        var user = await _unitOfWork.Users.GetUserByIdAsync(studentUser.UserId);
        int linkedCount = await _unitOfWork.Users.CountActiveStudentTeacherLinksAsync(studentUserId);

        var profileDto = BuildProfileDto(studentUser, user!, linkedCount);

        return Result<StudentUserProfileDto>.Success(profileDto, _localizer, "StudentUserProfileUpdated", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<StudentDashboardDto>> GetDashboardAsync(long studentUserId)
    {
        var studentUser = await _unitOfWork.Users.GetActiveStudentUserByIdAsync(studentUserId);
        if (studentUser is null)
            return Result<StudentDashboardDto>.Failure(_localizer, "StudentUserNotFound", HttpStatusCode.NotFound);

        // Get the student's teachers (including pending/rejected requests) for the dashboard
        var linkedTeachersResult = await GetMyTeachersAsync(studentUserId);

        var dashboardDto = new StudentDashboardDto
        {
            IsFirstLogin = studentUser.IsFirstLogin,
            StudentAccountCode = studentUser.StudentAccountCode,
            LinkedTeachers = linkedTeachersResult.IsSuccess && linkedTeachersResult.Data is not null
                ? linkedTeachersResult.Data
                : new List<StudentDashboardTeacherDto>()
        };

        return Result<StudentDashboardDto>.Success(dashboardDto, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<StudentDashboardTeacherDto>> CreateLinkRequestAsync(long studentUserId, CreateLinkRequestDto dto)
    {
        // ── 1. Validate student user exists ──
        var studentUser = await _unitOfWork.Users.GetActiveStudentUserByIdAsync(studentUserId);
        if (studentUser is null)
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "StudentUserNotFound", HttpStatusCode.NotFound);

        // ── 2. Resolve the teacher from the public 8-digit code ──
        if (string.IsNullOrWhiteSpace(dto.TeacherCode) || dto.TeacherCode.Trim().Length != 8)
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "InvalidTeacherCode", HttpStatusCode.BadRequest);

        var teacher = await _unitOfWork.Users.GetActiveTeacherByCodeAsync(dto.TeacherCode.Trim());
        if (teacher is null)
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // ── 3. Validate the student-typed name (the teacher identifies the request by it) ──
        if (string.IsNullOrWhiteSpace(dto.StudentName))
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "StudentNameRequired", HttpStatusCode.BadRequest);

        // ── 4. One live row per (student, teacher): reject if already pending/linked ──
        var liveLink = await _unitOfWork.Users.GetLiveStudentTeacherLinkAsync(studentUserId, teacher.Id);
        if (liveLink is not null)
        {
            return liveLink.LinkStatus == LinkStatus.Active
                ? Result<StudentDashboardTeacherDto>.Failure(_localizer, "TeacherAlreadyLinked", HttpStatusCode.Conflict)
                : Result<StudentDashboardTeacherDto>.Failure(_localizer, "LinkRequestAlreadyPending", HttpStatusCode.Conflict);
        }

        // ── 5. Create the Pending request (transaction-safe) ──
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            var now = DateTime.UtcNow;
            var link = new StudentTeacherLink
            {
                StudentUserId = studentUserId,
                TeacherId = teacher.Id,
                TeacherStudentId = null, // bound by the teacher at accept time
                LinkStatus = LinkStatus.Pending,
                RequestedStudentName = dto.StudentName.Trim(),
                RequestedStudentCode = string.IsNullOrWhiteSpace(dto.StudentCode)
                    ? null
                    : dto.StudentCode.Trim().ToUpperInvariant(),
                RequestedAt = now,
                // LinkedAt is non-nullable; it is overwritten with the accept time
                // when the teacher approves. Until then it mirrors RequestedAt.
                LinkedAt = now,
                CreateAt = now
            };

            await _unitOfWork.Users.AddStudentTeacherLinkAsync(link);

            // The dashboard is no longer "empty" once a request exists (AAM-FR-05.4)
            if (studentUser.IsFirstLogin)
            {
                studentUser.IsFirstLogin = false;
                await _unitOfWork.Users.UpdateStudentUserAsync(studentUser);
            }

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            // ── 6. Post-commit, best-effort: tell the teacher a request arrived ──
            try
            {
                await _linkNotifier.NotifyRequestReceivedAsync(teacher.Id, link.RequestedStudentName!);
            }
            catch { /* notification failure must not fail the request */ }

            var batchData = await _unitOfWork.Users.GetTeacherDashboardDataAsync(new List<long> { teacher.Id });
            var dashboardTeacher = BuildDashboardTeacherDtoFromBatch(link, teacher.Id, studentUser.LanguagePreference, batchData);

            return Result<StudentDashboardTeacherDto>.Success(dashboardTeacher, _localizer, "LinkRequestCreated", HttpStatusCode.Created);
        }
        catch (Exception ex) when (IsUniqueLinkViolation(ex))
        {
            // Race: two concurrent requests for the same pair — the filtered unique
            // index (LinkStatus IN Active,Pending) is the tiebreaker.
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "LinkRequestAlreadyPending", HttpStatusCode.Conflict);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> UnlinkTeacherAsync(long studentUserId, long teacherId, long actingUserId)
    {
        // The single live row is either Pending (cancel) or Active (unlink)
        var link = await _unitOfWork.Users.GetLiveStudentTeacherLinkAsync(studentUserId, teacherId);

        if (link is null)
            return Result<bool>.Failure(_localizer, "LinkNotFound", HttpStatusCode.NotFound);

        bool wasPending = link.LinkStatus == LinkStatus.Pending;

        // Soft transition: preserve the record for audit
        link.LinkStatus = wasPending ? LinkStatus.CancelledByStudent : LinkStatus.Unlinked;
        link.UnlinkedAt = DateTime.UtcNow;
        link.RemovedByUserId = actingUserId;

        await _unitOfWork.Users.UpdateStudentTeacherLinkAsync(link);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer,
            wasPending ? "LinkRequestCancelled" : "TeacherUnlinkSuccess", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    /// <summary>
    /// FIX DB-1: batch loading — 1 query for links + 4 queries for all teacher data.
    /// Request/approval flow: returns the LATEST link row per teacher across every
    /// status, so the student sees Pending requests and accepted/rejected outcomes
    /// on the same list. Pending/Active entries sort before historical ones.
    /// </summary>
    public async Task<Result<List<StudentDashboardTeacherDto>>> GetMyTeachersAsync(long studentUserId)
    {
        // Validate student user exists
        var studentUser = await _unitOfWork.Users.GetActiveStudentUserByIdAsync(studentUserId);
        if (studentUser is null)
            return Result<List<StudentDashboardTeacherDto>>.Failure(_localizer, "StudentUserNotFound", HttpStatusCode.NotFound);

        // All rows, newest first — reduce to the latest row per teacher so each
        // teacher appears exactly once with its current state (AAM-FR-05.7).
        var allLinks = await _unitOfWork.Users.GetAllStudentTeacherLinksAsync(studentUserId);
        var latestPerTeacher = allLinks
            .GroupBy(l => l.TeacherId)
            .Select(g => g.First()) // rows are ordered by Id DESC in the repo
            .ToList();

        if (!latestPerTeacher.Any())
        {
            return Result<List<StudentDashboardTeacherDto>>.Success(
                new List<StudentDashboardTeacherDto>(), _localizer, "Success", HttpStatusCode.OK);
        }

        // FIX DB-1: Batch load ALL teacher data in one call (4 queries total, not 4×N)
        var teacherIds = latestPerTeacher.Select(l => l.TeacherId).Distinct().ToList();
        var batchData = await _unitOfWork.Users.GetTeacherDashboardDataAsync(teacherIds);

        // Build dashboard DTOs from pre-loaded data — zero additional DB calls
        var dashboardTeachers = new List<StudentDashboardTeacherDto>();

        foreach (var link in latestPerTeacher)
        {
            if (!batchData.Teachers.ContainsKey(link.TeacherId))
                continue; // Teacher account deleted — skip

            var dashboardTeacher = BuildDashboardTeacherDtoFromBatch(
                link, link.TeacherId, studentUser.LanguagePreference, batchData);

            dashboardTeachers.Add(dashboardTeacher);
        }

        // Live entries first (Pending, then Active), then historical states,
        // newest activity first within each group.
        var ordered = dashboardTeachers
            .OrderBy(t => t.Status switch
            {
                nameof(LinkStatus.Pending) => 0,
                nameof(LinkStatus.Active) => 1,
                _ => 2
            })
            .ThenByDescending(t => t.RespondedAt ?? t.RequestedAt ?? t.LinkedAt)
            .ToList();

        return Result<List<StudentDashboardTeacherDto>>.Success(
            ordered, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<StudentUserProfileDto>> GetStudentUserByAccountCodeAsync(string accountCode)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
            return Result<StudentUserProfileDto>.Failure(_localizer, "StudentAccountCodeRequired", HttpStatusCode.BadRequest);

        var studentUser = await _unitOfWork.Users.GetStudentUserByAccountCodeAsync(accountCode);

        if (studentUser is null)
            return Result<StudentUserProfileDto>.Failure(_localizer, "StudentUserNotFound", HttpStatusCode.NotFound);

        var user = await _unitOfWork.Users.GetUserByIdAsync(studentUser.UserId);
        if (user is null)
            return Result<StudentUserProfileDto>.Failure(_localizer, "UserNotFound", HttpStatusCode.NotFound);

        int linkedCount = await _unitOfWork.Users.CountActiveStudentTeacherLinksAsync(studentUser.Id);

        var profileDto = BuildProfileDto(studentUser, user, linkedCount);

        return Result<StudentUserProfileDto>.Success(profileDto, _localizer, "Success", HttpStatusCode.OK);
    }

    // ══════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Builds a StudentUserProfileDto from the entity and user data.
    /// Centralizes the mapping to avoid duplication across methods.
    /// </summary>
    private static StudentUserProfileDto BuildProfileDto(StudentUser studentUser, User user, int linkedTeacherCount)
    {
        return new StudentUserProfileDto
        {
            Id = studentUser.Id,
            UserId = studentUser.UserId,
            StudentAccountCode = studentUser.StudentAccountCode,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            LanguagePreference = studentUser.LanguagePreference,
            AccountStatus = studentUser.AccountStatus.ToString(),
            IsFirstLogin = studentUser.IsFirstLogin,
            CreatedAt = studentUser.CreateAt,
            LinkedTeacherCount = linkedTeacherCount
        };
    }

    /// <summary>
    /// FIX DB-1 + FIX BUG-3: Builds a StudentDashboardTeacherDto from pre-loaded batch data.
    /// Zero database calls — all data resolved from in-memory dictionaries.
    /// Subject name respects the student's language preference (AAM-FR-02.2).
    /// </summary>
    private static StudentDashboardTeacherDto BuildDashboardTeacherDtoFromBatch(
        StudentTeacherLink link,
        long teacherId,
        string? languagePreference,
        TeacherDashboardBatchData batchData)
    {
        var teacher = batchData.Teachers.GetValueOrDefault(teacherId);
        string teacherFullName = string.Empty;
        if (teacher is not null && batchData.Users.TryGetValue(teacher.UserId, out var teacherUser))
            teacherFullName = teacherUser.FullName;

        // Resolve subject name with language preference
        string subjectName = teacher?.CustomSubject ?? string.Empty;
        if (teacher is not null &&
            batchData.TeacherSubjects.TryGetValue(teacherId, out var teacherSubjects) &&
            teacherSubjects.Any())
        {
            var firstSubjectId = teacherSubjects.First().SubjectId;
            if (batchData.Subjects.TryGetValue(firstSubjectId, out var subject))
            {
                // FIX BUG-3: Respect user's language preference for subject name (AAM-FR-02.2)
                subjectName = languagePreference == "ar" ? subject.NameAr : subject.NameEn;
            }
        }

        // Load STUDENT visibility settings (AAM-FR-04.8 / AAM-FR-05.8)
        batchData.Configurations.TryGetValue(teacherId, out var config);

        return new StudentDashboardTeacherDto
        {
            LinkId = link.Id,
            Status = link.LinkStatus.ToString(),
            RequestedAt = link.RequestedAt,
            RespondedAt = link.RespondedAt,
            TeacherCode = teacher?.TeacherCode ?? string.Empty,
            TeacherFullName = teacherFullName,
            SubjectName = subjectName,
            LinkedAt = link.LinkedAt,
            IsEnrollmentActive = link.LinkStatus == LinkStatus.Active && link.TeacherStudentId.HasValue,
            IsLinked = link.LinkStatus == LinkStatus.Active && link.TeacherStudentId.HasValue,
            VisibilityAttendance = config?.StudentVisibilityAttendance ?? true,
            VisibilityPayment = config?.StudentVisibilityPayment ?? true,
            VisibilityHomework = config?.StudentVisibilityHomework ?? true,
            VisibilityExamDefault = config?.StudentVisibilityExamDefault ?? false
        };
    }

    /// <summary>
    /// Architecture-safe unique-index violation detection (same rationale as
    /// AttendanceService.IsConcurrencyException / FIX H7): the Application layer
    /// cannot reference EF Core or SqlClient types, so we walk the exception
    /// chain by type name and match the filtered unique index name.
    /// </summary>
    private static bool IsUniqueLinkViolation(Exception ex)
    {
        var current = ex;
        while (current is not null)
        {
            if ((current.GetType().Name == "SqlException" || current.GetType().Name == "DbUpdateException") &&
                current.Message.Contains("IX_StudentTeacherLinks_StudentUserId_TeacherId"))
                return true;
            current = current.InnerException;
        }
        return false;
    }
}