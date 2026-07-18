using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Subscription;
using Edvanz.Application.IservicesContract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Edvanz.API.Controllers;

/// <summary>
/// Super-admin per-tutor module activation surface
/// (REQ-ADM-034 through REQ-ADM-038 / REQ-ADM-051 / BR-ADM-010).
///
/// AUTHORIZATION:
///   Every endpoint requires <c>SuperAdmin</c> role via
///   <c>[ModulePermission(roles: ["SuperAdmin"], roleOnly: true)]</c>.
///   The <c>PermissionHandler</c> short-circuits the SuperAdmin role on its
///   fast path so no module/permission checks apply.
///
/// CACHE INVALIDATION:
///   The service layer's <c>IUserAuthInvalidationService.InvalidateTutorAndAssistantsAsync</c>
///   fans out the snapshot invalidation to the tutor AND every assistant under
///   them, so a granted/revoked module takes effect on those users' very next
///   requests (REQ-ADM-035).
///
/// AUDIT:
///   Each grant/revoke produces one audit-trail row capturing the action,
///   tutor, module, and the calling admin's user id (REQ-ADM-055).
/// </summary>
[Route("api/admin/tutor-modules")]
[Authorize]
public class AdminTutorModuleController : ApiBaseController
{
    private readonly ITutorModuleAccessService _moduleService;
    private readonly ICurrentUserService _currentUser;

    public AdminTutorModuleController(
        ITutorModuleAccessService moduleService,
        ICurrentUserService currentUser)
    {
        _moduleService = moduleService;
        _currentUser = currentUser;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: GRANT (REQ-ADM-034)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // POST /api/admin/tutor-modules/grant
    //   { "teacherId": 42, "moduleId": 7 }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("grant")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Grant([FromBody] ModuleGrantRequest request)
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null)
            return Unauthorized();

        var result = await _moduleService.GrantAsync(request, adminUserId.Value);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2: REVOKE (REQ-ADM-034 / BR-ADM-010 / REQ-ADM-050)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // POST /api/admin/tutor-modules/revoke
    //   { "teacherId": 42, "moduleId": 7 }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("revoke")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke([FromBody] ModuleRevokeRequest request)
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null)
            return Unauthorized();

        var result = await _moduleService.RevokeAsync(request, adminUserId.Value);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 3: REPLACE (REQ-ADM-037 / REQ-ADM-051 / REQ-ADM-053)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // PUT /api/admin/tutor-modules/replace
    //   { "teacherId": 42, "moduleIds": [1, 2, 3, 7] }
    //
    // Replaces the tutor's full module set with the supplied list. Use cases:
    //   - "Toggle entire module group" UX (REQ-ADM-037).
    //   - "Apply feature configuration template" (REQ-ADM-051).
    //   - Preview-then-commit confirmation dialog (REQ-ADM-053) — the client
    //     diffs current vs requested and shows the user the impact before
    //     calling this endpoint.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("replace")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Replace([FromBody] TutorModulesReplaceRequest request)
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null)
            return Unauthorized();

        var result = await _moduleService.BulkReplaceAsync(request, adminUserId.Value);
        return ToResponse(result);
    }
    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 4: CATALOGUE — ALL PLATFORM MODULES
    // ══════════════════════════════════════════════════════════════════════════
    //
    // GET /api/admin/tutor-modules/catalogue  → Result<ModuleInfoDto[]>  (read-only)
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("catalogue")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<System.Collections.Generic.List<Edvanz.Application.Dtos.Subscription.ModuleInfoDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCatalogue()
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null) return AdminNotResolved();

        var result = await _moduleService.GetCatalogueAsync();
        return ToResponse(result);
    }

    protected IActionResult AdminNotResolved() =>
        new ObjectResult(new { success = false, message = "admin not found" })
        {
            StatusCode = (int)HttpStatusCode.NotFound
        };

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5: A TUTOR'S GRANTED MODULES
    // ══════════════════════════════════════════════════════════════════════════
    //
    // GET /api/admin/tutor-modules/{teacherId}  → Result<ModuleInfoDto[]>
    //   404 TeacherNotFound if the teacher does not exist.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<System.Collections.Generic.List<Edvanz.Application.Dtos.Subscription.ModuleInfoDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTutorModules([FromRoute] long teacherId)
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null) return AdminNotResolved();

        var result = await _moduleService.GetTutorModulesAsync(teacherId);
        return ToResponse(result);
    }
}