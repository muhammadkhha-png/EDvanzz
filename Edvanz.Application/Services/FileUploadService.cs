using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Upload;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Generic file-upload service (images + PDF → permanent public blob URLs).
/// Stateless — no DB record; the caller stores the returned URL. Upload is open to
/// any authenticated user; replace and delete are ownership-guarded via the
/// <c>{userId}/</c> blob-path prefix (SuperAdmin may manage any). See §3.3 / BUG-12:
/// identity is always the JWT caller, never a body/route id.
/// </summary>
public sealed class FileUploadService : IFileUploadService
{
    private readonly IFileStorageService _fileStorage;
    private readonly IStringLocalizer<Edvanz.Domain.Resources.Messages> _localizer;

    /// <summary>
    /// Maps an allowed content type to the extension used in the blob name. The
    /// extension is taken from the validated content type — never the client
    /// filename — so nothing user-controlled reaches the blob path.
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
        IFileStorageService fileStorage,
        IStringLocalizer<Edvanz.Domain.Resources.Messages> localizer)
    {
        _fileStorage = fileStorage;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<Result<List<UploadedFileDto>>> UploadFilesAsync(long userId, IReadOnlyList<IFormFile> files)
    {
        if (files is null || files.Count == 0)
            return Result<List<UploadedFileDto>>.Failure(
                _localizer, UploadConstants.Messages.NoFiles, HttpStatusCode.UnprocessableEntity);

        if (files.Count > UploadConstants.MaxFilesPerRequest)
            return Result<List<UploadedFileDto>>.Failure(
                _localizer, UploadConstants.Messages.TooManyFiles, HttpStatusCode.UnprocessableEntity);

        // Validate the WHOLE batch before uploading anything, so a single bad
        // file never leaves a partial set of blobs behind.
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
            string url = await UploadOneAsync(userId, file);
            results.Add(ToDto(url, file));
        }

        return Result<List<UploadedFileDto>>.Success(
            results, _localizer, "Success", HttpStatusCode.Created);
    }

    /// <inheritdoc />
    public async Task<Result<UploadedFileDto>> ReplaceFileAsync(long userId, string? role, string url, IFormFile file)
    {
        string? oldPath = _fileStorage.ResolvePublicBlobPath(url);
        if (oldPath is null)
            return Result<UploadedFileDto>.Failure(
                _localizer, UploadConstants.Messages.InvalidUrl, HttpStatusCode.BadRequest);

        if (!OwnsOrAdmin(userId, role, oldPath))
            return Result<UploadedFileDto>.Failure(
                _localizer, UploadConstants.Messages.NotOwned, HttpStatusCode.Forbidden);

        string? error = ValidateFile(file);
        if (error is not null)
            return Result<UploadedFileDto>.Failure(
                _localizer, error, HttpStatusCode.UnprocessableEntity);

        // Upload the NEW blob first — a replace must never lose the old, live
        // asset if the new upload fails (same ordering as ReplaceThumbnailAsync).
        string newUrl = await UploadOneAsync(userId, file);

        // Best-effort delete of the old blob. If it fails the old blob is
        // orphaned (non-fatal) — the new file is already live and returned.
        try
        {
            await _fileStorage.DeletePublicAsync(oldPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[FileUploadService] ReplaceFileAsync: new blob uploaded but failed to delete " +
                $"old blob '{oldPath}': {ex.Message}");
        }

        return Result<UploadedFileDto>.Success(
            ToDto(newUrl, file), _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteFileAsync(long userId, string? role, string url)
    {
        string? path = _fileStorage.ResolvePublicBlobPath(url);
        if (path is null)
            return Result<bool>.Failure(
                _localizer, UploadConstants.Messages.InvalidUrl, HttpStatusCode.BadRequest);

        if (!OwnsOrAdmin(userId, role, path))
            return Result<bool>.Failure(
                _localizer, UploadConstants.Messages.NotOwned, HttpStatusCode.Forbidden);

        await _fileStorage.DeletePublicAsync(path); // idempotent (no-op if already gone)
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

    private async Task<string> UploadOneAsync(long userId, IFormFile file)
    {
        string ext = ExtensionByContentType.TryGetValue(file.ContentType, out var mapped)
            ? mapped
            : string.Empty;
        string blobPath = $"{userId}/{Guid.NewGuid():N}{ext}";

        await using var stream = file.OpenReadStream();
        return await _fileStorage.UploadPublicAsync(blobPath, stream, file.ContentType);
    }

    private static UploadedFileDto ToDto(string url, IFormFile file) => new()
    {
        Url = url,
        OriginalName = Path.GetFileName(file.FileName),
        Size = file.Length,
        MimeType = file.ContentType,
    };

    /// <summary>
    /// True when the caller uploaded the file (blob path is under their
    /// <c>{userId}/</c> prefix) or is a SuperAdmin. The trailing slash prevents
    /// id-prefix collisions (e.g. user 1 vs user 12).
    /// </summary>
    private static bool OwnsOrAdmin(long userId, string? role, string blobPath) =>
        blobPath.StartsWith($"{userId}/", StringComparison.Ordinal)
        || string.Equals(role, "SuperAdmin", StringComparison.Ordinal);
}
