using Edvanz.API.Attributes;
using Edvanz.Application.Common;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ParentPortal;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Edvanz.API.Controllers;

/// <summary>
/// PUBLIC parent portal — the server-to-server API behind parent.edvanz.io, a PHP page where a
/// parent follows ONE student under ONE teacher, read-only, without installing the app.
///
/// <para><b>AUTH.</b> Class-level <c>[AllowAnonymous]</c> is DELIBERATE and EXPLICIT: there is no
/// JWT here (the end user is an unauthenticated parent), and the global FallbackPolicy would
/// otherwise reject every call. The real gate is <c>[ParentPortalKey]</c> — the platform kill
/// switch plus a constant-time <c>X-Portal-Key</c> shared-secret check that only the PHP portal
/// holds. Reads additionally require <c>X-Portal-Device</c>; the raw value is hashed server-side
/// and never stored. <c>X-Portal-Client-IP</c> is forwarded by the portal and used ONLY for the
/// hashed audit column and as a rate-limit partition fallback.</para>
///
/// <para><b>TRUST FOLLOWS THE PHONE, NOT THE BROWSER.</b> A grant row is per (student, device),
/// but a request is admitted immediately whenever the typed number is already trusted for that
/// student — it matches the roster's parent phone, or it already holds an approved grant. So a
/// parent who clears cookies, switches browser or buys a new handset does NOT land back in the
/// teacher's inbox. The device header still identifies WHICH grant is calling on every read; it
/// is simply no longer what earns access.</para>
///
/// <para><b>THE ROUTE'S <c>{rosterId}</c> IS NEVER TRUSTED.</b> Every read resolves the grant from
/// the DEVICE header first and then requires the supplied <c>{rosterId}</c> to be exactly the one
/// that grant names — otherwise 404. This is CLAUDE.md §3.3 ("never take an identity id from the
/// route or body") as generalized by BUG-12; the segment exists only so the portal's URLs are
/// readable and cacheable per student. Changing it to trust the route would hand every roster
/// record on the platform to anyone holding one valid device id.</para>
/// </summary>
[AllowAnonymous]
[ParentPortalKey]
[EnableRateLimiting(ParentPortalConstants.RateLimitPolicy)]
[Route(ParentPortalConstants.RouteBase)]
public class ParentPortalController : ApiBaseController
{
    private readonly IParentPortalService _portalService;

    public ParentPortalController(IParentPortalService portalService)
    {
        _portalService = portalService;
    }

    /// <summary>
    /// Public teacher card for the "is this the right teacher?" step: display name, subject, and
    /// whether they currently accept portal followers. Reveals nothing beyond what the teacher's
    /// share card already prints — the teacher code is public by design.
    /// </summary>
    /// <param name="teacherCode">The teacher's 8-digit code.</param>
    /// <param name="language">Optional "en"/"ar" override for the subject label; defaults to the Accept-Language culture.</param>
    /// <response code="200">Teacher card (check <c>portalEnabled</c> before showing the code form).</response>
    /// <response code="400">The code is not 8 digits.</response>
    /// <response code="404">No teacher with this code.</response>
    [HttpGet("teachers/{teacherCode}/preview")]
    [ProducesResponseType(typeof(Result<ParentPortalTeacherPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTeacherPreview(
        [FromRoute] string teacherCode, [FromQuery] string? language = null)
        => ToResponse(await _portalService.GetTeacherPreviewAsync(teacherCode, language));

    /// <summary>
    /// Requests follow-up access for one student on this device. Admitted immediately when the
    /// supplied phone is already trusted for that student — it matches the parent phone on the
    /// teacher's roster, OR it already holds an approved grant from another device (so a returning
    /// parent on a new browser/handset skips the queue). Otherwise it waits in the teacher's inbox.
    ///
    /// <para>A re-request within 24h of being REJECTED is silently discarded — it returns the same
    /// pending payload and writes nothing, so a rejected parent cannot keep repopulating the
    /// inbox.</para>
    ///
    /// <para><b>Do not read a "pending" response as "the codes were right".</b> A request for a
    /// student code that does not exist returns the byte-identical pending payload and writes
    /// nothing. Student codes are a sequential counter and teacher codes are public, so a
    /// distinguishable answer there would make this endpoint a roster-enumeration oracle. Student
    /// details are returned only on an <c>active</c> (phone-verified) result. A teacher who has
    /// the portal switched off is a DIFFERENT case and does answer honestly (403
    /// <c>ParentPortalDisabled</c>) — that fact is already public via the preview endpoint.</para>
    /// </summary>
    /// <response code="200">Either <c>state: "active"</c> (auto-approved) or <c>state: "pending"</c>.</response>
    /// <response code="400">Bad teacher-code length, missing student code, or an unparseable phone.</response>
    /// <response code="403">This teacher has not switched parent follow-up on.</response>
    /// <response code="404">No teacher with this code.</response>
    /// <response code="429">Too many requests from this device, or aimed at this teacher, in the last hour.</response>
    [HttpPost("access-requests")]
    [ProducesResponseType(typeof(Result<ParentPortalAccessRequestResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestAccess([FromBody] ParentPortalAccessRequestDto dto)
        => ToResponse(await _portalService.RequestAccessAsync(dto, ClientIp(), UserAgent()));

    /// <summary>
    /// Where this device stands right now, plus the LIVE per-section visibility flags. Always a
    /// 200 with a renderable <c>state</c> — never an error just because the device is not (yet)
    /// approved.
    /// </summary>
    /// <response code="200">One of active / pending / rejected / revoked / disabled / studentRemoved / none.</response>
    [HttpGet("access")]
    [ProducesResponseType(typeof(Result<ParentPortalAccessStateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAccessState()
        => ToResponse(await _portalService.GetAccessStateAsync(DeviceHash() ?? string.Empty));

    /// <summary>
    /// The whole portal home in one call: header, attendance (current month), payments and grades,
    /// each behind the teacher's own parent-visibility flag.
    /// </summary>
    /// <param name="rosterId">Must equal the roster id this device's grant names — see the controller remarks.</param>
    /// <response code="200">Dashboard payload.</response>
    /// <response code="401">No active grant on this device.</response>
    /// <response code="403">The teacher turned the portal off, or their account is not eligible.</response>
    /// <response code="404">The roster id is not the one this device follows, or the student was removed.</response>
    [HttpGet("students/{rosterId:long}/dashboard")]
    [ProducesResponseType(typeof(Result<ParentPortalDashboardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard([FromRoute] long rosterId)
        => ToResponse(await _portalService.GetDashboardAsync(DeviceHash() ?? string.Empty, rosterId));

    /// <summary>One month of attendance. Omit <c>year</c>/<c>month</c> for the teacher-local (Africa/Cairo) current month.</summary>
    /// <response code="200"><c>{ visible, data }</c> — <c>data</c> is null when the teacher hides attendance.</response>
    [HttpGet("students/{rosterId:long}/attendance")]
    [ProducesResponseType(typeof(Result<ParentPortalAttendanceSectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttendance(
        [FromRoute] long rosterId, [FromQuery] int? year = null, [FromQuery] int? month = null)
        => ToResponse(await _portalService.GetAttendanceAsync(DeviceHash() ?? string.Empty, rosterId, year, month));

    /// <summary>The student's payment tracking screen (paid / overdue / upcoming).</summary>
    /// <response code="200"><c>{ visible, data }</c> — <c>data</c> is null when the teacher hides payments.</response>
    [HttpGet("students/{rosterId:long}/payments")]
    [ProducesResponseType(typeof(Result<ParentPortalPaymentsSectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayments([FromRoute] long rosterId)
        => ToResponse(await _portalService.GetPaymentsAsync(DeviceHash() ?? string.Empty, rosterId));

    /// <summary>
    /// Merged offline (paper) + online exam results, newest first. The summary always spans the
    /// whole history, not the requested page.
    /// </summary>
    /// <response code="200"><c>{ visible, data }</c> — <c>data</c> is null when the teacher hides both exam channels.</response>
    [HttpGet("students/{rosterId:long}/grades")]
    [ProducesResponseType(typeof(Result<ParentPortalGradesSectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGrades(
        [FromRoute] long rosterId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => ToResponse(await _portalService.GetGradesAsync(DeviceHash() ?? string.Empty, rosterId, page, pageSize));

    /// <summary>
    /// The parent removing their own follow-up from this device. Idempotent — a device with no
    /// grant still gets a 200.
    /// </summary>
    /// <response code="200">Access removed (or there was none).</response>
    [HttpDelete("access")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeOwnAccess()
        => ToResponse(await _portalService.RevokeOwnAccessAsync(DeviceHash() ?? string.Empty));

    // ══════════════════════════════════════════════
    // HEADER HELPERS
    // ══════════════════════════════════════════════

    /// <summary>
    /// SHA-256 of the portal's <c>X-Portal-Device</c> header, or null when it is absent. Hashing
    /// happens here (and identically in the service for the request body's <c>deviceId</c>)
    /// through the one shared <see cref="ParentPortalHash"/> helper — the RAW device id never
    /// leaves the request.
    /// </summary>
    private string? DeviceHash() =>
        ParentPortalHash.Compute(Request.Headers[ParentPortalConstants.DeviceHeader].ToString());

    /// <summary>The real client IP the PHP portal forwarded, falling back to the socket peer (which is the portal itself).</summary>
    private string? ClientIp()
    {
        string forwarded = Request.Headers[ParentPortalConstants.ClientIpHeader].ToString();
        return string.IsNullOrWhiteSpace(forwarded)
            ? HttpContext.Connection.RemoteIpAddress?.ToString()
            : forwarded;
    }

    private string? UserAgent()
    {
        string agent = Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(agent) ? null : agent;
    }
}
