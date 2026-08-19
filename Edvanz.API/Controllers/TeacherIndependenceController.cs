using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Center;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// A center-owned teacher's self-service to request leaving their center and getting their own
/// independent account. Teacher-role ONLY (plain <see cref="AuthorizeAttribute"/> role gate — NOT
/// <c>[ModulePermission]</c>, which would let a Center/CenterAssistant through; only the teacher
/// themselves may request this). Identity is the teacher resolved from the JWT. Reachable even when
/// the center's subscription has lapsed (<see cref="AllowExpiredSubscriptionAttribute"/>).
/// </summary>
[Route("api/teacher/independence-request")]
[Authorize(Roles = "Teacher")]
[AllowExpiredSubscription]
public class TeacherIndependenceController : ModuleSixApiBaseController
{
    private readonly ITeacherIndependenceService _service;
    private readonly ICurrentUserService _currentUser;

    public TeacherIndependenceController(
        ITeacherIndependenceService service,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
        : base(currentUser, unitOfWork)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Submit a request to become an independent teacher (only valid while under a center).</summary>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitIndependenceRequestDto dto)
    {
        var teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        var userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();
        return ToResponse(await _service.SubmitAsync(teacherId.Value, userId.Value, dto));
    }

    /// <summary>The teacher's most recent independence request + its status (null if never requested).</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        return ToResponse(await _service.GetMyRequestAsync(teacherId.Value));
    }

    /// <summary>Cancel the teacher's own live Pending request.</summary>
    [HttpDelete]
    public async Task<IActionResult> Cancel()
    {
        var teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();
        var userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();
        return ToResponse(await _service.CancelAsync(teacherId.Value, userId.Value));
    }
}
