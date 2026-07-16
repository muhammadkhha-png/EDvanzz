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
/// teacher-scoped categories authorize by the denormalized <see cref="FileObject.TeacherId"/> tenant
/// column (set server-side from the uploader's JWT — never client-supplied, §4.4); the online-exam
/// student branch additionally checks live exam assignment; the national-ID image has no resource
/// policy (owner + admin only).
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
            case FileCategory.VideoThumbnail:
            case FileCategory.VideoAttachment:
            case FileCategory.VideoExamQuestionImage:
                return await IsSameTeacherTenantAsync(file);

            case FileCategory.OnlineExamQuestionImage:
                if (await IsSameTeacherTenantAsync(file))
                    return true;
                return await IsAssignedStudentAsync(file);

            case FileCategory.NationalIdImage:
            default:
                // National-ID has no resource policy (owner + admin already handled above);
                // any unhandled category is denied.
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

        long? userId = _currentUser.UserId;
        if (userId is null)
            return false;

        var studentUser = await _unitOfWork.Users.GetActiveStudentUserByUserIdAsync(userId.Value);
        if (studentUser is null)
            return false;

        var link = await _unitOfWork.Users.GetActiveStudentTeacherLinkAsync(studentUser.Id, file.TeacherId.Value);
        if (link is null || link.LinkStatus != LinkStatus.Active || link.TeacherStudentId is null)
            return false;

        return await _unitOfWork.OnlineExamsRepo.IsQuestionImageAssignedToStudentAsync(
            file.Id, file.TeacherId.Value, link.TeacherStudentId.Value);
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
}
