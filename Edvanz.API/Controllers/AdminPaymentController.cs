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

    /// <summary>
    /// Corrects a withdrawal's RECORDED amount when it differs from the cash physically handed
    /// over (e.g. it swept up refund money the assistant had already paid out before the
    /// 2026-08-24 performer-attribution change). Applying also re-runs the reset-aware wallet
    /// recompute for the assistant.
    /// </summary>
    [HttpPost("adjust-withdrawal")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(AdjustWithdrawalReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AdjustWithdrawal(
        [FromQuery] long walletResetLogId,
        [FromQuery] decimal newAmount,
        [FromQuery] bool dryRun = true)
    {
        if (_currentUser.UserId is null)
            return Unauthorized();

        return ToResponse(await _paymentService.AdjustWithdrawalAmountAsync(walletResetLogId, newAmount, dryRun));
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

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT: RECONCILE ORPHANED (SESSION-LESS) PAYMENT PERIODS
    //
    // Applies the session-delete money lifecycle to LEGACY orphans left by the OLD delete (SessionId
    // nulled, unpaid periods — monthly OR per-session — left lingering as inflated obligations, e.g.
    // student 134A's four Aug @35 per-session rows). Per student: VOID future-unpaid orphans, collapse
    // unpaid arrears-through-current-month into ONE pending monthly carry-forward debt per month
    // (re-prices to the next session on reassignment), keep paid orphans as history, resync the counter.
    //
    // SAMPLE (preview — writes nothing; scope to one teacher, or omit for every teacher):
    //   POST /api/admin/payments/reconcile-orphaned-periods?teacherId=20
    //   POST /api/admin/payments/reconcile-orphaned-periods?teacherId=20&dryRun=true
    //
    // SAMPLE (apply):
    //   POST /api/admin/payments/reconcile-orphaned-periods?teacherId=20&dryRun=false
    //
    // AUTH: SuperAdmin ONLY (roleOnly gate).
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("reconcile-orphaned-periods")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(OrphanedPeriodsReconcileReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReconcileOrphanedPeriods(
        [FromQuery] long? teacherId = null,
        [FromQuery] bool dryRun = true)
    {
        if (_currentUser.UserId is null)
            return Unauthorized();

        return ToResponse(await _paymentService.ReconcileOrphanedPeriodsAsync(teacherId, dryRun));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT: RE-PRORATE NEVER-PAID FIRST-MONTH CARRIED ANCHORS
    //
    // REMEDIATION for the never-paid first-month-MOVE proration WIPE (root cause fixed going-forward in
    // ApplyCarryOverPlanAsync + the DB2a fold-in): a student moved / reassigned between sessions within
    // their first month, before paying anything, had their genuine prorated first month re-priced to FULL
    // price with its anchor flag dropped (prod: student 8990 300×0.3333 → 300). Per AFFECTED student the
    // carried first-month anchor is re-priced to round(sessionOrCustom × first-attendance fraction), its
    // IsProRated / fraction / anchor flag restored, and its counter resynced. Non-qualifying candidates are
    // reported with a reason and left untouched.
    //
    // SAMPLE (preview — writes nothing; scope to one teacher, or omit for every teacher):
    //   POST /api/admin/payments/reprorate-carried-anchors?teacherId=20
    //   POST /api/admin/payments/reprorate-carried-anchors?teacherId=20&dryRun=true
    //
    // SAMPLE (apply):
    //   POST /api/admin/payments/reprorate-carried-anchors?teacherId=20&dryRun=false
    //
    // AUTH: SuperAdmin ONLY (roleOnly gate).
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("reprorate-carried-anchors")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(CarriedAnchorReprorationReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReprorateCarriedAnchors(
        [FromQuery] long? teacherId = null,
        [FromQuery] bool dryRun = true)
    {
        if (_currentUser.UserId is null)
            return Unauthorized();

        return ToResponse(await _paymentService.ReprorateCarriedAnchorsAsync(teacherId, dryRun));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT: SET A TEACHER'S BILLING START (onboarding billing floor, §7.4b)
    //
    // Sets the teacher's BillingStartDate from the support side (normalized to the first of the
    // month) and runs the billing-start reconcile in ONE transaction: never-paid obligations dated
    // before the floor are removed, months an earlier floor newly allows are backfilled, first-month
    // anchors and counters are recomputed. Rows with cash or a hand-set amount are NEVER touched
    // (reported as kept). Also RE-LOCKS the teacher's one-time self-service change.
    //
    // SAMPLE (preview — writes nothing, config keeps its stored value):
    //   POST /api/admin/payments/billing-start?teacherId=123&date=2026-09-01
    //   POST /api/admin/payments/billing-start?teacherId=123&date=2026-09-01&dryRun=true
    //
    // SAMPLE (apply):
    //   POST /api/admin/payments/billing-start?teacherId=123&date=2026-09-01&dryRun=false
    //
    // AUTH: SuperAdmin ONLY (roleOnly gate).
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("billing-start")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(BillingStartAdminResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetBillingStart(
        [FromQuery] long teacherId,
        [FromQuery] DateTime date,
        [FromQuery] bool dryRun = true)
    {
        if (_currentUser.UserId is null)
            return Unauthorized();

        return ToResponse(await _paymentService.SetBillingStartForTeacherAsync(teacherId, date, dryRun));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT: RE-GRANT A TEACHER'S ONE-TIME BILLING-START CHANGE
    //
    // The teacher's self-service BillingStartDate set is ONE-TIME (further changes 403
    // BillingStartDateLocked). This re-grants exactly one more change — the support flow for the
    // yearly onboarding reset, or for a teacher who picked the wrong month on the first try.
    // Consumed by the teacher's next successful change (or by an admin set).
    //
    // SAMPLE:
    //   POST /api/admin/payments/billing-start/allow-change?teacherId=123
    //
    // AUTH: SuperAdmin ONLY (roleOnly gate).
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("billing-start/allow-change")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AllowBillingStartChange([FromQuery] long teacherId)
    {
        if (_currentUser.UserId is null)
            return Unauthorized();

        return ToResponse(await _paymentService.AllowBillingStartChangeAsync(teacherId));
    }
}
