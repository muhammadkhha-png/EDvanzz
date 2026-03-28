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
/// </summary>
public class StudentUserService : IStudentUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStudentAccountCodeGenerator _codeGenerator;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public StudentUserService(
        IUnitOfWork unitOfWork,
        IStudentAccountCodeGenerator codeGenerator,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _codeGenerator = codeGenerator;
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

        // Count active links for the profile summary
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

        // Validate language preference if provided
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

        // Get linked teachers for the dashboard
        var linkedTeachersResult = await GetLinkedTeachersAsync(studentUserId);

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
    public async Task<Result<StudentDashboardTeacherDto>> LinkTeacherAsync(long studentUserId, LinkTeacherDto dto)
    {
        // ── 1. Validate student user exists ──
        var studentUser = await _unitOfWork.Users.GetActiveStudentUserByIdAsync(studentUserId);
        if (studentUser is null)
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "StudentUserNotFound", HttpStatusCode.NotFound);

        // ── 2. Validate TeacherCode (credential #1) ──
        if (string.IsNullOrWhiteSpace(dto.TeacherCode) || dto.TeacherCode.Length != 8)
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "InvalidTeacherCode", HttpStatusCode.BadRequest);

        var teacher = await _unitOfWork.Users.GetActiveTeacherByCodeAsync(dto.TeacherCode);

        if (teacher is null)
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // ── 3. Check if already linked to this teacher ──
        bool alreadyLinked = await _unitOfWork.Users.StudentTeacherLinkExistsAsync(studentUserId, teacher.Id);

        if (alreadyLinked)
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "TeacherAlreadyLinked", HttpStatusCode.Conflict);

        // ── 4. Validate StudentCode + HashedToken (credentials #2 and #3) ──
        if (string.IsNullOrWhiteSpace(dto.StudentCode))
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "StudentCodeRequired", HttpStatusCode.BadRequest);

        if (string.IsNullOrWhiteSpace(dto.HashedToken))
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "HashedTokenRequired", HttpStatusCode.BadRequest);

        // Normalize student code to uppercase for case-insensitive matching (REQ-STU-CODE-003)
        string normalizedStudentCode = dto.StudentCode.Trim().ToUpperInvariant();

        var teacherStudent = await _unitOfWork.Users.GetTeacherStudentByLinkingCredentialsAsync(
            teacher.Id, normalizedStudentCode, dto.HashedToken.Trim());

        if (teacherStudent is null)
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "InvalidLinkCredentials", HttpStatusCode.BadRequest);

        // ── 5. Create the link (transaction-safe) ──
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            var link = new StudentTeacherLink
            {
                StudentUserId = studentUserId,
                TeacherId = teacher.Id,
                TeacherStudentId = teacherStudent.Id,
                LinkStatus = LinkStatus.Active,
                LinkedAt = DateTime.UtcNow,
                CreateAt = DateTime.UtcNow
            };

            await _unitOfWork.Users.AddStudentTeacherLinkAsync(link);

            // Update IsFirstLogin if this is the student's first teacher link
            if (studentUser.IsFirstLogin)
            {
                studentUser.IsFirstLogin = false;
                await _unitOfWork.Users.UpdateStudentUserAsync(studentUser);
            }

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var dashboardTeacher = await BuildDashboardTeacherDtoAsync(link, teacher);

            return Result<StudentDashboardTeacherDto>.Success(dashboardTeacher, _localizer, "TeacherLinkSuccess", HttpStatusCode.Created);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> UnlinkTeacherAsync(long studentUserId, long teacherId)
    {
        var link = await _unitOfWork.Users.GetActiveStudentTeacherLinkAsync(studentUserId, teacherId);

        if (link is null)
            return Result<bool>.Failure(_localizer, "LinkNotFound", HttpStatusCode.NotFound);

        // Soft-unlink: preserve the record for audit
        link.LinkStatus = LinkStatus.Unlinked;
        link.UnlinkedAt = DateTime.UtcNow;

        await _unitOfWork.Users.UpdateStudentTeacherLinkAsync(link);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, "TeacherUnlinkSuccess", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<List<StudentDashboardTeacherDto>>> GetLinkedTeachersAsync(long studentUserId)
    {
        // Validate student user exists
        var studentUser = await _unitOfWork.Users.GetActiveStudentUserByIdAsync(studentUserId);
        if (studentUser is null)
            return Result<List<StudentDashboardTeacherDto>>.Failure(_localizer, "StudentUserNotFound", HttpStatusCode.NotFound);

        // Get all active links for this student
        var activeLinks = await _unitOfWork.Users.GetActiveStudentTeacherLinksAsync(studentUserId);

        if (!activeLinks.Any())
        {
            return Result<List<StudentDashboardTeacherDto>>.Success(
                new List<StudentDashboardTeacherDto>(), _localizer, "Success", HttpStatusCode.OK);
        }

        // Build dashboard DTOs for each linked teacher
        var dashboardTeachers = new List<StudentDashboardTeacherDto>();

        foreach (var link in activeLinks)
        {
            var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(link.TeacherId);
            if (teacher is null) continue; // Teacher account deleted — skip

            var dashboardTeacher = await BuildDashboardTeacherDtoAsync(link, teacher);

            dashboardTeachers.Add(dashboardTeacher);
        }

        return Result<List<StudentDashboardTeacherDto>>.Success(
            dashboardTeachers, _localizer, "Success", HttpStatusCode.OK);
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
    /// Builds a StudentDashboardTeacherDto by loading teacher's user info, subject, and configuration.
    /// Used by both LinkTeacherAsync and GetLinkedTeachersAsync.
    /// All queries go through IUserRepo named methods — no raw expressions.
    /// </summary>
    private async Task<StudentDashboardTeacherDto> BuildDashboardTeacherDtoAsync(
        StudentTeacherLink link,
        Teacher teacher)
    {
        // Load the teacher's user record for the full name
        var teacherUser = await _unitOfWork.Users.GetUserByIdAsync(teacher.UserId);

        // Load the teacher's first subject for display
        string subjectName = teacher.CustomSubject ?? string.Empty;
        var teacherSubjects = await _unitOfWork.Users.GetTeacherSubjectsByTeacherIdAsync(teacher.Id);
        if (teacherSubjects.Any())
        {
            var firstSubject = await _unitOfWork.Users.GetSubjectByIdAsync(teacherSubjects.First().SubjectId);
            if (firstSubject is not null)
                subjectName = firstSubject.NameEn; // TODO: respect language preference
        }

        // Load visibility configuration (AAM-FR-04.8 / AAM-FR-05.8)
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacher.Id);

        return new StudentDashboardTeacherDto
        {
            LinkId = link.Id,
            TeacherCode = teacher.TeacherCode,
            TeacherFullName = teacherUser?.FullName ?? string.Empty,
            SubjectName = subjectName,
            LinkedAt = link.LinkedAt,
            IsEnrollmentActive = link.TeacherStudentId.HasValue,
            VisibilityAttendance = config?.StudentVisibilityAttendance ?? true,
            VisibilityPayment = config?.StudentVisibilityPayment ?? true,
            VisibilityHomework = config?.StudentVisibilityHomework ?? true,
            VisibilityExamDefault = config?.StudentVisibilityExamDefault ?? false
        };
    }
}