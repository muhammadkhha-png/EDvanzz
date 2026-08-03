using System.Net;
using Edvanz.Application.Dtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

namespace Edvanz.Application.Services;

/// <summary>
/// Central file-registry authorization + lifecycle service. See <see cref="IFileAccessService"/>.
///
/// Authorization order for a gated read (fail-closed): owner → SuperAdmin → category policy. The
/// teacher tenant authorizes by the denormalized <see cref="FileObject.TeacherId"/> column (set
/// server-side from the uploader's JWT — never client-supplied, §4.4). Student AND parent
/// branches (Phase 4, parent parity): the online-exam image checks live exam assignment; the
/// video categories (photo / attachment / video-exam question image) check live membership of
/// the OWNING VIDEO's scope, which also enforces the Published + PublishDate gate. A parent's
/// check runs across every teacherStudentId reachable through their active children under the
/// file's teacher (almost always 0 or 1, but never assumed to be exactly one — nothing stops a
/// parent having two children with the same teacher), delegating to the SAME canonical scope
/// predicates the student path uses, so the Published gate and exam-assignment logic are
/// inherited automatically, never re-derived. The national-ID image has no resource policy
/// (owner + admin only) for either caller type.
/// </summary>
public sealed class FileAccessService : IFileAccessService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContext;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public FileAccessService(
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        ICurrentUserService currentUser,
        IHttpContextAccessor httpContext,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _httpContext = httpContext;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<Result<string>> TryGetReadUrlAsync(Guid publicId)
    {
        long? userId = _currentUser.UserId;
        if (userId is null)
            return Result<string>.Failure(_localizer, FileConstants.Messages.NotFound, HttpStatusCode.Unauthorized);

        var file = await _unitOfWork.FileObjectsRepo.GetByPublicIdAsync(publicId);
        if (file is null)
            return Result<string>.Failure(_localizer, FileConstants.Messages.NotFound, HttpStatusCode.NotFound);

        bool allowed = await IsReadAuthorizedAsync(file, userId.Value);
        if (!allowed)
            return Result<string>.Failure(_localizer, FileConstants.Messages.NotOwned, HttpStatusCode.Forbidden);

        // Force-download name for attachments (PDFs); inline for images.
        string? downloadName = file.Category == FileCategory.VideoAttachment ? file.OriginalName : null;
        string sas = await _fileStorage.GetReadUrlAsync(file.BlobPath, downloadName);
        return Result<string>.Success(sas, _localizer);
    }

    /// <summary>Owner → SuperAdmin → category policy. Fail-closed on any unhandled category.</summary>
    private async Task<bool> IsReadAuthorizedAsync(FileObject file, long callerUserId)
    {
        // 1. Owner — the uploader.
        if (file.OwnerUserId == callerUserId)
            return true;

        // 2. SuperAdmin.
        if (string.Equals(_currentUser.Role, "SuperAdmin", StringComparison.Ordinal))
            return true;

        // 3. Category policy.
        switch (file.Category)
        {
            case FileCategory.VideoPhoto:
            case FileCategory.VideoAttachment:
            case FileCategory.VideoExamQuestionImage:
                if (await IsSameTeacherTenantAsync(file))
                    return true;
                if (await IsScopedStudentOfVideoAsync(file))
                    return true;
                return await IsScopedParentOfVideoAsync(file);

            case FileCategory.OnlineExamQuestionImage:
                if (await IsSameTeacherTenantAsync(file))
                    return true;
                if (await IsAssignedStudentAsync(file))
                    return true;
                return await IsAssignedParentAsync(file);

            case FileCategory.NationalIdImage:
            default:
                // National-ID has no resource policy (owner + admin already handled above);
                // any unhandled category is denied. Same for parents — intentionally no parent
                // branch here; a parent never needs to read the sign-up ID image.
                return false;
        }
    }

    /// <summary>True when the caller resolves to the file's owning teacher tenant.</summary>
    private async Task<bool> IsSameTeacherTenantAsync(FileObject file)
    {
        if (file.TeacherId is null)
            return false;

        long? callerTeacherId = await ResolveCallerTeacherIdAsync();
        return callerTeacherId is not null && callerTeacherId == file.TeacherId;
    }

    /// <summary>True when the caller is a student in the file's exam's live assigned set.</summary>
    private async Task<bool> IsAssignedStudentAsync(FileObject file)
    {
        if (file.TeacherId is null)
            return false;

        long? teacherStudentId = await ResolveBoundTeacherStudentIdAsync(file.TeacherId.Value);
        if (teacherStudentId is null)
            return false;

        return await _unitOfWork.OnlineExamsRepo.IsQuestionImageAssignedToStudentAsync(
            file.Id, file.TeacherId.Value, teacherStudentId.Value);
    }

    /// <summary>
    /// True when the caller is a student the file's OWNING VIDEO is scoped to. Applies to
    /// <see cref="FileCategory.VideoPhoto"/> / <see cref="FileCategory.VideoAttachment"/> /
    /// <see cref="FileCategory.VideoExamQuestionImage"/>. Resolves the owning video (attachments
    /// carry the back-reference; photos and exam images are looked up tenant-scoped), then reuses
    /// the module's canonical scope predicate <c>IsStudentInVideoScopeAsync</c> — which also
    /// enforces the Published + PublishDate visibility gate, so a Draft/scheduled video's files
    /// are never readable by students.
    /// </summary>
    private async Task<bool> IsScopedStudentOfVideoAsync(FileObject file)
    {
        if (file.TeacherId is null)
            return false;

        long? teacherStudentId = await ResolveBoundTeacherStudentIdAsync(file.TeacherId.Value);
        if (teacherStudentId is null)
            return false;

        long? videoAssetId = file.Category == FileCategory.VideoAttachment
            ? file.VideoAssetId
            : await _unitOfWork.VideoAssetsRepo.GetOwningVideoAssetIdForFileAsync(
                  file.Id, file.Category, file.TeacherId.Value);
        if (videoAssetId is null)
            return false;

        return await _unitOfWork.VideoAssetsRepo.IsStudentInVideoScopeAsync(
            teacherStudentId.Value, videoAssetId.Value, file.TeacherId.Value);
    }

    /// <summary>
    /// Resolves the calling JWT to the bound roster record (<c>TeacherStudentId</c>) for the given
    /// teacher tenant: active <c>StudentUser</c> → Active <c>StudentTeacherLink</c> to that teacher
    /// → non-null binding. Null (deny) at any missing step — shared by the exam-assigned and
    /// video-scoped student policies.
    /// </summary>
    private async Task<long?> ResolveBoundTeacherStudentIdAsync(long teacherId)
    {
        long? userId = _currentUser.UserId;
        if (userId is null)
            return null;

        var studentUser = await _unitOfWork.Users.GetActiveStudentUserByUserIdAsync(userId.Value);
        if (studentUser is null)
            return null;

        var link = await _unitOfWork.Users.GetActiveStudentTeacherLinkAsync(studentUser.Id, teacherId);
        if (link is null || link.LinkStatus != LinkStatus.Active || link.TeacherStudentId is null)
            return null;

        return link.TeacherStudentId;
    }

    /// <summary>
    /// True when the caller is a PARENT whose child(ren) the file's OWNING VIDEO is scoped to.
    /// Applies to the same three categories as <see cref="IsScopedStudentOfVideoAsync"/>. Resolves
    /// every teacherStudentId reachable by the calling parent under the file's teacher tenant, then
    /// checks each against the SAME canonical <c>IsStudentInVideoScopeAsync</c> predicate the
    /// student path uses — so the Published + PublishDate gate is inherited, not re-derived.
    /// </summary>
    private async Task<bool> IsScopedParentOfVideoAsync(FileObject file)
    {
        if (file.TeacherId is null)
            return false;

        var teacherStudentIds = await ResolveChildTeacherStudentIdsForParentAsync(file.TeacherId.Value);
        if (teacherStudentIds.Count == 0)
            return false;

        long? videoAssetId = file.Category == FileCategory.VideoAttachment
            ? file.VideoAssetId
            : await _unitOfWork.VideoAssetsRepo.GetOwningVideoAssetIdForFileAsync(
                  file.Id, file.Category, file.TeacherId.Value);
        if (videoAssetId is null)
            return false;

        foreach (var teacherStudentId in teacherStudentIds)
        {
            if (await _unitOfWork.VideoAssetsRepo.IsStudentInVideoScopeAsync(
                    teacherStudentId, videoAssetId.Value, file.TeacherId.Value))
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when the caller is a PARENT with a child in the file's exam's live assigned set.
    /// Mirrors <see cref="IsAssignedStudentAsync"/> — resolves every teacherStudentId reachable by
    /// the calling parent under the file's teacher tenant, then checks each against the SAME
    /// canonical <c>IsQuestionImageAssignedToStudentAsync</c> predicate the student path uses.
    /// </summary>
    private async Task<bool> IsAssignedParentAsync(FileObject file)
    {
        if (file.TeacherId is null)
            return false;

        var teacherStudentIds = await ResolveChildTeacherStudentIdsForParentAsync(file.TeacherId.Value);
        if (teacherStudentIds.Count == 0)
            return false;

        foreach (var teacherStudentId in teacherStudentIds)
        {
            if (await _unitOfWork.OnlineExamsRepo.IsQuestionImageAssignedToStudentAsync(
                    file.Id, file.TeacherId.Value, teacherStudentId))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves every teacherStudentId a PARENT can legitimately reach under the given teacher
    /// tenant — across ALL of the parent's active children, whichever are linked (Method A via
    /// StudentTeacherLink, Method B via ParentChildTeacherLink) to this teacher. Almost always 0
    /// or 1 entries; a list because nothing stops a parent having more than one child tutored by
    /// the same teacher (siblings/twins). Composed entirely from existing IUserRepo methods — the
    /// same ones ParentScopedApiBaseController uses — no new repository query needed. Mirrors
    /// ResolveBoundTeacherStudentIdAsync's Method A/B branch, but for a caller that may resolve to
    /// several ids instead of one.
    /// </summary>
    private async Task<List<long>> ResolveChildTeacherStudentIdsForParentAsync(long teacherId)
    {
        long? userId = _currentUser.UserId;
        if (userId is null)
            return new List<long>();

        var parentUser = await _unitOfWork.Users.GetActiveParentUserByUserIdAsync(userId.Value);
        if (parentUser is null)
            return new List<long>();

        var children = await _unitOfWork.Users.GetActiveChildrenAsync(parentUser.Id);
        if (children.Count == 0)
            return new List<long>();

        var teacherStudentIds = new List<long>();

        foreach (var child in children)
        {
            if (child.LinkMethod == ChildLinkMethod.StudentAccount)
            {
                if (child.StudentUserId is null)
                    continue;

                var studentLink = await _unitOfWork.Users
                    .GetActiveStudentTeacherLinkAsync(child.StudentUserId.Value, teacherId);
                if (studentLink is not null
                    && studentLink.LinkStatus == LinkStatus.Active
                    && studentLink.TeacherStudentId is not null)
                    teacherStudentIds.Add(studentLink.TeacherStudentId.Value);
            }
            else
            {
                var parentLink = await _unitOfWork.Users
                    .GetActiveParentChildTeacherLinkAsync(child.Id, teacherId);
                if (parentLink is not null
                    && parentLink.LinkStatus == LinkStatus.Active
                    && parentLink.TeacherStudentId is not null)
                    teacherStudentIds.Add(parentLink.TeacherStudentId.Value);
            }
        }

        return teacherStudentIds;
    }

    /// <summary>
    /// Resolves the caller's teacher id from the JWT — teacher lookup, or assistant → owning tutor.
    /// Mirrors <c>ModuleSixApiBaseController.ResolveTeacherIdAsync</c>. Null for students/SuperAdmins.
    /// </summary>
    private async Task<long?> ResolveCallerTeacherIdAsync()
    {
        long? userId = _currentUser.UserId;
        if (userId is null)
            return null;

        if (string.Equals(_currentUser.Role, "Assistant", StringComparison.Ordinal))
        {
            var assistant = await _currentUser.GetAssistantDataAsync();
            return assistant?.TeacherAccountId;
        }

        var teacher = await _unitOfWork.Users.GetTeacherByUserIdAsync(userId.Value);
        return teacher?.Id;
    }

    /// <inheritdoc />
    public async Task<Result<FileObject>> ResolveForAttachAsync(
        Guid publicId, FileCategory expectedCategory, long callerUserId, long? callerTeacherId)
    {
        var file = await _unitOfWork.FileObjectsRepo.GetByPublicIdAsync(publicId, tracked: true);
        if (file is null)
            return Result<FileObject>.Failure(_localizer, FileConstants.Messages.NotFound, HttpStatusCode.NotFound);

        bool ownsOrTenant = file.OwnerUserId == callerUserId
            || (callerTeacherId is not null && file.TeacherId == callerTeacherId);
        if (!ownsOrTenant)
            return Result<FileObject>.Failure(_localizer, FileConstants.Messages.NotOwned, HttpStatusCode.Forbidden);

        if (file.Category != expectedCategory)
            return Result<FileObject>.Failure(
                _localizer, FileConstants.Messages.CategoryMismatch, HttpStatusCode.BadRequest);

        // Already attached to a DIFFERENT resource → reject (claim-stealing guard). Re-attaching the
        // same file to the same slot is handled by the caller comparing ids before calling us, so a
        // file reaching here in Attached state is being claimed by a second resource.
        if (file.Status == FileStatus.Attached)
            return Result<FileObject>.Failure(_localizer, FileConstants.Messages.AlreadyInUse, HttpStatusCode.Conflict);

        file.Status = FileStatus.Attached;
        return Result<FileObject>.Success(file, HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task DetachAsync(long? fileObjectId)
    {
        if (fileObjectId is null)
            return;

        var file = await _unitOfWork.FileObjectsRepo.GetByIdAsync(fileObjectId.Value);
        if (file is null)
            return;

        file.VideoAssetId = null;
        file.Status = FileStatus.Detached;
        await _unitOfWork.FileObjectsRepo.UpdateAsync(file);
    }

    /// <inheritdoc />
    public string BuildGatedUrl(Guid publicId)
    {
        var request = _httpContext.HttpContext?.Request;
        if (request is null)
            return $"/api/files/{publicId}";

        return $"{request.Scheme}://{request.Host}{request.PathBase}/api/files/{publicId}";
    }

    /// <inheritdoc />
    public async Task<string?> TryBuildGatedUrlAsync(long? fileObjectId)
    {
        if (fileObjectId is null)
            return null;

        var file = await _unitOfWork.FileObjectsRepo.GetByIdAsync(fileObjectId.Value);
        return file is null ? null : BuildGatedUrl(file.PublicId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<long, string?>> TryBuildGatedUrlsAsync(
        IEnumerable<long> fileInternalIds)
    {
        var ids = fileInternalIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<long, string?>();

        // ONE query for the whole set; BuildGatedUrl is pure string work off the loaded PublicIds,
        // identical to the single method. A missing row is left out of the map (lookup → null),
        // mirroring TryBuildGatedUrlAsync's null-handling exactly.
        var files = await _unitOfWork.FileObjectsRepo.GetByIdsAsync(ids);
        return files.ToDictionary(f => f.Id, f => (string?)BuildGatedUrl(f.PublicId));
    }
}
