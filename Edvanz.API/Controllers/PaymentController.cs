using Edvanz.Application.Dtos.Payment;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// API controller for the Payment Module (Module 4).
/// Manages payment collection, editing, deletion, unpaid overview,
/// custom amounts, assistant wallets, dashboard, departure, transfer,
/// offline sync, and student/parent payment views.
/// All endpoints are teacher-scoped via the teacherId route parameter.
///
/// All endpoint documentation follows the existing project pattern:
/// WHAT IT DOES → TABLES READ/WRITTEN → SAMPLE REQUEST.
/// </summary>
public class PaymentController : ApiBaseController
{
    private readonly IPaymentService _paymentService;
    private readonly IPaymentReportService _reportService;

    public PaymentController(
        IPaymentService paymentService,
        IPaymentReportService reportService)
    {
        _paymentService = paymentService;
        _reportService = reportService;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: COLLECT PAYMENT
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Collects a payment from a student for a session.
    //   REQ-PAY-001/002: All four collection methods produce a unified record.
    //   REQ-PAY-018/019: Automatically assigned to earliest unpaid period.
    //   REQ-PAY-020/026: Duplicate and already-paid detection.
    //
    // TABLES WRITTEN: PaymentTransactions, PaymentPeriods, StudentPaymentCounters, AssistantWallets
    // TABLES READ: Teachers, Sessions, TeacherStudents, StudentSessionAssignments, PaymentPeriods
    //
    // SAMPLE: POST /api/payment/123/collect
    //   { "teacherStudentId": 456, "sessionId": 789, "amount": 200.00,
    //     "paymentMethod": "BarcodeScan" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{teacherId:long}/collect")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CollectPayment(
        [FromRoute] long teacherId,
        [FromBody] CollectPaymentDto dto)
    {
        dto.TeacherId = teacherId;
        var result = await _paymentService.CollectPaymentAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2: GET STUDENT PAYMENT STATUS
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the payment status for a student on the collection screen.
    //   REQ-PAY-NFR-006: Displayed immediately after student identification.
    //
    // TABLES READ: TeacherStudents, StudentPaymentCounters, PaymentPeriods
    //
    // SAMPLE: GET /api/payment/123/students/456/status
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/students/{teacherStudentId:long}/status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentPaymentStatus(
        [FromRoute] long teacherId,
        [FromRoute] long teacherStudentId)
    {
        var result = await _paymentService.GetStudentPaymentStatusAsync(teacherId, teacherStudentId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 3: CHECK DUPLICATE PAYMENT
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Checks for same-day duplicate or already-paid period before collection.
    //   REQ-PAY-020/026: Pre-collection validation.
    //
    // TABLES READ: PaymentTransactions, PaymentPeriods
    //
    // SAMPLE: GET /api/payment/123/students/456/duplicate-check
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/students/{teacherStudentId:long}/duplicate-check")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckDuplicate(
        [FromRoute] long teacherId,
        [FromRoute] long teacherStudentId)
    {
        var result = await _paymentService.CheckDuplicatePaymentAsync(teacherId, teacherStudentId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 4: EDIT PAYMENT
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Edits an existing payment transaction (amount, status, period reassignment).
    //   BR-PAY-002: Only the tutor role may edit.
    //
    // TABLES WRITTEN: PaymentTransactions, PaymentEditLogs, PaymentPeriods, StudentPaymentCounters
    // TABLES READ: PaymentTransactions
    //
    // SAMPLE: PUT /api/payment/123/transactions/789
    //   { "newAmount": 150.00, "editReason": "Correction" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("{teacherId:long}/transactions/{transactionId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditPayment(
        [FromRoute] long teacherId,
        [FromRoute] long transactionId,
        [FromBody] EditPaymentDto dto)
    {
        dto.TeacherId = teacherId;
        dto.TransactionId = transactionId;
        var result = await _paymentService.EditPaymentAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5: DELETE PAYMENT
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Soft-deletes a payment transaction.
    //   BR-PAY-002: Only the tutor role may delete.
    //
    // TABLES WRITTEN: PaymentTransactions, PaymentEditLogs, PaymentPeriods, StudentPaymentCounters
    //
    // SAMPLE: DELETE /api/payment/123/transactions/789
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("{teacherId:long}/transactions/{transactionId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePayment(
        [FromRoute] long teacherId,
        [FromRoute] long transactionId,
        [FromQuery] long deletedByUserId)
    {
        var result = await _paymentService.DeletePaymentAsync(teacherId, transactionId, deletedByUserId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5b: PAYMENT EDIT HISTORY
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns the edit/modification history for a specific payment transaction.
    //   REQ-PAY-027: Edit history accessible to tutor.
    //
    // TABLES READ: PaymentEditLogs
    //
    // SAMPLE: GET /api/payment/123/transactions/789/edit-logs
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/transactions/{transactionId:long}/edit-logs")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaymentEditLogs(
        [FromRoute] long teacherId,
        [FromRoute] long transactionId)
    {
        var result = await _paymentService.GetPaymentEditHistoryAsync(teacherId, transactionId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 6: STUDENT PAYMENT HISTORY
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns complete payment history for a student across all sessions.
    //   REQ-PAY-052: Unified timeline with periods, transfers, departures.
    //
    // TABLES READ: PaymentTransactions, PaymentPeriods, SessionTransferEvents, StudentDepartures
    //
    // SAMPLE: GET /api/payment/123/students/456/history?page=1&pageSize=20
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/students/{teacherStudentId:long}/history")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentHistory(
        [FromRoute] long teacherId,
        [FromRoute] long teacherStudentId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _paymentService.GetStudentPaymentHistoryAsync(
            teacherId, teacherStudentId, startDate, endDate, page, pageSize);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 7/8: CUSTOM AMOUNT (GET/SET)
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/students/{teacherStudentId:long}/custom-amount")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomAmount(
        [FromRoute] long teacherId,
        [FromRoute] long teacherStudentId)
    {
        var result = await _paymentService.GetCustomAmountAsync(teacherId, teacherStudentId);
        return ToResponse(result);
    }

    [HttpPut("{teacherId:long}/students/{teacherStudentId:long}/custom-amount")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCustomAmount(
        [FromRoute] long teacherId,
        [FromRoute] long teacherStudentId,
        [FromBody] SetCustomAmountDto dto)
    {
        dto.TeacherId = teacherId;
        dto.TeacherStudentId = teacherStudentId;
        var result = await _paymentService.SetCustomAmountAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 9: UNPAID STUDENTS OVERVIEW
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/unpaid")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnpaidStudents(
        [FromRoute] long teacherId,
        [FromQuery] UnpaidStudentsFilterDto filter)
    {
        var result = await _paymentService.GetUnpaidStudentsAsync(teacherId, filter);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 10: UNPAID COUNT BY SESSION (badge)
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/sessions/{sessionId:long}/unpaid-count")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnpaidCount(
        [FromRoute] long teacherId,
        [FromRoute] long sessionId)
    {
        var result = await _paymentService.GetUnpaidCountBySessionAsync(teacherId, sessionId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 11: COLLECTOR VIEW
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/collectors")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCollectorSummary(
        [FromRoute] long teacherId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var result = await _paymentService.GetCollectorSummaryAsync(teacherId, startDate, endDate);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 12/13/14: WALLET MANAGEMENT
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/wallets")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllWallets([FromRoute] long teacherId)
    {
        var result = await _paymentService.GetAllWalletsAsync(teacherId);
        return ToResponse(result);
    }

    [HttpGet("{teacherId:long}/wallets/{assistantId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWalletDetail(
        [FromRoute] long teacherId,
        [FromRoute] long assistantId)
    {
        var result = await _paymentService.GetWalletDetailAsync(teacherId, assistantId);
        return ToResponse(result);
    }

    [HttpPost("{teacherId:long}/wallets/{assistantId:long}/reset")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetWallet(
        [FromRoute] long teacherId,
        [FromRoute] long assistantId,
        [FromBody] WalletResetDto dto)
    {
        dto.TeacherId = teacherId;
        dto.AssistantId = assistantId;
        var result = await _paymentService.ResetWalletAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 15: DASHBOARD
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/dashboard")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(
        [FromRoute] long teacherId,
        [FromQuery] PaymentDashboardFilterDto filter)
    {
        var result = await _paymentService.GetDashboardAsync(teacherId, filter);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 16/17: DEPARTURE
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/students/{teacherStudentId:long}/departure-summary")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDepartureSummary(
        [FromRoute] long teacherId,
        [FromRoute] long teacherStudentId)
    {
        var result = await _paymentService.GetDepartureSummaryAsync(teacherId, teacherStudentId);
        return ToResponse(result);
    }

    [HttpPost("{teacherId:long}/departure/confirm")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmDeparture(
        [FromRoute] long teacherId,
        [FromBody] ConfirmDepartureDto dto)
    {
        dto.TeacherId = teacherId;
        var result = await _paymentService.ConfirmDepartureAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 18/19: SESSION TRANSFER
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/students/{teacherStudentId:long}/transfer-summary")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransferSummary(
        [FromRoute] long teacherId,
        [FromRoute] long teacherStudentId,
        [FromQuery] long sourceSessionId,
        [FromQuery] long destinationSessionId)
    {
        var result = await _paymentService.GetTransferSummaryAsync(
            teacherId, teacherStudentId, sourceSessionId, destinationSessionId);
        return ToResponse(result);
    }

    [HttpPost("{teacherId:long}/transfer/confirm")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmTransfer(
        [FromRoute] long teacherId,
        [FromBody] ConfirmTransferDto dto)
    {
        dto.TeacherId = teacherId;
        var result = await _paymentService.ConfirmTransferAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 20: OFFLINE SYNC
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{teacherId:long}/sync")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> SyncOfflinePayments(
        [FromRoute] long teacherId,
        [FromBody] OfflinePaymentSyncRequestDto dto)
    {
        dto.TeacherId = teacherId;
        var result = await _paymentService.SyncOfflinePaymentsAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 21: STUDENT/PARENT PAYMENT VIEW
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{teacherId:long}/students/{teacherStudentId:long}/payment-view")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStudentPaymentView(
        [FromRoute] long teacherId,
        [FromRoute] long teacherStudentId)
    {
        var result = await _paymentService.GetStudentPaymentViewAsync(teacherId, teacherStudentId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 22: GENERATE REPORT
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{teacherId:long}/reports/generate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateReport(
        [FromRoute] long teacherId,
        [FromBody] PaymentReportRequestDto request)
    {
        var result = await _reportService.GenerateReportAsync(teacherId, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 23: EXPORT REPORT
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("{teacherId:long}/reports/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportReport(
        [FromRoute] long teacherId,
        [FromBody] PaymentReportRequestDto request,
        [FromQuery] string format = "xlsx")
    {
        var result = await _reportService.ExportReportAsync(teacherId, request, format);
        if (!result.IsSuccess)
            return ToResponse(result);

        var contentType = format == "pdf" ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        var fileName = $"payment-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{format}";

        return File(result.Data!, contentType, fileName);
    }
}