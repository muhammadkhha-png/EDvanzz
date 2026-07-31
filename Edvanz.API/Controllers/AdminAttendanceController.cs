using Edvanz.API.Attributes;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Super-admin diagnostic/ops surface for the Attendance Module's nightly auto-absent sweep.
///
/// PURPOSE:
///   Lets a SuperAdmin manually trigger <see cref="IAttendanceAutoAbsentService.SweepTeacherAsync"/>
///   for one teacher on demand — for validating the sweep against seeded test accounts
///   (teacher1/teacher2) in Postman without waiting for the nightly 02:30 Africa/Cairo
///   Hangfire recurring job (AttendanceConstants.AutoAbsentJobId).
///
/// AUTHORIZATION:
///   Class-level [Authorize]; the action carries
///   [ModulePermission(roles: ["SuperAdmin"], roleOnly: true)] — mirrors
///   AdminTutorModuleController / AdminModuleQuotaController / AdminSubscriptionController.
///
/// NOT A HANGFIRE JOB TRIGGER:
///   This calls straight into the Application-layer service the Hangfire worker itself calls
///   (AttendanceAutoAbsentService.SweepTeacherAsync) — synchronously, in-request, no queue
///   involved. Same code path as production, so a Postman run here reflects real sweep
///   behaviour, but it does NOT touch or drain the Hangfire "auto-absent" queue.
///
/// SAFE TO CALL REPEATEDLY:
///   The sweep is idempotent (a slot with an existing record for a student is never re-marked),
///   so calling this twice on the same teacher/day is a no-op the second time.
/// </summary>
[Route("api/admin/attendance")]
[Authorize]
public class AdminAttendanceController : ApiBaseController
{
    private readonly IAttendanceAutoAbsentService _autoAbsentService;
    private readonly ICurrentUserService _currentUser;

    public AdminAttendanceController(
        IAttendanceAutoAbsentService autoAbsentService,
        ICurrentUserService currentUser)
    {
        _autoAbsentService = autoAbsentService;
        _currentUser = currentUser;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT: MANUALLY TRIGGER THE AUTO-ABSENT SWEEP FOR ONE TEACHER
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Runs the exact same sweep the nightly Hangfire dispatcher enqueues per teacher —
    //   synchronously, so the response carries the outcome immediately (no queue, no polling).
    //
    // TABLES WRITTEN: AttendanceRecords, AttendanceEditLogs, StudentAbsenceCounters,
    //                  SessionOccurrences (status) — identical to the nightly job.
    // TABLES READ: Users/Teachers, Sessions, SessionLinks, SessionOccurrences,
    //              StudentSessionAssignments, AttendanceRecords
    //
    // AUTH: SuperAdmin ONLY (roleOnly gate).
    //
    // SAMPLE REQUEST:
    //   POST /api/admin/attendance/auto-absent/sweep/1
    //
    // SAMPLE RESPONSE (200):
    //   {
    //     "teacherId": 1,
    //     "absencesWritten": 4,
    //     "heldRolledForward": 1,
    //     "occurrencesProcessed": 6,
    //     "slotsSkippedNotFullyPassed": 2,
    //     "reason": "Swept"
    //   }
    //   "reason" is also the diagnostic signal: "Disabled" (AutoAbsent:Enabled=false),
    //   "TeacherNotFound", "EmptyWindow" (nothing in [EffectiveFrom, today) yet),
    //   "NoCandidateOccurrences", or "Swept" for a normal run.
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("auto-absent/sweep/{teacherId:long}")]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(AutoAbsentOutcome), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> TriggerAutoAbsentSweep([FromRoute] long teacherId)
    {
        long? adminUserId = _currentUser.UserId;
        if (adminUserId is null)
            return Unauthorized();

        var outcome = await _autoAbsentService.SweepTeacherAsync(teacherId);
        return Ok(outcome);
    }
}