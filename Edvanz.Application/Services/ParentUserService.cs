using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ParentUser;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements all Parent User module operations.
/// Follows the Result pattern for operation outcomes.
/// All database access goes through IUnitOfWork + IGenericRepo.
/// 
/// TRANSACTION SAFETY:
/// All transactional methods use the ownsTransaction pattern.
/// </summary>
public class ParentUserService : IParentUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public ParentUserService(
        IUnitOfWork unitOfWork,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<Result<ParentUserProfileDto>> InitializeParentUserAsync(CreateParentUserDto dto)
    {
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var parentRepo = _unitOfWork.GetRepository<ParentUser, long>();

        var user = await userRepo.FindAsync(u => u.Id == dto.UserId && u.UserType == UserType.Parent);
        if (user is null)
            return Result<ParentUserProfileDto>.Failure(_localizer, "UserNotFound", HttpStatusCode.NotFound);

        bool alreadyExists = await parentRepo.AnyAsync(p => p.UserId == dto.UserId);
        if (alreadyExists)
            return Result<ParentUserProfileDto>.Failure(_localizer, "ParentUserAlreadyInitialized", HttpStatusCode.Conflict);

        // Transaction-safe: participates in outer tx if active (User module registration)
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            var parentUser = new ParentUser
            {
                UserId = dto.UserId,
                LanguagePreference = dto.LanguagePreference,
                AccountStatus = AccountStatus.Active,
                CreateAt = DateTime.UtcNow
            };

            await parentRepo.AddAsync(parentUser);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var profileDto = BuildProfileDto(parentUser, user, childCount: 0);
            return Result<ParentUserProfileDto>.Success(profileDto, _localizer, "ParentUserInitializedSuccess", HttpStatusCode.Created);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<ParentUserProfileDto>> GetParentUserProfileAsync(long parentUserId)
    {
        var parentRepo = _unitOfWork.GetRepository<ParentUser, long>();
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var childRepo = _unitOfWork.GetRepository<ParentChild, long>();

        var parentUser = await parentRepo.FindAsync(p => p.Id == parentUserId && p.DeletedAt == null);
        if (parentUser is null)
            return Result<ParentUserProfileDto>.Failure(_localizer, "ParentUserNotFound", HttpStatusCode.NotFound);

        var user = await userRepo.FindAsync(u => u.Id == parentUser.UserId);
        if (user is null)
            return Result<ParentUserProfileDto>.Failure(_localizer, "UserNotFound", HttpStatusCode.NotFound);

        int childCount = await childRepo.CountAsync(c => c.ParentUserId == parentUserId && c.IsActive);

        var profileDto = BuildProfileDto(parentUser, user, childCount);
        return Result<ParentUserProfileDto>.Success(profileDto, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<ParentUserProfileDto>> UpdateParentUserProfileAsync(
        long parentUserId, UpdateParentUserProfileDto dto)
    {
        var parentRepo = _unitOfWork.GetRepository<ParentUser, long>();
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var childRepo = _unitOfWork.GetRepository<ParentChild, long>();

        var parentUser = await parentRepo.FindAsync(p => p.Id == parentUserId && p.DeletedAt == null);
        if (parentUser is null)
            return Result<ParentUserProfileDto>.Failure(_localizer, "ParentUserNotFound", HttpStatusCode.NotFound);

        if (dto.LanguagePreference is not null &&
            dto.LanguagePreference != "en" && dto.LanguagePreference != "ar")
        {
            return Result<ParentUserProfileDto>.Failure(_localizer, "InvalidLanguagePreference", HttpStatusCode.BadRequest);
        }

        if (dto.LanguagePreference is not null)
            parentUser.LanguagePreference = dto.LanguagePreference;

        await parentRepo.UpdateAsync(parentUser);
        await _unitOfWork.SaveChangesAsync();

        var user = await userRepo.FindAsync(u => u.Id == parentUser.UserId);
        int childCount = await childRepo.CountAsync(c => c.ParentUserId == parentUserId && c.IsActive);
        var profileDto = BuildProfileDto(parentUser, user!, childCount);

        return Result<ParentUserProfileDto>.Success(profileDto, _localizer, "ParentUserProfileUpdated", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<ParentDashboardDto>> GetDashboardAsync(long parentUserId)
    {
        var parentRepo = _unitOfWork.GetRepository<ParentUser, long>();
        var childRepo = _unitOfWork.GetRepository<ParentChild, long>();

        var parentUser = await parentRepo.FindAsync(p => p.Id == parentUserId && p.DeletedAt == null);
        if (parentUser is null)
            return Result<ParentDashboardDto>.Failure(_localizer, "ParentUserNotFound", HttpStatusCode.NotFound);

        var children = await childRepo.GetAsync(c => c.ParentUserId == parentUserId && c.IsActive);

        var childDtos = new List<ParentChildDto>();
        foreach (var child in children)
        {
            var childDto = await BuildChildDtoAsync(child);
            childDtos.Add(childDto);
        }

        var dashboard = new ParentDashboardDto { Children = childDtos };
        return Result<ParentDashboardDto>.Success(dashboard, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<ParentChildDto>> AddChildByAccountCodeAsync(
        long parentUserId, AddChildByAccountCodeDto dto)
    {
        var parentRepo = _unitOfWork.GetRepository<ParentUser, long>();
        var studentUserRepo = _unitOfWork.GetRepository<StudentUser, long>();
        var childRepo = _unitOfWork.GetRepository<ParentChild, long>();
        var userRepo = _unitOfWork.GetRepository<User, long>();

        // Validate parent exists
        var parentUser = await parentRepo.FindAsync(p => p.Id == parentUserId && p.DeletedAt == null);
        if (parentUser is null)
            return Result<ParentChildDto>.Failure(_localizer, "ParentUserNotFound", HttpStatusCode.NotFound);

        // Validate student account code
        if (string.IsNullOrWhiteSpace(dto.StudentAccountCode))
            return Result<ParentChildDto>.Failure(_localizer, "StudentAccountCodeRequired", HttpStatusCode.BadRequest);

        string normalizedCode = dto.StudentAccountCode.Trim().ToUpperInvariant();

        var studentUser = await studentUserRepo.FindAsync(s =>
            s.StudentAccountCode == normalizedCode && s.DeletedAt == null);

        if (studentUser is null)
            return Result<ParentChildDto>.Failure(_localizer, "StudentUserNotFound", HttpStatusCode.NotFound);

        // Check if this child is already linked to this parent
        bool alreadyLinked = await childRepo.AnyAsync(c =>
            c.ParentUserId == parentUserId &&
            c.StudentUserId == studentUser.Id &&
            c.IsActive);

        if (alreadyLinked)
            return Result<ParentChildDto>.Failure(_localizer, "ChildAlreadyLinked", HttpStatusCode.Conflict);

        // Get the child's name from their User record
        var childUser = await userRepo.FindAsync(u => u.Id == studentUser.UserId);
        string childName = childUser?.FullName ?? "Unknown";

        var child = new ParentChild
        {
            ParentUserId = parentUserId,
            LinkMethod = ChildLinkMethod.StudentAccount,
            StudentUserId = studentUser.Id,
            ChildName = childName,
            IsActive = true,
            CreateAt = DateTime.UtcNow
        };

        await childRepo.AddAsync(child);
        await _unitOfWork.SaveChangesAsync();

        var childDto = await BuildChildDtoAsync(child);
        return Result<ParentChildDto>.Success(childDto, _localizer, "ChildLinkedSuccess", HttpStatusCode.Created);
    }

    /// <inheritdoc />
    public async Task<Result<ParentChildDto>> AddChildManualAsync(
        long parentUserId, AddChildManualDto dto)
    {
        var parentRepo = _unitOfWork.GetRepository<ParentUser, long>();
        var childRepo = _unitOfWork.GetRepository<ParentChild, long>();

        var parentUser = await parentRepo.FindAsync(p => p.Id == parentUserId && p.DeletedAt == null);
        if (parentUser is null)
            return Result<ParentChildDto>.Failure(_localizer, "ParentUserNotFound", HttpStatusCode.NotFound);

        if (string.IsNullOrWhiteSpace(dto.ChildName))
            return Result<ParentChildDto>.Failure(_localizer, "ChildNameRequired", HttpStatusCode.BadRequest);

        var child = new ParentChild
        {
            ParentUserId = parentUserId,
            LinkMethod = ChildLinkMethod.ManualProfile,
            StudentUserId = null,
            ChildName = dto.ChildName.Trim(),
            IsActive = true,
            CreateAt = DateTime.UtcNow
        };

        await childRepo.AddAsync(child);
        await _unitOfWork.SaveChangesAsync();

        var childDto = await BuildChildDtoAsync(child);
        return Result<ParentChildDto>.Success(childDto, _localizer, "ChildCreatedSuccess", HttpStatusCode.Created);
    }

    /// <inheritdoc />
    public async Task<Result<ParentChildTeacherDto>> LinkTeacherToChildAsync(
        long parentUserId, long childId, LinkTeacherToChildDto dto)
    {
        var childRepo = _unitOfWork.GetRepository<ParentChild, long>();
        var teacherRepo = _unitOfWork.GetRepository<Teacher, long>();
        var teacherStudentRepo = _unitOfWork.GetRepository<TeacherStudent, long>();
        var linkRepo = _unitOfWork.GetRepository<ParentChildTeacherLink, long>();
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var teacherSubjectRepo = _unitOfWork.GetRepository<TeacherSubject, long>();
        var subjectRepo = _unitOfWork.GetRepository<Subject, long>();
        var configRepo = _unitOfWork.GetRepository<TeacherConfiguration, long>();

        // ── 1. Validate child exists and belongs to this parent ──
        var child = await childRepo.FindAsync(c =>
            c.Id == childId && c.ParentUserId == parentUserId && c.IsActive);

        if (child is null)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "ChildNotFound", HttpStatusCode.NotFound);

        // Method B only — Method A children get teachers from their StudentUser account
        if (child.LinkMethod == ChildLinkMethod.StudentAccount)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "CannotLinkTeacherToMethodAChild", HttpStatusCode.BadRequest);

        // ── 2. Validate TeacherCode ──
        if (string.IsNullOrWhiteSpace(dto.TeacherCode) || dto.TeacherCode.Length != 8)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "InvalidTeacherCode", HttpStatusCode.BadRequest);

        var teacher = await teacherRepo.FindAsync(t =>
            t.TeacherCode == dto.TeacherCode &&
            t.AccountStatus == AccountStatus.Active &&
            t.DeletedAt == null);

        if (teacher is null)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // ── 3. Check duplicate link ──
        bool alreadyLinked = await linkRepo.AnyAsync(l =>
            l.ParentChildId == childId &&
            l.TeacherId == teacher.Id &&
            l.LinkStatus == LinkStatus.Active);

        if (alreadyLinked)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "TeacherAlreadyLinked", HttpStatusCode.Conflict);

        // ── 4. Validate StudentCode + HashedToken ──
        if (string.IsNullOrWhiteSpace(dto.StudentCode))
            return Result<ParentChildTeacherDto>.Failure(_localizer, "StudentCodeRequired", HttpStatusCode.BadRequest);

        if (string.IsNullOrWhiteSpace(dto.HashedToken))
            return Result<ParentChildTeacherDto>.Failure(_localizer, "HashedTokenRequired", HttpStatusCode.BadRequest);

        string normalizedStudentCode = dto.StudentCode.Trim().ToUpperInvariant();

        var teacherStudent = await teacherStudentRepo.FindAsync(ts =>
            ts.TeacherId == teacher.Id &&
            ts.StudentCode == normalizedStudentCode &&
            ts.HashedToken == dto.HashedToken.Trim() &&
            !ts.IsDeleted);

        if (teacherStudent is null)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "InvalidLinkCredentials", HttpStatusCode.BadRequest);

        // ── Transaction-safe ──
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            var link = new ParentChildTeacherLink
            {
                ParentChildId = childId,
                TeacherId = teacher.Id,
                TeacherStudentId = teacherStudent.Id,
                LinkStatus = LinkStatus.Active,
                LinkedAt = DateTime.UtcNow,
                CreateAt = DateTime.UtcNow
            };

            await linkRepo.AddAsync(link);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var teacherDto = await BuildTeacherDtoAsync(
                link.Id, link.LinkedAt, link.TeacherStudentId.HasValue,
                teacher, userRepo, teacherSubjectRepo, subjectRepo, configRepo);

            return Result<ParentChildTeacherDto>.Success(teacherDto, _localizer, "TeacherLinkSuccess", HttpStatusCode.Created);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> UnlinkTeacherFromChildAsync(
        long parentUserId, long childId, long teacherId)
    {
        var childRepo = _unitOfWork.GetRepository<ParentChild, long>();
        var linkRepo = _unitOfWork.GetRepository<ParentChildTeacherLink, long>();

        // Validate child belongs to this parent
        var child = await childRepo.FindAsync(c =>
            c.Id == childId && c.ParentUserId == parentUserId && c.IsActive);

        if (child is null)
            return Result<bool>.Failure(_localizer, "ChildNotFound", HttpStatusCode.NotFound);

        if (child.LinkMethod == ChildLinkMethod.StudentAccount)
            return Result<bool>.Failure(_localizer, "CannotUnlinkTeacherFromMethodAChild", HttpStatusCode.BadRequest);

        var link = await linkRepo.FindAsync(l =>
            l.ParentChildId == childId &&
            l.TeacherId == teacherId &&
            l.LinkStatus == LinkStatus.Active);

        if (link is null)
            return Result<bool>.Failure(_localizer, "LinkNotFound", HttpStatusCode.NotFound);

        link.LinkStatus = LinkStatus.Unlinked;
        link.UnlinkedAt = DateTime.UtcNow;

        await linkRepo.UpdateAsync(link);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, "TeacherUnlinkSuccess", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<ParentChildDto>> GetChildAsync(long parentUserId, long childId)
    {
        var childRepo = _unitOfWork.GetRepository<ParentChild, long>();

        var child = await childRepo.FindAsync(c =>
            c.Id == childId && c.ParentUserId == parentUserId && c.IsActive);

        if (child is null)
            return Result<ParentChildDto>.Failure(_localizer, "ChildNotFound", HttpStatusCode.NotFound);

        var childDto = await BuildChildDtoAsync(child);
        return Result<ParentChildDto>.Success(childDto, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> RemoveChildAsync(long parentUserId, long childId)
    {
        var childRepo = _unitOfWork.GetRepository<ParentChild, long>();

        var child = await childRepo.FindAsync(c =>
            c.Id == childId && c.ParentUserId == parentUserId && c.IsActive);

        if (child is null)
            return Result<bool>.Failure(_localizer, "ChildNotFound", HttpStatusCode.NotFound);

        child.IsActive = false;

        await childRepo.UpdateAsync(child);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, "ChildRemovedSuccess", HttpStatusCode.OK);
    }

    // ══════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Builds a ParentUserProfileDto from entity and user data.
    /// </summary>
    private static ParentUserProfileDto BuildProfileDto(ParentUser parentUser, User user, int childCount)
    {
        return new ParentUserProfileDto
        {
            Id = parentUser.Id,
            UserId = parentUser.UserId,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            LanguagePreference = parentUser.LanguagePreference,
            AccountStatus = parentUser.AccountStatus.ToString(),
            CreatedAt = parentUser.CreateAt,
            ChildCount = childCount
        };
    }

    /// <summary>
    /// Builds a ParentChildDto including all linked teachers.
    /// Handles both Method A (from StudentTeacherLink) and Method B (from ParentChildTeacherLink).
    /// </summary>
    private async Task<ParentChildDto> BuildChildDtoAsync(ParentChild child)
    {
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var teacherRepo = _unitOfWork.GetRepository<Teacher, long>();
        var teacherSubjectRepo = _unitOfWork.GetRepository<TeacherSubject, long>();
        var subjectRepo = _unitOfWork.GetRepository<Subject, long>();
        var configRepo = _unitOfWork.GetRepository<TeacherConfiguration, long>();

        var teacherDtos = new List<ParentChildTeacherDto>();

        if (child.LinkMethod == ChildLinkMethod.StudentAccount && child.StudentUserId.HasValue)
        {
            // Method A: read teachers from StudentTeacherLink (read-only for parent)
            var studentLinkRepo = _unitOfWork.GetRepository<StudentTeacherLink, long>();

            var studentLinks = await studentLinkRepo.GetAsync(l =>
                l.StudentUserId == child.StudentUserId.Value &&
                l.LinkStatus == LinkStatus.Active);

            foreach (var link in studentLinks)
            {
                var teacher = await teacherRepo.FindAsync(t => t.Id == link.TeacherId && t.DeletedAt == null);
                if (teacher is null) continue;

                var teacherDto = await BuildTeacherDtoAsync(
                    link.Id, link.LinkedAt, link.TeacherStudentId.HasValue,
                    teacher, userRepo, teacherSubjectRepo, subjectRepo, configRepo);
                teacherDtos.Add(teacherDto);
            }
        }
        else if (child.LinkMethod == ChildLinkMethod.ManualProfile)
        {
            // Method B: read teachers from ParentChildTeacherLink
            var parentLinkRepo = _unitOfWork.GetRepository<ParentChildTeacherLink, long>();

            var parentLinks = await parentLinkRepo.GetAsync(l =>
                l.ParentChildId == child.Id &&
                l.LinkStatus == LinkStatus.Active);

            foreach (var link in parentLinks)
            {
                var teacher = await teacherRepo.FindAsync(t => t.Id == link.TeacherId && t.DeletedAt == null);
                if (teacher is null) continue;

                var teacherDto = await BuildTeacherDtoAsync(
                    link.Id, link.LinkedAt, link.TeacherStudentId.HasValue,
                    teacher, userRepo, teacherSubjectRepo, subjectRepo, configRepo);
                teacherDtos.Add(teacherDto);
            }
        }

        // Resolve StudentAccountCode for Method A children
        string? studentAccountCode = null;
        if (child.LinkMethod == ChildLinkMethod.StudentAccount && child.StudentUserId.HasValue)
        {
            var studentUserRepo = _unitOfWork.GetRepository<StudentUser, long>();
            var studentUser = await studentUserRepo.FindAsync(s => s.Id == child.StudentUserId.Value);
            studentAccountCode = studentUser?.StudentAccountCode;
        }

        return new ParentChildDto
        {
            ChildId = child.Id,
            ChildName = child.ChildName,
            LinkMethod = child.LinkMethod.ToString(),
            StudentAccountCode = studentAccountCode,
            IsActive = child.IsActive,
            Teachers = teacherDtos
        };
    }

    /// <summary>
    /// Builds a ParentChildTeacherDto using PARENT visibility settings (AAM-FR-04.9).
    /// Shared by both Method A and Method B teacher rendering.
    /// </summary>
    private static async Task<ParentChildTeacherDto> BuildTeacherDtoAsync(
        long linkId, DateTime linkedAt, bool isEnrollmentActive,
        Teacher teacher,
        IGenericRepo<User, long> userRepo,
        IGenericRepo<TeacherSubject, long> teacherSubjectRepo,
        IGenericRepo<Subject, long> subjectRepo,
        IGenericRepo<TeacherConfiguration, long> configRepo)
    {
        var teacherUser = await userRepo.FindAsync(u => u.Id == teacher.UserId);

        string subjectName = teacher.CustomSubject ?? string.Empty;
        var teacherSubjects = await teacherSubjectRepo.GetAsync(ts => ts.TeacherId == teacher.Id);
        if (teacherSubjects.Any())
        {
            var firstSubject = await subjectRepo.FindAsync(s => s.Id == teacherSubjects.First().SubjectId);
            if (firstSubject is not null)
                subjectName = firstSubject.NameEn;
        }

        // PARENT visibility settings (AAM-FR-04.9) — distinct from student settings
        var config = await configRepo.FindAsync(c => c.TeacherId == teacher.Id);

        return new ParentChildTeacherDto
        {
            LinkId = linkId,
            TeacherCode = teacher.TeacherCode,
            TeacherFullName = teacherUser?.FullName ?? string.Empty,
            SubjectName = subjectName,
            LinkedAt = linkedAt,
            IsEnrollmentActive = isEnrollmentActive,
            VisibilityAttendance = config?.ParentVisibilityAttendance ?? true,
            VisibilityPayment = config?.ParentVisibilityPayment ?? true,
            VisibilityHomework = config?.ParentVisibilityHomework ?? true,
            VisibilityExamDefault = config?.ParentVisibilityExamDefault ?? false
        };
    }
}