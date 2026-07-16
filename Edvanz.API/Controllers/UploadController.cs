using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Generic file upload — images + PDF → permanent public blob URLs. Lets the
/// frontend upload files once and embed the returned URL in later requests
/// (exam images, video attachments, etc.) instead of posting bytes inline.
///
/// Route: <c>api/upload</c> (inherited <c>[Route("api/[controller]")]</c>).
/// Any authenticated user may upload; <c>replace</c> and <c>delete</c> are
/// ownership-guarded in the service — a caller may only act on files under their
/// own <c>{userId}/</c> prefix, with SuperAdmin able to manage any. Identity is
/// always the JWT caller, never a body/route id (§3.3 / BUG-12).
/// </summary>
[Authorize]
public sealed class UploadController : ApiBaseController
{
    private readonly IFileUploadService _uploads;
    private readonly ICurrentUserService _currentUser;

    public UploadController(IFileUploadService uploads, ICurrentUserService currentUser)
    {
        _uploads = uploads;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Uploads one or more files (multipart field <c>files</c>). Returns a
    /// <c>data</c> array of { url, originalName, size, mimeType }.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52_428_800)] // 50 MB batch — raises the ~28.6 MB Kestrel default for THIS action
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upload([FromForm] List<IFormFile> files)
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();

        return ToResponse(await _uploads.UploadFilesAsync(userId.Value, files));
    }

    /// <summary>
    /// Replaces the file behind <paramref name="url"/> with a new <paramref name="file"/>.
    /// Uploads the new file (new URL) and deletes the old blob; returns the new
    /// descriptor. Ownership-guarded.
    /// </summary>
    [HttpPut]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52_428_800)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Replace([FromForm] ReplaceFileForm form)
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();

        return ToResponse(await _uploads.ReplaceFileAsync(userId.Value, _currentUser.Role, form.Url, form.File));
    }

    /// <summary>Deletes the file behind <paramref name="url"/>. Idempotent. Ownership-guarded.</summary>
    [HttpDelete]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete([FromQuery] string url)
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();

        return ToResponse(await _uploads.DeleteFileAsync(userId.Value, _currentUser.Role, url));
    }
}

/// <summary>
/// Multipart form model for <see cref="UploadController.Replace"/>. Bundling the
/// scalar <c>url</c> field and the <c>file</c> together in a single [FromForm] type
/// keeps Swashbuckle able to generate the OpenAPI schema — a [FromForm] string mixed
/// with a bare [FromForm] IFormFile parameter throws SwaggerGeneratorException and
/// aborts the whole document. Field names (url, file) bind unchanged on the wire.
/// </summary>
public sealed class ReplaceFileForm
{
    /// <summary>URL of the existing blob to replace.</summary>
    [FromForm]
    public string Url { get; set; } = default!;

    /// <summary>The new file to upload in its place.</summary>
    [FromForm]
    public IFormFile File { get; set; } = default!;
}
