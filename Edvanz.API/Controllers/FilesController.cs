using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// The single gated read endpoint for every uploaded file. <c>GET /api/files/{fileId}</c> resolves
/// the file in the central registry, re-runs the resource-scoped authorization check on THIS fetch
/// (owner → SuperAdmin → category policy), and 302-redirects to a fresh, short-lived private SAS URL.
/// Anonymous access is impossible — the blob container is private and the endpoint requires a JWT.
/// </summary>
[ApiController]
[Route("api/files")]
[Authorize]
public sealed class FilesController : ApiBaseController
{
    private readonly IFileAccessService _fileAccess;

    public FilesController(IFileAccessService fileAccess)
    {
        _fileAccess = fileAccess;
    }

    /// <summary>
    /// Authorizes the caller against the file's resource, then 302-redirects to a fresh SAS URL.
    /// 401 (no JWT), 404 (unknown file), 403 (not authorized).
    /// </summary>
    [HttpGet("{fileId:guid}")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] Guid fileId)
    {
        var result = await _fileAccess.TryGetReadUrlAsync(fileId);
        if (!result.IsSuccess)
            return ToResponse(result);

        return Redirect(result.Data!);
    }
}
