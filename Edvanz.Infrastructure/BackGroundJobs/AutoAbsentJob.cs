using Edvanz.Application.Services;
using Edvanz.Domain.Constants;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Edvanz.Infrastructure.BackGroundJobs;

// ════════════════════════════════════════════════════════════════════════════
// DISPATCHER — nightly, fans out one auto-absent worker per teacher
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Nightly auto-absent dispatcher. Registered as a Hangfire recurring job in Program.cs (default
/// 02:30 Africa/Cairo — off-peak, after the day has fully closed).
///
/// FAN-OUT MODEL (mirrors <see cref="RecurringAssignmentDispatcherJob"/>):
/// 1. Ask the service for teacher ids that held a class within the catch-up window.
/// 2. Enqueue ONE per-teacher worker on the auto-absent queue. Each worker is idempotent (a slot with
///    an existing record is never re-marked), so a re-run of the dispatcher is safe.
/// The kill switch and all gating live in <see cref="IAttendanceAutoAbsentService"/>; when disabled the
/// selector returns an empty set and nothing is enqueued.
/// </summary>
public class AutoAbsentDispatcherJob
{
    private readonly IAttendanceAutoAbsentService _service;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<AutoAbsentDispatcherJob> _logger;

    public AutoAbsentDispatcherJob(
        IAttendanceAutoAbsentService service,
        IBackgroundJobClient backgroundJobs,
        ILogger<AutoAbsentDispatcherJob> logger)
    {
        _service = service;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    /// <summary>Hangfire entry point. Registered as a recurring job in Program.cs.</summary>
    public async Task RunAsync()
    {
        var teacherIds = await _service.GetTeacherIdsToSweepAsync();
        if (teacherIds.Count == 0)
        {
            _logger.LogDebug("Auto-absent dispatcher: no teachers to sweep (disabled or no recent classes).");
            return;
        }

        foreach (long teacherId in teacherIds)
            _backgroundJobs.Enqueue<IAutoAbsentJob>(job => job.SweepTeacherAsync(teacherId));

        _logger.LogInformation(
            "Auto-absent dispatcher: enqueued {Count} per-teacher sweep workers.", teacherIds.Count);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// WORKER INTERFACE — declared here so Hangfire's DI activator binds it
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Per-teacher auto-absent worker. Hangfire instantiates it through DI so each invocation gets a fresh
/// DbContext scope. Mirrors the <see cref="IRecurringAssignmentMaterializerJob"/> pattern.
/// </summary>
public interface IAutoAbsentJob
{
    /// <summary>
    /// Sweeps one teacher's overdue occurrences. Hangfire retries on exception (3 attempts, exponential
    /// backoff); the underlying service is idempotent so retries are safe.
    /// </summary>
    [Queue(AttendanceConstants.AutoAbsentQueue)]
    Task SweepTeacherAsync(long teacherId);
}

// ════════════════════════════════════════════════════════════════════════════
// WORKER IMPLEMENTATION — thin Hangfire wrapper around the service
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Default <see cref="IAutoAbsentJob"/>. All logic lives in <see cref="IAttendanceAutoAbsentService"/>;
/// this wrapper only exists so Hangfire serializes the invocation against an interface.
/// </summary>
public class AutoAbsentJob : IAutoAbsentJob
{
    private readonly IAttendanceAutoAbsentService _service;
    private readonly ILogger<AutoAbsentJob> _logger;

    public AutoAbsentJob(
        IAttendanceAutoAbsentService service,
        ILogger<AutoAbsentJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SweepTeacherAsync(long teacherId)
    {
        var outcome = await _service.SweepTeacherAsync(teacherId);

        // Outcome is already logged inside the service; a debug line here ties it to the Hangfire job id.
        _logger.LogDebug(
            "Auto-absent worker done for teacher {TeacherId}: {Reason} ({Absences} abs, {Held} held).",
            teacherId, outcome.Reason, outcome.AbsencesWritten, outcome.HeldRolledForward);
    }
}
