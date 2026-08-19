using Edvanz.Application.Dtos.Center;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Center-facing endpoints (role Center / CenterAssistant). Identity is ALWAYS the center resolved
/// from the JWT — never from the route/body. These operate at CENTER scope (overview + teacher
/// management); to operate a specific teacher's modules the client sends the X-Acting-Teacher-Id
/// header to the existing teacher endpoints instead.
/// </summary>
[Route("api/center")]
[Authorize(Roles = "Center,CenterAssistant")]
public class CenterController : ApiBaseController
{
    private readonly ICenterService _centerService;
    private readonly ICenterSubscriptionService _centerSubscriptionService;
    private readonly ICenterRevenueService _centerRevenueService;
    private readonly ICenterAssistantService _centerAssistantService;
    private readonly ICurrentUserService _currentUser;

    public CenterController(
        ICenterService centerService,
        ICenterSubscriptionService centerSubscriptionService,
        ICenterRevenueService centerRevenueService,
        ICenterAssistantService centerAssistantService,
        ICurrentUserService currentUser)
    {
        _centerService = centerService;
        _centerSubscriptionService = centerSubscriptionService;
        _centerRevenueService = centerRevenueService;
        _centerAssistantService = centerAssistantService;
        _currentUser = currentUser;
    }

    /// <summary>The center id resolved from the JWT (Center login, or a CenterAssistant's center).</summary>
    private async Task<long?> ResolveCenterIdAsync() => (await _currentUser.GetCenterDataAsync())?.Id;

    private IActionResult CenterNotResolved() =>
        new ObjectResult(new { success = false, message = "Center could not be resolved from token." })
        { StatusCode = StatusCodes.Status404NotFound };

    // Owner-only: the overview carries the center's financial totals + its cut.
    [HttpGet("overview")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> GetOverview()
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerService.GetOverviewAsync(centerId.Value));
    }

    // ── Center settings (center-controlled: revenue-share % + student-code mode) ──

    [HttpGet("settings")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> GetSettings()
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerService.GetSettingsAsync(centerId.Value));
    }

    [HttpPut("settings")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateCenterSettingsDto dto)
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerService.UpdateSettingsAsync(centerId.Value, dto));
    }

    [HttpGet("teachers")]
    public async Task<IActionResult> GetTeachers()
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerService.GetTeachersAsync(centerId.Value));
    }

    // Teacher-account management is owner-only (a center assistant operates teachers via acting-as,
    // it does not create/edit/deactivate teacher accounts).
    [HttpPost("teachers")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> CreateTeacher([FromBody] CreateCenterTeacherDto dto)
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        var actingUserId = _currentUser.UserId;
        if (actingUserId is null) return UserNotResolved();
        return ToResponse(await _centerService.CreateTeacherAsync(centerId.Value, actingUserId.Value, dto));
    }

    [HttpPut("teachers/{teacherId:long}")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> UpdateTeacher([FromRoute] long teacherId, [FromBody] UpdateCenterTeacherDto dto)
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerService.UpdateTeacherAsync(centerId.Value, teacherId, dto));
    }

    [HttpPost("teachers/{teacherId:long}/deactivate")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> DeactivateTeacher([FromRoute] long teacherId)
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerService.DeactivateTeacherAsync(centerId.Value, teacherId));
    }

    [HttpPost("teachers/{teacherId:long}/activate")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> ReactivateTeacher([FromRoute] long teacherId)
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerService.ReactivateTeacherAsync(centerId.Value, teacherId));
    }

    // ── Teacher login management (owner-only): the center creates the login + can reset the password ──

    /// <summary>Give a center-owned teacher a working login (username + initial password), so the
    /// teacher can sign in and operate their own account normally. Everything stays the same tenant,
    /// so the center's acting-as view and the teacher's own view are in sync.</summary>
    [HttpPost("teachers/{teacherId:long}/enable-login")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> EnableTeacherLogin([FromRoute] long teacherId, [FromBody] EnableCenterTeacherLoginDto dto)
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerService.EnableTeacherLoginAsync(centerId.Value, teacherId, dto));
    }

    /// <summary>Center-managed password reset for one of its teachers (no old password needed).</summary>
    [HttpPost("teachers/{teacherId:long}/reset-password")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> ResetTeacherPassword([FromRoute] long teacherId, [FromBody] ResetCenterTeacherPasswordDto dto)
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerService.ResetTeacherPasswordAsync(centerId.Value, teacherId, dto));
    }

    /// <summary>Turn off a teacher's login (blocks sign-in + revokes sessions) without deleting them.</summary>
    [HttpPost("teachers/{teacherId:long}/disable-login")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> DisableTeacherLogin([FromRoute] long teacherId)
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerService.DisableTeacherLoginAsync(centerId.Value, teacherId));
    }

    // ── Subscription (the quota package) — owner-only ──

    [HttpGet("subscription")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> GetSubscription()
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerSubscriptionService.GetSubscriptionAsync(centerId.Value));
    }

    [HttpPost("subscription/request")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> SubmitSubscriptionRequest([FromBody] SubmitCenterSubscriptionRequestDto dto)
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        var userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();
        return ToResponse(await _centerSubscriptionService.SubmitRequestAsync(centerId.Value, userId.Value, dto));
    }

    [HttpDelete("subscription/request")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> CancelSubscriptionRequest()
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        var userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();
        return ToResponse(await _centerSubscriptionService.CancelRequestAsync(centerId.Value, userId.Value));
    }

    // ── Revenue report — owner-only (financial) ──

    [HttpGet("revenue")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> GetRevenue([FromQuery] string? month)
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerRevenueService.GetRevenueAsync(centerId.Value, month));
    }

    // ── Front-desk center-wide code resolve (shared codes disambiguation) ──

    [HttpGet("students/resolve")]
    public async Task<IActionResult> ResolveStudentByCode([FromQuery] string? code)
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerService.ResolveStudentByCodeAsync(centerId.Value, code));
    }

    // ── Center assistants (managed by the center OWNER only) ──

    [HttpGet("assistants")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> GetAssistants()
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerAssistantService.GetAssistantsAsync(centerId.Value));
    }

    [HttpPost("assistants")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> CreateAssistant([FromBody] CreateCenterAssistantDto dto)
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        var actingUserId = _currentUser.UserId;
        if (actingUserId is null) return UserNotResolved();
        return ToResponse(await _centerAssistantService.CreateAsync(centerId.Value, actingUserId.Value, dto));
    }

    [HttpPost("assistants/{centerAssistantId:long}/deactivate")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> DeactivateAssistant([FromRoute] long centerAssistantId)
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerAssistantService.DeactivateAsync(centerId.Value, centerAssistantId));
    }

    [HttpPost("assistants/{centerAssistantId:long}/activate")]
    [Authorize(Roles = "Center")]
    public async Task<IActionResult> ReactivateAssistant([FromRoute] long centerAssistantId)
    {
        var centerId = await ResolveCenterIdAsync();
        if (centerId is null) return CenterNotResolved();
        return ToResponse(await _centerAssistantService.ReactivateAsync(centerId.Value, centerAssistantId));
    }
}
