using Edvanz.API.Attributes;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Payment "Screens" API (<c>api/v1/*</c>) — screen-oriented endpoints backing the frontend
/// <c>payment.json</c> spec. Deliberately a SEPARATE controller from <see cref="PaymentController"/>
/// so it forms its own Swagger heading ("PaymentScreens"), keeping the new screen contract clearly
/// distinguishable from the existing <c>api/payment/*</c> endpoints.
///
/// DESIGN:
/// <list type="bullet">
///   <item>Additive &amp; reuse-first — delegates to <see cref="IPaymentScreenService"/>, which
///         composes existing PaymentRepo/PaymentService logic. No money logic is duplicated.</item>
///   <item>Absolute route templates (<c>/api/v1/...</c>) match the frontend paths exactly,
///         independent of the inherited <c>api/[controller]</c> convention.</item>
///   <item><c>teacherId</c> is always resolved from the JWT via <c>ResolveTeacherIdAsync()</c> —
///         never from the route/body (REQ-PAY-NFR-001 / no IDOR).</item>
///   <item>Every action carries <c>[ModulePermission]</c> exactly like <see cref="PaymentController"/>.</item>
/// </list>
/// </summary>
[Authorize]
public sealed class PaymentScreensController : ModuleSixApiBaseController
{
    private readonly IPaymentScreenService _screenService;

    public PaymentScreensController(
        IPaymentScreenService screenService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
        : base(currentUser, unitOfWork)
    {
        _screenService = screenService;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Screen: SessionPaymentCollectedByMonth
    // GET /api/v1/payments/collections?month=&year=&page=&limit=
    // Paginated ledger of collected payments for a month + year.
    // AUTH: Teacher (module) OR Assistant with Payment.ViewHistory.
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("/api/v1/payments/collections")]
    [ModulePermission(PaymentConstants.ModuleName, PaymentConstants.PermissionViewHistory)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetCollectionsByMonth(
        [FromQuery] int month,
        [FromQuery] int year,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _screenService.GetCollectionsByMonthAsync(
            teacherId.Value, month, year, page, limit);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Screen: AssistantWallet
    // GET /api/v1/assistants/{assistantId}/wallet?page=&limit=
    // Wallet card + paginated recent collections.
    // AUTH: TUTOR-ONLY (Teacher/SuperAdmin) — matches every existing wallet endpoint.
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("/api/v1/assistants/{assistantId:long}/wallet")]
    [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssistantWallet(
        long assistantId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _screenService.GetAssistantWalletScreenAsync(
            teacherId.Value, assistantId, page, limit);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Screen: CollectPayment (student list)
    // GET /api/v1/payments/collect/students?filter=&search=&page=&limit=
    // AUTH: Teacher (module) OR Assistant with Payment.Collect.
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("/api/v1/payments/collect/students")]
    [ModulePermission(PaymentConstants.ModuleName, PaymentConstants.PermissionCollect)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetCollectStudents(
        [FromQuery] string? filter = "all",
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _screenService.GetCollectStudentsAsync(
            teacherId.Value, filter, search, page, limit);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Screen: PaymentTracking (students by status)
    // GET /api/v1/payments/students?month=YYYY-MM&status=paid|prorated|unpaid&page=&limit=
    // AUTH: Teacher (module) OR Assistant with Payment.ViewUnpaidStudents.
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("/api/v1/payments/students")]
    [ModulePermission(PaymentConstants.ModuleName, PaymentConstants.PermissionViewUnpaidStudents)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetStudentsByStatus(
        [FromQuery] string? month,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _screenService.GetStudentsByStatusAsync(
            teacherId.Value, month, status, page, limit);
        return ToResponse(result);
    }
}
