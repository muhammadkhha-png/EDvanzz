using Edvanz.API.Attributes;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Application.Dtos.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Super-admin ops surface for one-time Payment Module maintenance.
///
/// PURPOSE:
///   Runs <see cref="IPaymentService.ReconcileMovedStudentsAsync"/> — the one-time carry-over
///   cleanup for students whose UNPAID/PARTIAL billing was STRANDED under an old session by the
///   pre-carry-over reassign flow. Stranded unpaid-due arrears are moved into the student's CURRENT
///   session, stranded future periods cancelled, partials settled (paid part kept, remainder
///   re-billed), and each student's counter recomputed. The current session's schedule is NOT
///   regenerated (an overlap guard prevents double-billing a month it already bills).
///
/// AUTHORIZATION:
///   Class-level [Authorize]; the action carries [ModulePermission(roles: ["SuperAdmin"],
///   roleOnly: true)] — mirrors AdminAttendanceController / AdminTutorModuleController.
///
/// SAFETY:
///   dryRun defaults to TRUE — a bare POST writes NOTHING and returns exactly what WOULD change
///   (per student: id/name/code, the months/amounts moved / cancelled / settled, and any
///   overlap-skipped months). Pass ?dryRun=false to APPLY the carry-over (each student in its own
///   transaction). Monthly billing only; PerSession students are reported and skipped.
/// </summary>
[Route("api/admin/payments")]
[Authorize]
public class AdminPaymentController : ApiBaseController
{
    private readonly IPaymentService _paymentService;
    private readonly ICurrentUserService _currentUser;

    public AdminPaymentController(
        IPaymentService paymentService,
        ICurrentUserService currentUser)
    {
        _paymentService = paymentService;
        _currentUser = currentUser;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT: RECONCILE STRANDED "MOVED STUDENT" BILLING
    //
    // SAMPLE (preview — writes nothing):
    //   POST /api/admin/payments/reconcile-moved-students
    //   POST /api/admin/payments/reconcile-moved-students?dryRun=true
    //
    // SAMPLE (apply):
    //   POST /api/admin/payments/reconcile-moved-students?dryRun=false
    //
    // AUTH: SuperAdmin ONLY (roleOnly gate).
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("reconcile-moved-students")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(MovedStudentsReconcileReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReconcileMovedStudents([FromQuery] bool dryRun = true)
    {
        if (_currentUser.UserId is null)
            return Unauthorized();

        return ToResponse(await _paymentService.ReconcileMovedStudentsAsync(dryRun));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT: BACKFILL A PAID MONTH BY MOVING AN ADVANCE PAYMENT
    //
    // Corrects a student who paid ahead but whose earlier month has no billing period: moves a
    // fully-paid ADVANCE month's cash back onto a NEW target month (target becomes Paid, the advance
    // month reverts to Unpaid). Total cash paid is invariant. Monthly billing only.
    //
    // SAMPLE (preview — writes nothing):
    //   POST /api/admin/payments/backfill-paid-month?teacherStudentId=1803&targetMonth=2026-07&fromAdvanceMonth=2026-09
    //   POST /api/admin/payments/backfill-paid-month?teacherStudentId=1803&targetMonth=2026-07&fromAdvanceMonth=2026-09&dryRun=true
    //
    // SAMPLE (apply):
    //   POST /api/admin/payments/backfill-paid-month?teacherStudentId=1803&targetMonth=2026-07&fromAdvanceMonth=2026-09&dryRun=false
    //
    // AUTH: SuperAdmin ONLY (roleOnly gate).
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("backfill-paid-month")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(BackfillPaidMonthReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BackfillPaidMonth(
        [FromQuery] long teacherStudentId,
        [FromQuery] string targetMonth,
        [FromQuery] string fromAdvanceMonth,
        [FromQuery] bool dryRun = true)
    {
        if (_currentUser.UserId is null)
            return Unauthorized();

        return ToResponse(await _paymentService.BackfillPaidMonthAsync(
            teacherStudentId, targetMonth, fromAdvanceMonth, dryRun));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT: RESET-AWARE RECOMPUTE OF AN ASSISTANT WALLET BALANCE
    //
    // Repairs a CurrentBalance corrupted by pre-reset reversals (e.g. salma −2700 → 0). Held cash is
    // reconstructed from events AFTER the last full cash hand-over, so refunds of cash already handed to
    // the tutor no longer drive the balance negative. Reports old vs new; TotalCollected is untouched.
    //
    // SAMPLE (preview — writes nothing):
    //   POST /api/admin/payments/recompute-assistant-wallet?assistantId=24
    //   POST /api/admin/payments/recompute-assistant-wallet?assistantId=24&dryRun=true
    //
    // SAMPLE (apply):
    //   POST /api/admin/payments/recompute-assistant-wallet?assistantId=24&dryRun=false
    //
    // AUTH: SuperAdmin ONLY (roleOnly gate).
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("recompute-assistant-wallet")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(RecomputeAssistantWalletReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RecomputeAssistantWallet(
        [FromQuery] long assistantId,
        [FromQuery] bool dryRun = true)
    {
        if (_currentUser.UserId is null)
            return Unauthorized();

        return ToResponse(await _paymentService.RecomputeAssistantWalletAsync(assistantId, dryRun));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT: RECONCILE DUPLICATE PAYMENT PERIODS
    //
    // Repairs students left with a DUPLICATE period ladder for the same session (root cause: a
    // non-idempotent assign that regenerated a full parallel ladder — now fixed). Deletes only the
    // empty, unreferenced Unpaid twins; a twin holding cash or a reference is reported as a conflict.
    //
    // SAMPLE (preview — writes nothing; scope to one teacher, or omit for every teacher):
    //   POST /api/admin/payments/reconcile-duplicate-periods?teacherId=20
    //   POST /api/admin/payments/reconcile-duplicate-periods?teacherId=20&dryRun=true
    //
    // SAMPLE (apply):
    //   POST /api/admin/payments/reconcile-duplicate-periods?teacherId=20&dryRun=false
    //
    // AUTH: SuperAdmin ONLY (roleOnly gate).
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("reconcile-duplicate-periods")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(DuplicatePeriodsReconcileReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReconcileDuplicatePeriods(
        [FromQuery] long? teacherId = null,
        [FromQuery] bool dryRun = true)
    {
        if (_currentUser.UserId is null)
            return Unauthorized();

        return ToResponse(await _paymentService.ReconcileDuplicatePeriodsAsync(teacherId, dryRun));
    }
}
