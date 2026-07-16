using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Subscription;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Edvanz.API.Controllers;

/// <summary>
/// Subscription Management Module — teacher-facing endpoints (§4.1 / FR-SUB-003 / FR-SUB-030…040).
///
/// AUTHORIZATION:
///   Class-level [Authorize] requires an authenticated user.
///   Class-level [AllowExpiredSubscription] (FR-SUB-024) bypasses the
///   ActiveSubscriptionHandler so an expired tutor can still:
///     - View their current subscription / days remaining
///     - View their payment history
///     - Run the renewal flow (initiate / submit-manual / poll-status)
///
/// TENANT GUARD:
///   The {teacherId:long} route param is what every endpoint scopes to. The Phase 08
///   audit/access work will verify (in this controller) that the calling user owns
///   the resolved teacher id — for now, the service layer does the data-side guard
///   (every repo query already filters by teacherId).
/// </summary>
[Route("api/subscription")]
[Authorize]
[AllowExpiredSubscription]
public class SubscriptionController : ApiBaseController
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public SubscriptionController(
        ISubscriptionService subscriptionService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _subscriptionService = subscriptionService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: GET CURRENT SUBSCRIPTION (REQ-SUB-003 / FR-SUB-003)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the calling teacher's current subscription with derived status,
    //   days remaining, and the price the next renewal will charge.
    //
    // TABLES READ: TeacherSubscriptions (filtered IsCurrent = true index),
    //              Teachers, StudentCapacityPackages
    // TABLES WRITTEN: none (read-only)
    //
    // SAMPLE: GET /api/subscription/current
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("current")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Subscription.CurrentSubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrent()
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _subscriptionService.GetCurrentAsync(teacherId.Value);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2: GET PAYMENT HISTORY (REQ-SUB-022 / FR-SUB-039)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Paginated subscription payment history. Phone numbers and transaction
    //   references are masked per BR-SUB-011.
    //
    // TABLES READ: TeacherSubscriptions
    //
    // SAMPLE: GET /api/subscription/history?page=1&pageSize=20
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("history")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.Subscription.SubscriptionHistoryItemDto>>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _subscriptionService.GetHistoryPagedAsync(teacherId.Value, page, pageSize);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 3: INITIATE RENEWAL (REQ-SUB-015 / FR-SUB-030 / FR-SUB-040)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Creates a PendingSubscriptionPayment row in Status = Initiated and returns
    //   either a Paymob iframe URL (if PaymobOptions.Enabled = true and the gateway
    //   responded) or the manual-pay instructions (D-04 stub fallback).
    //
    // TABLES WRITTEN: PendingSubscriptionPayments (one new row)
    // TABLES READ: Teachers, StudentCapacityPackages
    //
    // SAMPLE: POST /api/subscription/renew/initiate
    //   { "paymentMethod": "VodafoneCash", "paymentChannel": "Manual" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("renew/initiate")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Subscription.RenewInitiateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InitiateRenewal([FromBody] RenewInitiateRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _subscriptionService.InitiateRenewalAsync(teacherId.Value, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 4: SUBMIT MANUAL PAYMENT DETAILS (FR-SUB-033)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Tutor reports their external transaction reference. Transitions the
    //   pending row to AwaitingSuperAdminApproval and encrypts the submitted
    //   details (REQ-SUB-NFR-004).
    //
    // TABLES WRITTEN: PendingSubscriptionPayments (status + encrypted blob)
    //
    // SAMPLE: POST /api/subscription/renew/manual-submit
    //   { "pendingPaymentId": 5001, "paymentPhoneNumber": "01012345678",
    //     "transactionReference": "VFC987654321" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("renew/manual-submit")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Subscription.RenewStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitManual([FromBody] ManualSubmitRequest request)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _subscriptionService.SubmitManualAsync(teacherId.Value, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5: POLL RENEWAL STATUS
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the current state of a pending payment. Used by the Flutter app
    //   while awaiting webhook callback or admin review.
    //
    // TABLES READ: PendingSubscriptionPayments
    //
    // SAMPLE: GET /api/subscription/renew/status/5001
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("renew/status/{pendingPaymentId:long}")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Subscription.RenewStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRenewalStatus([FromRoute] long pendingPaymentId)
    {
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null) return TeacherNotResolved();

        var result = await _subscriptionService.GetRenewalStatusAsync(teacherId.Value, pendingPaymentId);
        return ToResponse(result);
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    /// <summary>
    /// Resolves the calling user to a teacher id:
    ///   - Teacher role: lookup by user id.
    ///   - Assistant role: load assistant, return owning tutor id (BR-SUB-002).
    /// Returns null when no mapping exists (e.g., a SuperAdmin would never hit
    /// these endpoints).
    /// </summary>
    private async Task<long?> ResolveTeacherIdAsync()
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return null;

        if (string.Equals(_currentUser.Role, "Assistant", StringComparison.Ordinal))
        {
            var assistant = await _currentUser.GetAssistantDataAsync();
            return assistant?.TeacherAccountId;
        }

        var teacher = await _unitOfWork.Users.GetTeacherByUserIdAsync(userId.Value);
        return teacher?.Id;
    }

    /// <summary>
    /// Standardized response when the teacher row cannot be resolved from the
    /// authenticated user. Returns 404 with a generic message — the caller should
    /// not be told whether the role mismatch is auth or data.
    /// </summary>
    private IActionResult TeacherNotResolved()
    {
        return new ObjectResult(new { success = false, message = "Teacher not found" })
        {
            StatusCode = (int)HttpStatusCode.NotFound
        };
    }
}