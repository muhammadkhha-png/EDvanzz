using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Upload;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Generic file-upload service. Proxies bytes to the private uploads container and records each in
/// the central registry (<see cref="FileObject"/>, Status = Pending), returning the opaque file id
/// + gated URL. See <see cref="IFileUploadService"/>. Ownership is enforced on
/// <c>FileObject.OwnerUserId</c> (SuperAdmin may manage any); identity is always the JWT caller.
/// </summary>
public sealed class FileUploadService : IFileUploadService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly IFileAccessService _fileAccess;
    private readonly IStringLocalizer<Edvanz.Domain.Resources.Messages> _localizer;

    /// <summary>
    /// Maps an allowed content type to the blob-name extension. The extension is taken from the
    /// validated content type — never the client filename — so nothing user-controlled reaches the
    /// blob path.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ExtensionByContentType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"]      = ".jpg",
            ["image/png"]       = ".png",
            ["image/gif"]       = ".gif",
            ["image/webp"]      = ".webp",
            ["image/bmp"]       = ".bmp",
            ["image/tiff"]      = ".tiff",
            ["image/heic"]      = ".heic",
            ["image/heif"]      = ".heif",
            ["application/pdf"] = ".pdf",
        };

    public FileUploadService(
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        IFileAccessService fileAccess,
        IStringLocalizer<Edvanz.Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _fileAccess = fileAccess;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<Result<List<UploadedFileDto>>> UploadFilesAsync(
        long userId, long? teacherId, FileCategory category, IReadOnlyList<IFormFile> files)
    {
        if (!FileConstants.UploadableCategories.Contains(category))
            return Result<List<UploadedFileDto>>.Failure(
                _localizer, FileConstants.Messages.CategoryNotUploadable, HttpStatusCode.BadRequest);

        if (files is null || files.Count == 0)
            return Result<List<UploadedFileDto>>.Failure(
                _localizer, UploadConstants.Messages.NoFiles, HttpStatusCode.UnprocessableEntity);

        if (files.Count > UploadConstants.MaxFilesPerRequest)
            return Result<List<UploadedFileDto>>.Failure(
                _localizer, UploadConstants.Messages.TooManyFiles, HttpStatusCode.UnprocessableEntity);

        // Validate the WHOLE batch before uploading anything.
        foreach (var file in files)
        {
            string? error = ValidateFile(file);
            if (error is not null)
                return Result<List<UploadedFileDto>>.Failure(
                    _localizer, error, HttpStatusCode.UnprocessableEntity);
        }

        var results = new List<UploadedFileDto>(files.Count);
        foreach (var file in files)
        {
            var registered = await UploadOneAsync(userId, teacherId, category, file);
            results.Add(registered);
        }

        await _unitOfWork.SaveChangesAsync();

        return Result<List<UploadedFileDto>>.Success(
            results, _localizer, "Success", HttpStatusCode.Created);
    }

    /// <inheritdoc />
    public async Task<Result<UploadedFileDto>> ReplaceFileAsync(
        long userId, string? role, Guid fileId, IFormFile file)
    {
        var old = await _unitOfWork.FileObjectsRepo.GetByPublicIdAsync(fileId, tracked: true);
        if (old is null)
            return Result<UploadedFileDto>.Failure(
                _localizer, FileConstants.Messages.NotFound, HttpStatusCode.NotFound);

        if (!OwnsOrAdmin(userId, role, old))
            return Result<UploadedFileDto>.Failure(
                _localizer, FileConstants.Messages.NotOwned, HttpStatusCode.Forbidden);

        string? error = ValidateFile(file);
        if (error is not null)
            return Result<UploadedFileDto>.Failure(
                _localizer, error, HttpStatusCode.UnprocessableEntity);

        // Upload the NEW file first — a replace must never lose the old, live asset if the new
        // upload fails. The old registry row is detached (GC reaps its blob).
        var registered = await UploadOneAsync(userId, old.TeacherId, old.Category, file);
        old.Status = FileStatus.Detached;
        old.VideoAssetId = null;
        await _unitOfWork.FileObjectsRepo.UpdateAsync(old);
        await _unitOfWork.SaveChangesAsync();

        return Result<UploadedFileDto>.Success(registered, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteFileAsync(long userId, string? role, Guid fileId)
    {
        var file = await _unitOfWork.FileObjectsRepo.GetByPublicIdAsync(fileId, tracked: true);
        if (file is null)
            return Result<bool>.Failure(
                _localizer, FileConstants.Messages.NotFound, HttpStatusCode.NotFound);

        if (!OwnsOrAdmin(userId, role, file))
            return Result<bool>.Failure(
                _localizer, FileConstants.Messages.NotOwned, HttpStatusCode.Forbidden);

        // Idempotent — detaching an already-detached file is a no-op; the GC reaps the blob.
        file.Status = FileStatus.Detached;
        file.VideoAssetId = null;
        await _unitOfWork.FileObjectsRepo.UpdateAsync(file);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, "Success", HttpStatusCode.OK);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    /// <summary>Content-type allow-list + size check. Returns a message key, or null when valid.</summary>
    private static string? ValidateFile(IFormFile file)
    {
        if (file is null)
            return UploadConstants.Messages.InvalidType;

        bool typeAllowed = UploadConstants.AllowedContentTypes.Contains(
            file.ContentType, StringComparer.OrdinalIgnoreCase);
        if (!typeAllowed)
            return UploadConstants.Messages.InvalidType;

        if (file.Length > UploadConstants.MaxFileSizeBytes)
            return UploadConstants.Messages.FileTooLarge;

        return null;
    }

    /// <summary>
    /// Uploads one file to the private container and stages its <see cref="FileObject"/> row
    /// (Status = Pending). The caller commits (batched SaveChanges). Returns the descriptor.
    /// </summary>
    private async Task<UploadedFileDto> UploadOneAsync(
        long userId, long? teacherId, FileCategory category, IFormFile file)
    {
        string ext = ExtensionByContentType.TryGetValue(file.ContentType, out var mapped)
            ? mapped
            : string.Empty;

        // Tenant-scoped, non-guessable path. Uses the teacher tenant when known, else the uploader.
        string scope = (teacherId ?? userId).ToString();
        string blobPath = $"files/{scope}/{Guid.NewGuid():N}{ext}";

        await using (var stream = file.OpenReadStream())
        {
            await _fileStorage.UploadAsync(blobPath, stream, file.ContentType);
        }

        var fileObject = new FileObject
        {
            PublicId = Guid.NewGuid(),
            OwnerUserId = userId,
            TeacherId = teacherId,
            Category = category,
            Status = FileStatus.Pending,
            BlobPath = blobPath,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            OriginalName = Path.GetFileName(file.FileName) ?? string.Empty,
            CreateAt = DateTime.UtcNow,
        };

        await _unitOfWork.FileObjectsRepo.AddAsync(fileObject);

        return new UploadedFileDto
        {
            FileId = fileObject.PublicId,
            Url = _fileAccess.BuildGatedUrl(fileObject.PublicId),
            OriginalName = fileObject.OriginalName,
            Size = fileObject.SizeBytes,
            MimeType = fileObject.ContentType,
        };
    }

    /// <summary>True when the caller uploaded the file or is a SuperAdmin.</summary>
    private static bool OwnsOrAdmin(long userId, string? role, FileObject file) =>
        file.OwnerUserId == userId || string.Equals(role, "SuperAdmin", StringComparison.Ordinal);
}
