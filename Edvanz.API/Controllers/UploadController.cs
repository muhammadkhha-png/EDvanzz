using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Generic file upload — images + PDF → the private uploads container + a registry row. Returns an
/// opaque <c>fileId</c> (and a stable gated URL) the frontend sends back when creating/updating a
/// resource. The file is served only through <c>GET /api/files/{fileId}</c>, which re-checks access
/// on every fetch.
///
/// Route: <c>api/upload</c>. Any authenticated user may upload; <c>replace</c> and <c>delete</c> are
/// ownership-guarded in the service (a caller may act only on files they uploaded; SuperAdmin any).
/// Identity is always the JWT caller, never a body/route id (§3.3 / BUG-12).
/// </summary>
[Authorize]
public sealed class UploadController : ModuleSixApiBaseController
{
    private readonly IFileUploadService _uploads;
    private readonly ICurrentUserService _currentUser;

    public UploadController(
        IFileUploadService uploads, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
        : base(currentUser, unitOfWork)
    {
        _uploads = uploads;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Uploads one or more files (multipart field <c>files</c>) under the given <c>category</c>.
    /// Returns a <c>data</c> array of { fileId, url, originalName, size, mimeType }.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52_428_800)] // 50 MB batch — raises the ~28.6 MB Kestrel default for THIS action
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upload([FromForm] List<IFormFile> files, [FromForm] FileCategory category)
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();

        // Tenant for the file — teacher/assistant → owning teacher; null for others (file still
        // owner-scoped). ResolveTeacherIdAsync returns null when there is no teacher mapping.
        long? teacherId = await ResolveTeacherIdAsync();

        return ToResponse(await _uploads.UploadFilesAsync(userId.Value, teacherId, category, files));
    }

    /// <summary>
    /// Replaces the file identified by <c>fileId</c> with a new <c>file</c>. Uploads the new file
    /// (new id) and detaches the old one; returns the new descriptor. Ownership-guarded.
    /// </summary>
    [HttpPut]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52_428_800)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Replace([FromForm] ReplaceFileForm form)
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();

        return ToResponse(await _uploads.ReplaceFileAsync(userId.Value, _currentUser.Role, form.FileId, form.File));
    }

    /// <summary>Deletes (detaches) the file identified by <c>fileId</c>. Idempotent. Ownership-guarded.</summary>
    [HttpDelete]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromQuery] Guid fileId)
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();

        return ToResponse(await _uploads.DeleteFileAsync(userId.Value, _currentUser.Role, fileId));
    }
}

/// <summary>
/// Multipart form model for <see cref="UploadController.Replace"/>. Bundling the scalar
/// <c>fileId</c> and the <c>file</c> in a single [FromForm] type keeps Swashbuckle able to generate
/// the OpenAPI schema (a [FromForm] scalar mixed with a bare [FromForm] IFormFile parameter throws).
/// </summary>
public sealed class ReplaceFileForm
{
    /// <summary>Opaque id of the existing registry file to replace.</summary>
    [FromForm]
    public Guid FileId { get; set; }

    /// <summary>The new file to upload in its place.</summary>
    [FromForm]
    public IFormFile File { get; set; } = default!;
}
