using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ParentUser;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
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
/// 
/// FIX DB-1: Dashboard and child DTO builders use batch loading via
/// GetTeacherDashboardDataAsync to eliminate N+1 query patterns.
/// </summary>
public class ParentUserService : IParentUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubscriptionGateService _subscriptionGate;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public ParentUserService(
        IUnitOfWork unitOfWork,
        ISubscriptionGateService subscriptionGate,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _subscriptionGate = subscriptionGate;
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
    /// <summary>
    /// FIX DB-1: Rewritten to collect ALL teacher IDs across ALL children first,
    /// then batch-load in one call. Previously each child × each teacher = 4×M×N round-trips.
    /// Now: 1 query for children + 1 per child for links + 4 batch queries = much fewer.
    /// </summary>
    public async Task<Result<ParentDashboardDto>> GetDashboardAsync(long parentUserId)
    {
        var parentUser = await _unitOfWork.Users.GetActiveParentUserByIdAsync(parentUserId);
        if (parentUser is null)
            return Result<ParentDashboardDto>.Failure(_localizer, "ParentUserNotFound", HttpStatusCode.NotFound);

        var children = await _unitOfWork.Users.GetActiveChildrenAsync(parentUserId);

        // FIX DB-1: Collect ALL teacher IDs across ALL children first for batch loading
        var allTeacherIds = new List<long>();
        var childLinksMap = new Dictionary<long, IReadOnlyList<StudentTeacherLink>>();
        var childParentLinksMap = new Dictionary<long, IReadOnlyList<ParentChildTeacherLink>>();

        foreach (var child in children)
        {
            if (child.LinkMethod == ChildLinkMethod.StudentAccount && child.StudentUserId.HasValue)
            {
                var studentLinks = await _unitOfWork.Users.GetActiveStudentTeacherLinksAsync(child.StudentUserId.Value);
                childLinksMap[child.Id] = studentLinks;
                allTeacherIds.AddRange(studentLinks.Select(l => l.TeacherId));
            }
            else if (child.LinkMethod == ChildLinkMethod.ManualProfile)
            {
                var parentLinks = await _unitOfWork.Users.GetActiveParentChildTeacherLinksAsync(child.Id);
                childParentLinksMap[child.Id] = parentLinks;
                allTeacherIds.AddRange(parentLinks.Select(l => l.TeacherId));
            }
        }

        // FIX DB-1: Single batch load for ALL teachers across ALL children
        var batchData = await _unitOfWork.Users.GetTeacherDashboardDataAsync(allTeacherIds.Distinct().ToList());

        // Build child DTOs using pre-loaded batch data — zero additional DB calls per teacher
        var childDtos = new List<ParentChildDto>();
        foreach (var child in children)
        {
            var childDto = await BuildChildDtoFromBatchAsync(
                child, parentUser.LanguagePreference, batchData, childLinksMap, childParentLinksMap);
            childDtos.Add(childDto);
        }

        var dashboard = new ParentDashboardDto { Children = childDtos };
        return Result<ParentDashboardDto>.Success(dashboard, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    /// <summary>
    /// FIX BUG-5: Now uses ownsTransaction pattern for consistency.
    /// </summary>
    public async Task<Result<ParentChildDto>> AddChildByAccountCodeAsync(
        long parentUserId, AddChildByAccountCodeDto dto)
    {
        var parentUser = await _unitOfWork.Users.GetActiveParentUserByIdAsync(parentUserId);
        if (parentUser is null)
            return Result<ParentChildDto>.Failure(_localizer, "ParentUserNotFound", HttpStatusCode.NotFound);

        if (string.IsNullOrWhiteSpace(dto.StudentAccountCode))
            return Result<ParentChildDto>.Failure(_localizer, "StudentAccountCodeRequired", HttpStatusCode.BadRequest);

        var studentUser = await _unitOfWork.Users.GetStudentUserByAccountCodeAsync(dto.StudentAccountCode);

        if (studentUser is null)
            return Result<ParentChildDto>.Failure(_localizer, "StudentUserNotFound", HttpStatusCode.NotFound);

        bool alreadyLinked = await _unitOfWork.Users.ChildAlreadyLinkedAsync(parentUserId, studentUser.Id);

        if (alreadyLinked)
            return Result<ParentChildDto>.Failure(_localizer, "ChildAlreadyLinked", HttpStatusCode.Conflict);

        var childUser = await _unitOfWork.Users.GetUserByIdAsync(studentUser.UserId);
        string childName = childUser?.FullName ?? "Unknown";

        // FIX BUG-5: Added ownsTransaction pattern for consistency
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
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

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            // Build child DTO with batch data for the linked teachers
            var studentLinks = await _unitOfWork.Users.GetActiveStudentTeacherLinksAsync(studentUser.Id);
            var teacherIds = studentLinks.Select(l => l.TeacherId).Distinct().ToList();
            var batchData = await _unitOfWork.Users.GetTeacherDashboardDataAsync(teacherIds);

            var childDto = BuildChildDtoFromBatchSync(
                child, parentUser.LanguagePreference, batchData,
                studentLinks: studentLinks, parentLinks: null, studentUser);

            return Result<ParentChildDto>.Success(childDto, _localizer, "ChildLinkedSuccess", HttpStatusCode.Created);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// FIX BUG-5: Now uses ownsTransaction pattern for consistency.
    /// </summary>
    public async Task<Result<ParentChildDto>> AddChildManualAsync(
        long parentUserId, AddChildManualDto dto)
    {
        var parentUser = await _unitOfWork.Users.GetActiveParentUserByIdAsync(parentUserId);
        if (parentUser is null)
            return Result<ParentChildDto>.Failure(_localizer, "ParentUserNotFound", HttpStatusCode.NotFound);

        if (string.IsNullOrWhiteSpace(dto.ChildName))
            return Result<ParentChildDto>.Failure(_localizer, "ChildNameRequired", HttpStatusCode.BadRequest);

        // FIX BUG-5: Added ownsTransaction pattern for consistency
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
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

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            // New manual child has no teachers yet — empty batch data
            var childDto = new ParentChildDto
            {
                ChildId = child.Id,
                ChildName = child.ChildName,
                LinkMethod = child.LinkMethod.ToString(),
                StudentAccountCode = null,
                IsActive = child.IsActive,
                Teachers = new List<ParentChildTeacherDto>()
            };

            return Result<ParentChildDto>.Success(childDto, _localizer, "ChildCreatedSuccess", HttpStatusCode.Created);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<ParentChildTeacherDto>> LinkTeacherToChildAsync(
        long parentUserId, long childId, LinkTeacherToChildDto dto)
    {
        var child = await _unitOfWork.Users.GetActiveChildAsync(parentUserId, childId);

        if (child is null)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "ChildNotFound", HttpStatusCode.NotFound);

        if (child.LinkMethod == ChildLinkMethod.StudentAccount)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "CannotLinkTeacherToMethodAChild", HttpStatusCode.BadRequest);

        if (string.IsNullOrWhiteSpace(dto.TeacherCode) || dto.TeacherCode.Length != 8)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "InvalidTeacherCode", HttpStatusCode.BadRequest);

        var teacher = await _unitOfWork.Users.GetActiveTeacherByCodeAsync(dto.TeacherCode);

        if (teacher is null)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // A managerial-subscription teacher does not accept parent links.
        if (await _subscriptionGate.IsManagerialAsync(teacher.Id))
            return Result<ParentChildTeacherDto>.Failure(
                _localizer, SubscriptionConstants.Messages.ManagerialSubscriptionNoStudents, HttpStatusCode.Forbidden);

        if (string.IsNullOrWhiteSpace(dto.StudentCode))
            return Result<ParentChildTeacherDto>.Failure(_localizer, "StudentCodeRequired", HttpStatusCode.BadRequest);

        if (string.IsNullOrWhiteSpace(dto.HashedToken))
            return Result<ParentChildTeacherDto>.Failure(_localizer, "HashedTokenRequired", HttpStatusCode.BadRequest);

        var teacherStudent = await _unitOfWork.Users.GetTeacherStudentByLinkingCredentialsAsync(
            teacher.Id, dto.StudentCode, dto.HashedToken);

        if (teacherStudent is null)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "InvalidLinkCredentials", HttpStatusCode.BadRequest);

        bool linkExists = await _unitOfWork.Users.ParentChildTeacherLinkExistsAsync(child.Id, teacher.Id);
        if (linkExists)
            return Result<ParentChildTeacherDto>.Failure(_localizer, "TeacherAlreadyLinked", HttpStatusCode.Conflict);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            var link = new ParentChildTeacherLink
            {
                ParentChildId = child.Id,
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

            // Resolve parent's language preference for subject name
            var parentUser = await _unitOfWork.Users.GetActiveParentUserByIdAsync(parentUserId);
            var batchData = await _unitOfWork.Users.GetTeacherDashboardDataAsync(new List<long> { teacher.Id });

            var teacherDto = BuildTeacherDtoFromBatch(
                link.Id, link.LinkedAt, link.TeacherStudentId.HasValue,
                teacher.Id, parentUser?.LanguagePreference, batchData);

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

        var parentUser = await _unitOfWork.Users.GetActiveParentUserByIdAsync(parentUserId);

        // Load teacher links and batch data for this single child
        var teacherIds = new List<long>();
        IReadOnlyList<StudentTeacherLink>? studentLinks = null;
        IReadOnlyList<ParentChildTeacherLink>? parentLinks = null;

        if (child.LinkMethod == ChildLinkMethod.StudentAccount && child.StudentUserId.HasValue)
        {
            studentLinks = await _unitOfWork.Users.GetActiveStudentTeacherLinksAsync(child.StudentUserId.Value);
            teacherIds.AddRange(studentLinks.Select(l => l.TeacherId));
        }
        else if (child.LinkMethod == ChildLinkMethod.ManualProfile)
        {
            parentLinks = await _unitOfWork.Users.GetActiveParentChildTeacherLinksAsync(child.Id);
            teacherIds.AddRange(parentLinks.Select(l => l.TeacherId));
        }

        var batchData = await _unitOfWork.Users.GetTeacherDashboardDataAsync(teacherIds.Distinct().ToList());

        StudentUser? studentUser = null;
        if (child.LinkMethod == ChildLinkMethod.StudentAccount && child.StudentUserId.HasValue)
            studentUser = await _unitOfWork.Users.GetStudentUserByIdAsync(child.StudentUserId.Value);

        var childDto = BuildChildDtoFromBatchSync(
            child, parentUser?.LanguagePreference, batchData, studentLinks, parentLinks, studentUser);

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
    /// FIX DB-1: Builds a ParentChildDto from pre-loaded batch data for the dashboard.
    /// Used when link data was already fetched during the dashboard aggregation pass.
    /// Only the StudentUser lookup (for account code) requires an async call.
    /// </summary>
    private async Task<ParentChildDto> BuildChildDtoFromBatchAsync(
        ParentChild child,
        string? languagePreference,
        TeacherDashboardBatchData batchData,
        Dictionary<long, IReadOnlyList<StudentTeacherLink>> childLinksMap,
        Dictionary<long, IReadOnlyList<ParentChildTeacherLink>> childParentLinksMap)
    {
        IReadOnlyList<StudentTeacherLink>? studentLinks = null;
        IReadOnlyList<ParentChildTeacherLink>? parentLinks = null;

        if (child.LinkMethod == ChildLinkMethod.StudentAccount)
            childLinksMap.TryGetValue(child.Id, out studentLinks);
        else if (child.LinkMethod == ChildLinkMethod.ManualProfile)
            childParentLinksMap.TryGetValue(child.Id, out parentLinks);

        StudentUser? studentUser = null;
        if (child.LinkMethod == ChildLinkMethod.StudentAccount && child.StudentUserId.HasValue)
            studentUser = await _unitOfWork.Users.GetStudentUserByIdAsync(child.StudentUserId.Value);

        return BuildChildDtoFromBatchSync(child, languagePreference, batchData, studentLinks, parentLinks, studentUser);
    }

    /// <summary>
    /// FIX DB-1: Pure in-memory child DTO builder — zero database calls.
    /// All teacher data is resolved from the pre-loaded batchData dictionaries.
    /// </summary>
    private static ParentChildDto BuildChildDtoFromBatchSync(
        ParentChild child,
        string? languagePreference,
        TeacherDashboardBatchData batchData,
        IReadOnlyList<StudentTeacherLink>? studentLinks,
        IReadOnlyList<ParentChildTeacherLink>? parentLinks,
        StudentUser? studentUser)
    {
        var teacherDtos = new List<ParentChildTeacherDto>();

        if (child.LinkMethod == ChildLinkMethod.StudentAccount && studentLinks is not null)
        {
            foreach (var link in studentLinks)
            {
                if (!batchData.Teachers.ContainsKey(link.TeacherId)) continue;
                var dto = BuildTeacherDtoFromBatch(
                    link.Id, link.LinkedAt, link.TeacherStudentId.HasValue,
                    link.TeacherId, languagePreference, batchData);
                teacherDtos.Add(dto);
            }
        }
        else if (child.LinkMethod == ChildLinkMethod.ManualProfile && parentLinks is not null)
        {
            foreach (var link in parentLinks)
            {
                if (!batchData.Teachers.ContainsKey(link.TeacherId)) continue;
                var dto = BuildTeacherDtoFromBatch(
                    link.Id, link.LinkedAt, link.TeacherStudentId.HasValue,
                    link.TeacherId, languagePreference, batchData);
                teacherDtos.Add(dto);
            }
        }

        return new ParentChildDto
        {
            ChildId = child.Id,
            ChildName = child.ChildName,
            LinkMethod = child.LinkMethod.ToString(),
            StudentAccountCode = studentUser?.StudentAccountCode,
            IsActive = child.IsActive,
            Teachers = teacherDtos
        };
    }

    /// <summary>
    /// FIX DB-1 + FIX BUG-3: Builds a ParentChildTeacherDto from pre-loaded batch data.
    /// Uses PARENT visibility settings (AAM-FR-04.9).
    /// Subject name respects parent's language preference (AAM-FR-02.2).
    /// Zero database calls — all data resolved from in-memory dictionaries.
    /// </summary>
    private static ParentChildTeacherDto BuildTeacherDtoFromBatch(
        long linkId, DateTime linkedAt, bool isEnrollmentActive,
        long teacherId, string? languagePreference,
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
                // FIX BUG-3: Respect user's language preference (AAM-FR-02.2)
                subjectName = languagePreference == "ar" ? subject.NameAr : subject.NameEn;
            }
        }

        // PARENT visibility settings (AAM-FR-04.9)
        batchData.Configurations.TryGetValue(teacherId, out var config);

        return new ParentChildTeacherDto
        {
            LinkId = linkId,
            TeacherCode = teacher?.TeacherCode ?? string.Empty,
            TeacherFullName = teacherFullName,
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