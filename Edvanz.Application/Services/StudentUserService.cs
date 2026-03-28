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
/// All database access goes through IUnitOfWork + IGenericRepo.
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
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var studentUserRepo = _unitOfWork.GetRepository<StudentUser, long>();

        // Validate the user exists and is of type Student
        var user = await userRepo.FindAsync(u => u.Id == dto.UserId && u.UserType == UserType.Student);
        if (user is null)
            return Result<StudentUserProfileDto>.Failure(_localizer, "UserNotFound", HttpStatusCode.NotFound);

        // Ensure no duplicate StudentUser record for this user
        bool alreadyExists = await studentUserRepo.AnyAsync(s => s.UserId == dto.UserId);
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

            await studentUserRepo.AddAsync(studentUser);
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
        var studentUserRepo = _unitOfWork.GetRepository<StudentUser, long>();
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var linkRepo = _unitOfWork.GetRepository<StudentTeacherLink, long>();

        var studentUser = await studentUserRepo.FindAsync(s => s.Id == studentUserId && s.DeletedAt == null);
        if (studentUser is null)
            return Result<StudentUserProfileDto>.Failure(_localizer, "StudentUserNotFound", HttpStatusCode.NotFound);

        var user = await userRepo.FindAsync(u => u.Id == studentUser.UserId);
        if (user is null)
            return Result<StudentUserProfileDto>.Failure(_localizer, "UserNotFound", HttpStatusCode.NotFound);

        // Count active links for the profile summary
        int linkedCount = await linkRepo.CountAsync(l =>
            l.StudentUserId == studentUserId && l.LinkStatus == LinkStatus.Active);

        var profileDto = BuildProfileDto(studentUser, user, linkedCount);

        return Result<StudentUserProfileDto>.Success(profileDto, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<StudentUserProfileDto>> UpdateStudentUserProfileAsync(
        long studentUserId, UpdateStudentUserProfileDto dto)
    {
        var studentUserRepo = _unitOfWork.GetRepository<StudentUser, long>();
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var linkRepo = _unitOfWork.GetRepository<StudentTeacherLink, long>();

        var studentUser = await studentUserRepo.FindAsync(s => s.Id == studentUserId && s.DeletedAt == null);
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

        await studentUserRepo.UpdateAsync(studentUser);
        await _unitOfWork.SaveChangesAsync();

        // Reload user for profile DTO
        var user = await userRepo.FindAsync(u => u.Id == studentUser.UserId);
        int linkedCount = await linkRepo.CountAsync(l =>
            l.StudentUserId == studentUserId && l.LinkStatus == LinkStatus.Active);

        var profileDto = BuildProfileDto(studentUser, user!, linkedCount);

        return Result<StudentUserProfileDto>.Success(profileDto, _localizer, "StudentUserProfileUpdated", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<StudentDashboardDto>> GetDashboardAsync(long studentUserId)
    {
        var studentUserRepo = _unitOfWork.GetRepository<StudentUser, long>();

        var studentUser = await studentUserRepo.FindAsync(s => s.Id == studentUserId && s.DeletedAt == null);
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
        var studentUserRepo = _unitOfWork.GetRepository<StudentUser, long>();
        var teacherRepo = _unitOfWork.GetRepository<Teacher, long>();
        var teacherStudentRepo = _unitOfWork.GetRepository<TeacherStudent, long>();
        var linkRepo = _unitOfWork.GetRepository<StudentTeacherLink, long>();
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var teacherSubjectRepo = _unitOfWork.GetRepository<TeacherSubject, long>();
        var subjectRepo = _unitOfWork.GetRepository<Subject, long>();
        var configRepo = _unitOfWork.GetRepository<TeacherConfiguration, long>();

        // ── 1. Validate student user exists ──
        var studentUser = await studentUserRepo.FindAsync(s => s.Id == studentUserId && s.DeletedAt == null);
        if (studentUser is null)
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "StudentUserNotFound", HttpStatusCode.NotFound);

        // ── 2. Validate TeacherCode (credential #1) ──
        if (string.IsNullOrWhiteSpace(dto.TeacherCode) || dto.TeacherCode.Length != 8)
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "InvalidTeacherCode", HttpStatusCode.BadRequest);

        var teacher = await teacherRepo.FindAsync(t =>
            t.TeacherCode == dto.TeacherCode &&
            t.AccountStatus == AccountStatus.Active &&
            t.DeletedAt == null);

        if (teacher is null)
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // ── 3. Check if already linked to this teacher ──
        bool alreadyLinked = await linkRepo.AnyAsync(l =>
            l.StudentUserId == studentUserId &&
            l.TeacherId == teacher.Id &&
            l.LinkStatus == LinkStatus.Active);

        if (alreadyLinked)
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "TeacherAlreadyLinked", HttpStatusCode.Conflict);

        // ── 4. Validate StudentCode + HashedToken (credentials #2 and #3) ──
        if (string.IsNullOrWhiteSpace(dto.StudentCode))
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "StudentCodeRequired", HttpStatusCode.BadRequest);

        if (string.IsNullOrWhiteSpace(dto.HashedToken))
            return Result<StudentDashboardTeacherDto>.Failure(_localizer, "HashedTokenRequired", HttpStatusCode.BadRequest);

        // Normalize student code to uppercase for case-insensitive matching (REQ-STU-CODE-003)
        string normalizedStudentCode = dto.StudentCode.Trim().ToUpperInvariant();

        var teacherStudent = await teacherStudentRepo.FindAsync(ts =>
            ts.TeacherId == teacher.Id &&
            ts.StudentCode == normalizedStudentCode &&
            ts.HashedToken == dto.HashedToken.Trim() &&
            !ts.IsDeleted);

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

            await linkRepo.AddAsync(link);

            // Update IsFirstLogin if this is the student's first teacher link
            if (studentUser.IsFirstLogin)
            {
                studentUser.IsFirstLogin = false;
                await studentUserRepo.UpdateAsync(studentUser);
            }

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var dashboardTeacher = await BuildDashboardTeacherDtoAsync(
                link, teacher, userRepo, teacherSubjectRepo, subjectRepo, configRepo);

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
        var linkRepo = _unitOfWork.GetRepository<StudentTeacherLink, long>();

        var link = await linkRepo.FindAsync(l =>
            l.StudentUserId == studentUserId &&
            l.TeacherId == teacherId &&
            l.LinkStatus == LinkStatus.Active);

        if (link is null)
            return Result<bool>.Failure(_localizer, "LinkNotFound", HttpStatusCode.NotFound);

        // Soft-unlink: preserve the record for audit
        link.LinkStatus = LinkStatus.Unlinked;
        link.UnlinkedAt = DateTime.UtcNow;

        await linkRepo.UpdateAsync(link);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, "TeacherUnlinkSuccess", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<List<StudentDashboardTeacherDto>>> GetLinkedTeachersAsync(long studentUserId)
    {
        var studentUserRepo = _unitOfWork.GetRepository<StudentUser, long>();
        var linkRepo = _unitOfWork.GetRepository<StudentTeacherLink, long>();
        var teacherRepo = _unitOfWork.GetRepository<Teacher, long>();
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var teacherSubjectRepo = _unitOfWork.GetRepository<TeacherSubject, long>();
        var subjectRepo = _unitOfWork.GetRepository<Subject, long>();
        var configRepo = _unitOfWork.GetRepository<TeacherConfiguration, long>();

        // Validate student user exists
        bool exists = await studentUserRepo.AnyAsync(s => s.Id == studentUserId && s.DeletedAt == null);
        if (!exists)
            return Result<List<StudentDashboardTeacherDto>>.Failure(_localizer, "StudentUserNotFound", HttpStatusCode.NotFound);

        // Get all active links for this student
        var activeLinks = await linkRepo.GetAsync(l =>
            l.StudentUserId == studentUserId &&
            l.LinkStatus == LinkStatus.Active);

        if (!activeLinks.Any())
        {
            return Result<List<StudentDashboardTeacherDto>>.Success(
                new List<StudentDashboardTeacherDto>(), _localizer, "Success", HttpStatusCode.OK);
        }

        // Build dashboard DTOs for each linked teacher
        var dashboardTeachers = new List<StudentDashboardTeacherDto>();

        foreach (var link in activeLinks)
        {
            var teacher = await teacherRepo.FindAsync(t => t.Id == link.TeacherId && t.DeletedAt == null);
            if (teacher is null) continue; // Teacher account deleted — skip

            var dashboardTeacher = await BuildDashboardTeacherDtoAsync(
                link, teacher, userRepo, teacherSubjectRepo, subjectRepo, configRepo);

            dashboardTeachers.Add(dashboardTeacher);
        }

        return Result<List<StudentDashboardTeacherDto>>.Success(
            dashboardTeachers, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<StudentUserProfileDto>> GetStudentUserByAccountCodeAsync(string accountCode)
    {
        var studentUserRepo = _unitOfWork.GetRepository<StudentUser, long>();
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var linkRepo = _unitOfWork.GetRepository<StudentTeacherLink, long>();

        if (string.IsNullOrWhiteSpace(accountCode))
            return Result<StudentUserProfileDto>.Failure(_localizer, "StudentAccountCodeRequired", HttpStatusCode.BadRequest);

        var studentUser = await studentUserRepo.FindAsync(s =>
            s.StudentAccountCode == accountCode.Trim().ToUpperInvariant() &&
            s.DeletedAt == null);

        if (studentUser is null)
            return Result<StudentUserProfileDto>.Failure(_localizer, "StudentUserNotFound", HttpStatusCode.NotFound);

        var user = await userRepo.FindAsync(u => u.Id == studentUser.UserId);
        if (user is null)
            return Result<StudentUserProfileDto>.Failure(_localizer, "UserNotFound", HttpStatusCode.NotFound);

        int linkedCount = await linkRepo.CountAsync(l =>
            l.StudentUserId == studentUser.Id && l.LinkStatus == LinkStatus.Active);

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
    /// </summary>
    private static async Task<StudentDashboardTeacherDto> BuildDashboardTeacherDtoAsync(
        StudentTeacherLink link,
        Teacher teacher,
        IGenericRepo<User, long> userRepo,
        IGenericRepo<TeacherSubject, long> teacherSubjectRepo,
        IGenericRepo<Subject, long> subjectRepo,
        IGenericRepo<TeacherConfiguration, long> configRepo)
    {
        // Load the teacher's user record for the full name
        var teacherUser = await userRepo.FindAsync(u => u.Id == teacher.UserId);

        // Load the teacher's first subject for display
        string subjectName = teacher.CustomSubject ?? string.Empty;
        var teacherSubjects = await teacherSubjectRepo.GetAsync(ts => ts.TeacherId == teacher.Id);
        if (teacherSubjects.Any())
        {
            var firstSubject = await subjectRepo.FindAsync(s => s.Id == teacherSubjects.First().SubjectId);
            if (firstSubject is not null)
                subjectName = firstSubject.NameEn; // TODO: respect language preference
        }

        // Load visibility configuration (AAM-FR-04.8 / AAM-FR-05.8)
        var config = await configRepo.FindAsync(c => c.TeacherId == teacher.Id);

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