using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Upload;
using Microsoft.AspNetCore.Http;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Generic file-upload service (images + PDF → permanent public blob URLs).
/// Stateless: no DB record is created — the caller (frontend) stores the returned
/// URL wherever it needs it. Upload is open to any authenticated user; replace and
/// delete are ownership-guarded — a caller may only act on files under their own
/// <c>{userId}/</c> blob-path prefix, with SuperAdmin able to manage any.
/// Identity is always the JWT caller, never a body/route id (§3.3 / BUG-12).
/// </summary>
public interface IFileUploadService
{
    /// <summary>
    /// Uploads 1..N files to the public container and returns their permanent
    /// descriptors (HTTP 201). Rejects the whole batch (422) if any file fails
    /// the content-type allow-list / size / count limits.
    /// </summary>
    Task<Result<List<UploadedFileDto>>> UploadFilesAsync(long userId, IReadOnlyList<IFormFile> files);

    /// <summary>
    /// Replaces the file behind <paramref name="url"/> with <paramref name="file"/>:
    /// uploads the new file (yielding a NEW url), then deletes the old blob, and
    /// returns the new descriptor (HTTP 200). 400 if the URL isn't ours, 403 if
    /// the caller doesn't own it, 422 if the new file fails validation.
    /// </summary>
    Task<Result<UploadedFileDto>> ReplaceFileAsync(long userId, string? role, string url, IFormFile file);

    /// <summary>
    /// Deletes the file behind <paramref name="url"/>. Idempotent. 400 if the URL
    /// isn't ours, 403 if the caller doesn't own it.
    /// </summary>
    Task<Result<bool>> DeleteFileAsync(long userId, string? role, string url);
}
