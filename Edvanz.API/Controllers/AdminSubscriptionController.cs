using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Subscription;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Edvanz.API.Controllers;

/// <summary>
/// Subscription Management Module — super-admin endpoints (§4.4 / FR-SUB-060…064).
///
/// AUTHORIZATION:
///   Class-level [Authorize] requires authentication.
///   Every action carries [ModulePermission(roles: ["SuperAdmin"], roleOnly: true)].
///   The ActiveSubscriptionHandler short-circuits SuperAdmin role on its fast path
///   so no [AllowExpiredSubscription] is needed here.
///
/// AUDIT:
///   Every write captures the calling admin's user id via ICurrentUserService and
///   passes it to the service layer as resolvedByUserId / adminUserId so
///   TeacherSubscription.CreatedByUserId records who took the action (REQ-ADM-016 / FR-SUB-064).
/// </summary>
[Route("api/admin/subscriptions")]
[Authorize]
public class AdminSubscriptionController : ApiBaseController
{
    private readonly IAdminSubscriptionService _adminService;
    private readonly ICurrentUserService _currentUser;

    public AdminSubscriptionController(
        IAdminSubscriptionService adminService,
        ICurrentUserService currentUser)
    {
        _adminService = adminService;
        _currentUser = currentUser;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: ACTIVATE (FR-SUB-060 / REQ-ADM-012)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Manually activates a teacher's subscription with no payment record.
    //   Inserts a new TeacherSubscription row with PaymentChannel = SuperAdminOverride.
    //
    // TABLES WRITTEN: TeacherSubscriptions (flips previous IsCurrent + inserts new)
    // CACHE: invalidated synchronously after commit
    //
    // SAMPLE: POST /api/admin/subscriptions/activate
    //   { "teacherId": 42, "startDate": null, "endDate": null }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("activate")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate([FromBody] AdminActivateRequest request)
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null) return AdminNotResolved();

        var result = await _adminService.ActivateAsync(adminUserId.Value, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2: EXTEND (FR-SUB-061 / REQ-ADM-016)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Extends the teacher's CURRENT subscription EndDate by N days.
    //   Does NOT create a new row — mutates the current row in place.
    //
    // TABLES WRITTEN: TeacherSubscriptions (EndDate update on current row)
    //
    // SAMPLE: POST /api/admin/subscriptions/extend
    //   { "teacherId": 42, "extensionDays": 7 }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("extend")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Extend([FromBody] AdminExtendRequest request)
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null) return AdminNotResolved();

        var result = await _adminService.ExtendAsync(adminUserId.Value, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 3: SET END DATE (FR-SUB-062 / REQ-ADM-015)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Overrides the EndDate on a specific TeacherSubscription row (current or historical).
    //
    // TABLES WRITTEN: TeacherSubscriptions
    //
    // SAMPLE: PUT /api/admin/subscriptions/end-date
    //   { "subscriptionId": 9001, "newEndDate": "2026-06-30T00:00:00Z" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("end-date")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEndDate([FromBody] AdminSetEndDateRequest request)
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null) return AdminNotResolved();

        var result = await _adminService.SetEndDateAsync(adminUserId.Value, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 4: GET PENDING APPROVAL QUEUE (FR-SUB-063)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Paginated list of pending payments awaiting super-admin approval.
    //   Phone number and transaction reference are decrypted server-side for review.
    //
    // TABLES READ: PendingSubscriptionPayments, Teachers, Users
    //
    // SAMPLE: GET /api/admin/subscriptions/pending?page=1&pageSize=20
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("pending")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingQueue(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _adminService.GetPendingQueueAsync(page, pageSize);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5: APPROVE PENDING PAYMENT (FR-SUB-063)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Approves a pending payment. Delegates to ISubscriptionService.ConfirmPaymentAsync —
    //   one confirm pipeline (§6.3) serves both webhook and manual-approval paths.
    //   EC-24 guard: refuses approval if a current sub was created within the last 24h.
    //
    // TABLES WRITTEN: TeacherSubscriptions (new IsCurrent row), PendingSubscriptionPayments (resolution)
    //
    // SAMPLE: POST /api/admin/subscriptions/pending/5001/approve
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("pending/{pendingPaymentId:long}/approve")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApprovePending([FromRoute] long pendingPaymentId)
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null) return AdminNotResolved();

        var result = await _adminService.ApprovePendingAsync(adminUserId.Value, pendingPaymentId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 6: REJECT PENDING PAYMENT (FR-SUB-063)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Rejects a pending payment with a reason. Enqueues
    //   IPendingPaymentRejectedNotificationJob to inform the teacher (push + WA + inbox).
    //
    // TABLES WRITTEN: PendingSubscriptionPayments (status = Rejected, RejectionReason)
    //
    // SAMPLE: POST /api/admin/subscriptions/pending/5001/reject
    //   { "rejectionReason": "Transaction reference not found in provider dashboard" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("pending/{pendingPaymentId:long}/reject")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectPending(
        [FromRoute] long pendingPaymentId,
        [FromBody] RejectPendingRequest request)
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null) return AdminNotResolved();

        var result = await _adminService.RejectPendingAsync(
            adminUserId.Value, pendingPaymentId, request.RejectionReason);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 7: UPDATE PACKAGE PRICE (REQ-ADM-016 / BR-SUB-009)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Updates a StudentCapacityPackage's MonthlyPriceEGP. Records who/when.
    //   In-flight pending payments retain their initiation-time price snapshot.
    //
    // TABLES WRITTEN: StudentCapacityPackages
    //
    // SAMPLE: PUT /api/admin/subscriptions/packages/3/price
    //   { "newMonthlyPriceEGP": 250.00 }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("packages/{packageId:long}/price")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePackagePrice(
        [FromRoute] long packageId,
        [FromBody] UpdatePackagePriceRequest request)
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null) return AdminNotResolved();

        var result = await _adminService.UpdatePackagePriceAsync(
            adminUserId.Value, packageId, request.NewMonthlyPriceEGP);
        return ToResponse(result);
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    /// <summary>
    /// Standardized response when the calling admin's user id cannot be read
    /// from claims. The [Authorize] + [ModulePermission] attributes already
    /// enforce that the caller is authenticated as SuperAdmin, so reaching this
    /// branch indicates a token/claim shape mismatch.
    /// </summary>
    private IActionResult AdminNotResolved()
    {
        return new ObjectResult(new { success = false, message = "Admin user not resolved" })
        {
            StatusCode = (int)HttpStatusCode.Unauthorized
        };
    }
}