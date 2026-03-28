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
/// All database access goes through IUnitOfWork.Users (IUserRepo) — no direct
/// GetRepository calls with raw expression predicates.
/// 
/// ARCHITECTURAL NOTE:
/// All query logic is encapsulated in IUserRepo named methods.
/// If a query needs to change, you edit the repo method — not this service.
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
        var user = await _unitOfWork.Users.GetByIdAndTypeAsync(dto.UserId, UserType.Parent);
        if (user is null)
            return Result<ParentUserProfileDto>.Failure(_localizer, "UserNotFound", HttpStatusCode.NotFound);

        bool alreadyExists = await _unitOfWork.Users.ParentUserExistsByUserIdAsync(dto.UserId);
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

            await _unitOfWork.Users.AddParentUserAsync(parentUser);
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
        var parentUser = await _unitOfWork.Users.GetActiveParentUserByIdAsync(parentUserId);
        if (parentUser is null)
            return Result<ParentUserProfileDto>.Failure(_localizer, "ParentUserNotFound", HttpStatusCode.NotFound);

        var user = await _unitOfWork.Users.GetUserByIdAsync(parentUser.UserId);
        if (user is null)
            return Result<ParentUserProfileDto>.Failure(_localizer, "UserNotFound", HttpStatusCode.NotFound);

        int childCount = await _unitOfWork.Users.CountActiveChildrenAsync(parentUserId);

        var profileDto = BuildProfileDto(parentUser, user, childCount);
        return Result<ParentUserProfileDto>.Success(profileDto, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<ParentUserProfileDto>> UpdateParentUserProfileAsync(
        long parentUserId, UpdateParentUserProfileDto dto)
    {
        var parentUser = await _unitOfWork.Users.GetActiveParentUserByIdAsync(parentUserId);
        if (parentUser is null)
            return Result<ParentUserProfileDto>.Failure(_localizer, "ParentUserNotFound", HttpStatusCode.NotFound);

        if (dto.LanguagePreference is not null &&
            dto.LanguagePreference != "en" && dto.LanguagePreference != "ar")
        {
            return Result<ParentUserProfileDto>.Failure(_localizer, "InvalidLanguagePreference", HttpStatusCode.BadRequest);
        }

        if (dto.LanguagePreference is not null)
            parentUser.LanguagePreference = dto.LanguagePreference;

        await _unitOfWork.Users.UpdateParentUserAsync(parentUser);
        await _unitOfWork.SaveChangesAsync();

        var user = await _unitOfWork.Users.GetUserByIdAsync(parentUser.UserId);
        int childCount = await _unitOfWork.Users.CountActiveChildrenAsync(parentUserId);
        var profileDto = BuildProfileDto(parentUser, user!, childCount);

        return Result<ParentUserProfileDto>.Success(profileDto, _localizer, "ParentUserProfileUpdated", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<ParentDashboardDto>> GetDashboardAsync(long parentUserId)
    {
        var parentUser = await _unitOfWork.Users.GetActiveParentUserByIdAsync(parentUserId);
        if (parentUser is null)
            return Result<ParentDashboardDto>.Failure(_localizer, "ParentUserNotFound", HttpStatusCode.NotFound);

        var children = await _unitOfWork.Users.GetActiveChildrenAsync(parentUserId);

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
        // Validate parent exists
        var parentUser = await _unitOfWork.Users.GetActiveParentUserByIdAsync(parentUserId);
        if (parentUser is null)
            return Result<ParentChildDto>.Failure(_localizer, "ParentUserNotFound", HttpStatusCode.NotFound);

        // Validate student account code
        if (string.IsNullOrWhiteSpace(dto.StudentAccountCode))
            return Result<ParentChildDto>.Failure(_localizer, "StudentAccountCodeRequired", HttpStatusCode.BadRequest);

        var studentUser = await _unitOfWork.Users.GetStudentUserByAccountCodeAsync(dto.StudentAccountCode);

        if (studentUser is null)
            return Result<ParentChildDto>.Failure(_localizer, "StudentUserNotFound", HttpStatusCode.NotFound);

        // Check if this child is already linked to this parent
        bool alreadyLinked = await _unitOfWork.Users.ChildAlreadyLinkedAsync(parentUserId, studentUser.Id);

        if (alreadyLinked)
            return Result<ParentChildDto>.Failure(_localizer, "ChildAlreadyLinked", HttpStatusCode.Conflict);

        // Get the child's name from their User record
        var childUser = await _unitOfWork.Users.GetUserByIdAsync(studentUser.UserId);
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

        await _unitOfWork.Users.AddParentChildAsync(child);
        await _unitOfWork.SaveChangesAsync();

        var childDto = await BuildChildDtoAsync(child);
        return Result<ParentChildDto>.Success(childDto, _localizer, "ChildLinkedSuccess", HttpStatusCode.Created);
    }

    /// <inheritdoc />
    public async Task<Result<ParentChildDto>> AddChildManualAsync(
        long parentUserId, AddChildManualDto dto)
    {
        var parentUser = await _unitOfWork.Users.GetActiveParentUserByIdAsync(parentUserId);
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

        await _unitOfWork.Users.AddParentChildAsync(child);
        await _unitOfWork.SaveChangesAsync();

        var childDto = await BuildChildDtoAsync(child);
        return Result<ParentChildDto>.Success(childDto, _localizer, "ChildCreatedSuccess", HttpStatusCode.Created);
    }

    /// <inheritdoc />
    public async Task<Result<ParentChildTeacherDto>> LinkTeacherToChildAsync(
        long parentUserId, long childId, LinkTeacherToChildDto dto)
    {
        // ── 1. Validate child exists and belongs to this parent ──
        var child = await _unitOfWork.Users.GetActiveChildAsync(parentUserId, childId);

        if (child is null)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "ChildNotFound", HttpStatusCode.NotFound);

        // Method B only — Method A children get teachers from their StudentUser account
        if (child.LinkMethod == ChildLinkMethod.StudentAccount)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "CannotLinkTeacherToMethodAChild", HttpStatusCode.BadRequest);

        // ── 2. Validate TeacherCode ──
        if (string.IsNullOrWhiteSpace(dto.TeacherCode) || dto.TeacherCode.Length != 8)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "InvalidTeacherCode", HttpStatusCode.BadRequest);

        var teacher = await _unitOfWork.Users.GetActiveTeacherByCodeAsync(dto.TeacherCode);

        if (teacher is null)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // ── 3. Check duplicate link ──
        bool alreadyLinked = await _unitOfWork.Users.ParentChildTeacherLinkExistsAsync(childId, teacher.Id);

        if (alreadyLinked)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "TeacherAlreadyLinked", HttpStatusCode.Conflict);

        // ── 4. Validate StudentCode + HashedToken ──
        if (string.IsNullOrWhiteSpace(dto.StudentCode))
            return Result<ParentChildTeacherDto>.Failure(_localizer, "StudentCodeRequired", HttpStatusCode.BadRequest);

        if (string.IsNullOrWhiteSpace(dto.HashedToken))
            return Result<ParentChildTeacherDto>.Failure(_localizer, "HashedTokenRequired", HttpStatusCode.BadRequest);

        string normalizedStudentCode = dto.StudentCode.Trim().ToUpperInvariant();

        var teacherStudent = await _unitOfWork.Users.GetTeacherStudentByLinkingCredentialsAsync(
            teacher.Id, normalizedStudentCode, dto.HashedToken.Trim());

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

            await _unitOfWork.Users.AddParentChildTeacherLinkAsync(link);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var teacherDto = await BuildTeacherDtoAsync(
                link.Id, link.LinkedAt, link.TeacherStudentId.HasValue, teacher);

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
        // Validate child belongs to this parent
        var child = await _unitOfWork.Users.GetActiveChildAsync(parentUserId, childId);

        if (child is null)
            return Result<bool>.Failure(_localizer, "ChildNotFound", HttpStatusCode.NotFound);

        if (child.LinkMethod == ChildLinkMethod.StudentAccount)
            return Result<bool>.Failure(_localizer, "CannotUnlinkTeacherFromMethodAChild", HttpStatusCode.BadRequest);

        var link = await _unitOfWork.Users.GetActiveParentChildTeacherLinkAsync(childId, teacherId);

        if (link is null)
            return Result<bool>.Failure(_localizer, "LinkNotFound", HttpStatusCode.NotFound);

        link.LinkStatus = LinkStatus.Unlinked;
        link.UnlinkedAt = DateTime.UtcNow;

        await _unitOfWork.Users.UpdateParentChildTeacherLinkAsync(link);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, "TeacherUnlinkSuccess", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<ParentChildDto>> GetChildAsync(long parentUserId, long childId)
    {
        var child = await _unitOfWork.Users.GetActiveChildAsync(parentUserId, childId);

        if (child is null)
            return Result<ParentChildDto>.Failure(_localizer, "ChildNotFound", HttpStatusCode.NotFound);

        var childDto = await BuildChildDtoAsync(child);
        return Result<ParentChildDto>.Success(childDto, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> RemoveChildAsync(long parentUserId, long childId)
    {
        var child = await _unitOfWork.Users.GetActiveChildAsync(parentUserId, childId);

        if (child is null)
            return Result<bool>.Failure(_localizer, "ChildNotFound", HttpStatusCode.NotFound);

        child.IsActive = false;

        await _unitOfWork.Users.UpdateParentChildAsync(child);
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
    /// All queries go through IUserRepo named methods — no raw expressions.
    /// </summary>
    private async Task<ParentChildDto> BuildChildDtoAsync(ParentChild child)
    {
        var teacherDtos = new List<ParentChildTeacherDto>();

        if (child.LinkMethod == ChildLinkMethod.StudentAccount && child.StudentUserId.HasValue)
        {
            // Method A: read teachers from StudentTeacherLink (read-only for parent)
            var studentLinks = await _unitOfWork.Users.GetActiveStudentTeacherLinksAsync(child.StudentUserId.Value);

            foreach (var link in studentLinks)
            {
                var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(link.TeacherId);
                if (teacher is null) continue;

                var teacherDto = await BuildTeacherDtoAsync(
                    link.Id, link.LinkedAt, link.TeacherStudentId.HasValue, teacher);
                teacherDtos.Add(teacherDto);
            }
        }
        else if (child.LinkMethod == ChildLinkMethod.ManualProfile)
        {
            // Method B: read teachers from ParentChildTeacherLink
            var parentLinks = await _unitOfWork.Users.GetActiveParentChildTeacherLinksAsync(child.Id);

            foreach (var link in parentLinks)
            {
                var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(link.TeacherId);
                if (teacher is null) continue;

                var teacherDto = await BuildTeacherDtoAsync(
                    link.Id, link.LinkedAt, link.TeacherStudentId.HasValue, teacher);
                teacherDtos.Add(teacherDto);
            }
        }

        // Resolve StudentAccountCode for Method A children
        string? studentAccountCode = null;
        if (child.LinkMethod == ChildLinkMethod.StudentAccount && child.StudentUserId.HasValue)
        {
            var studentUser = await _unitOfWork.Users.GetStudentUserByIdAsync(child.StudentUserId.Value);
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
    /// All queries go through IUserRepo named methods — no raw expressions.
    /// </summary>
    private async Task<ParentChildTeacherDto> BuildTeacherDtoAsync(
        long linkId, DateTime linkedAt, bool isEnrollmentActive,
        Teacher teacher)
    {
        var teacherUser = await _unitOfWork.Users.GetUserByIdAsync(teacher.UserId);

        string subjectName = teacher.CustomSubject ?? string.Empty;
        var teacherSubjects = await _unitOfWork.Users.GetTeacherSubjectsByTeacherIdAsync(teacher.Id);
        if (teacherSubjects.Any())
        {
            var firstSubject = await _unitOfWork.Users.GetSubjectByIdAsync(teacherSubjects.First().SubjectId);
            if (firstSubject is not null)
                subjectName = firstSubject.NameEn;
        }

        // PARENT visibility settings (AAM-FR-04.9) — distinct from student settings
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacher.Id);

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