using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Payment;
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
    // DASH-1: `month` accepts the unified "YYYY-MM" string (same as tracking/students) OR the
    //   legacy integer month (1-12) + separate `year`. Both optional → current local month/year.
    // AUTH: Teacher (module) OR Assistant with Payment.ViewHistory.
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("/api/v1/payments/collections")]
    [ModulePermission(PaymentConstants.ModuleName, PaymentConstants.PermissionViewHistory)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.CollectionsByMonthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetCollectionsByMonth(
        [FromQuery] string? month,
        [FromQuery] int? year,
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
    // AUTH: Teacher (module) → requested assistant. Assistant with Payment.ViewCollectorSummary →
    //   forced to their OWN wallet. Withdraw (below) stays tutor-only. [interim — TODO(assistant-dashboard)]
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("/api/v1/assistants/{assistantId:long}/wallet")]
    // TODO(assistant-dashboard): interim. Was roleOnly tutor-only; an assistant caller is now forced
    // to their OWN wallet (route assistantId ignored for assistants — see the service). This is the
    // assistant's own-collections view; a dedicated assistant dashboard is TBD (frontend + backend).
    [ModulePermission(PaymentConstants.ModuleName, PaymentConstants.PermissionViewCollectorSummary)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.AssistantWalletScreenResponse>), StatusCodes.Status200OK)]
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

        // Assistant → forced to their own wallet; Teacher/SuperAdmin → the requested assistant.
        var result = await _screenService.GetAssistantWalletScreenAsync(
            teacherId.Value, assistantId, page, limit, AssistantScopeUserId());
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Screen: CollectPayment (student list)
    // GET /api/v1/payments/collect/students?filter=&search=&page=&limit=
    // AUTH: Teacher (module) OR Assistant with Payment.Collect.
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("/api/v1/payments/collect/students")]
    [ModulePermission(PaymentConstants.ModuleName, PaymentConstants.PermissionCollect)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.CollectStudentsResponse>), StatusCodes.Status200OK)]
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
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.StudentsByStatusResponse>), StatusCodes.Status200OK)]
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

    // ══════════════════════════════════════════════════════════════════════════
    // Screen: SessionPaymentCollectedByYear
    // GET /api/v1/payments/collections/yearly?month=&year=&page=&limit=
    // DASH-1: a yearly view needs only the YEAR — derived from the unified "YYYY-MM" `month`
    //   string (its year) OR the legacy integer `year`. Both optional → current local year.
    // AUTH: Teacher (module) OR Assistant with Payment.ViewHistory.
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("/api/v1/payments/collections/yearly")]
    [ModulePermission(PaymentConstants.ModuleName, PaymentConstants.PermissionViewHistory)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.YearlyCollectionsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetYearlyCollections(
        [FromQuery] string? month,
        [FromQuery] int? year,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _screenService.GetYearlyCollectionsAsync(
            teacherId.Value, month, year, page, limit);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Screen: CollectPaymentSession (lookup)
    // GET /api/v1/collect/lookup?qr=&code=&name=
    // AUTH: Teacher (module) OR Assistant with Payment.Collect.
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("/api/v1/collect/lookup")]
    [ModulePermission(PaymentConstants.ModuleName, PaymentConstants.PermissionCollect)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.CollectLookupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ResolveLookup(
        [FromQuery] string? qr,
        [FromQuery] string? code,
        [FromQuery] string? name)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _screenService.ResolveLookupAsync(teacherId.Value, qr, code, name);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Screen: PaymentTracking (monthly aggregate — loads the whole screen)
    // GET /api/v1/payments/tracking?month=YYYY-MM
    // AUTH: Teacher (module) OR Assistant with Payment.ViewUnpaidStudents.
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("/api/v1/payments/tracking")]
    [ModulePermission(PaymentConstants.ModuleName, PaymentConstants.PermissionViewUnpaidStudents)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.TrackingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetTracking([FromQuery] string? month)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _screenService.GetTrackingAsync(teacherId.Value, month);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Screen: CollectPayment — bulk mark-paid  (MONEY)
    // POST /api/v1/payments/collect/mark-paid   body { studentIds: [] }
    // Header: Idempotency-Key (optional) — replay returns the original result.
    // AUTH: Teacher (module) OR Assistant with Payment.Collect.
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("/api/v1/payments/collect/mark-paid")]
    [ModulePermission(PaymentConstants.ModuleName, PaymentConstants.PermissionCollect)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.MarkPaidResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> MarkPaid(
        [FromBody] MarkPaidRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _screenService.MarkPaidAsync(
            teacherId.Value, GetActingUserId(),
            request?.StudentIds ?? new List<long>(), idempotencyKey);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Screen: CollectPaymentSession — submit batch  (MONEY)
    // POST /api/v1/collect/submit   body { month?, classSessionId?, students: [{studentId, amount}] }
    // Header: Idempotency-Key (optional) — replay returns the original result. 409 on empty batch.
    // AUTH: Teacher (module) OR Assistant with Payment.Collect.
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("/api/v1/collect/submit")]
    [ModulePermission(PaymentConstants.ModuleName, PaymentConstants.PermissionCollect)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.SubmitCollectionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitCollection(
        [FromBody] SubmitCollectionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _screenService.SubmitCollectionAsync(
            teacherId.Value, GetActingUserId(),
            request?.Month, request?.ClassSessionId,
            request?.Students ?? new List<SubmitCollectionItem>(), idempotencyKey);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Screen: AssistantWallet — withdraw  (MONEY, TUTOR-ONLY)
    // POST /api/v1/assistants/{assistantId}/wallet/withdraw   body { amount? }
    // Header: Idempotency-Key (optional). The tutor takes collected cash from the assistant.
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("/api/v1/assistants/{assistantId:long}/wallet/withdraw")]
    [ModulePermission(roles: new[] { "Teacher", "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Payment.WalletWithdrawResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> WithdrawFromWallet(
        long assistantId,
        [FromBody] WalletWithdrawRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _screenService.WithdrawAsync(
            teacherId.Value, assistantId, request?.Amount, GetActingUserId(), idempotencyKey);
        return ToResponse(result);
    }
}
