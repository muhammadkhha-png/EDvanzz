using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Upload;
using Edvanz.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Generic file-upload service. Proxies bytes to the private uploads container and creates a
/// <c>FileObject</c> registry row (Status = Pending) per file, returning its opaque id + gated URL.
/// The frontend sends the id back when creating/updating a resource, which attaches the file.
/// Upload is open to any authenticated user; replace and delete are ownership-guarded on
/// <c>FileObject.OwnerUserId</c> (SuperAdmin may manage any). Identity is always the JWT caller,
/// never a body/route id (§3.3 / BUG-12).
/// </summary>
public interface IFileUploadService
{
    /// <summary>
    /// Uploads 1..N files under the given <paramref name="category"/> and returns their descriptors
    /// (HTTP 201). Rejects the whole batch (422) if any file fails the content-type / size / count
    /// limits, or (400) if the category is not uploadable through this endpoint.
    /// </summary>
    Task<Result<List<UploadedFileDto>>> UploadFilesAsync(
        long userId, long? teacherId, FileCategory category, IReadOnlyList<IFormFile> files);

    /// <summary>
    /// Replaces the file identified by <paramref name="fileId"/> with <paramref name="file"/>:
    /// uploads a new registry file (same category/tenant), detaches the old one, and returns the new
    /// descriptor (HTTP 200). 404 if unknown, 403 if the caller doesn't own it, 422 on validation.
    /// </summary>
    Task<Result<UploadedFileDto>> ReplaceFileAsync(long userId, string? role, Guid fileId, IFormFile file);

    /// <summary>
    /// Detaches the file identified by <paramref name="fileId"/> (GC reaps the blob). Idempotent.
    /// 404 if unknown, 403 if the caller doesn't own it.
    /// </summary>
    Task<Result<bool>> DeleteFileAsync(long userId, string? role, Guid fileId);
}
