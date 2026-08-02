using Edvanz.Application.ServiceContract;
using Microsoft.Extensions.Logging;

namespace Edvanz.Infrastructure.BackGroundJobs;

/// <summary>
/// Daily purge of the student recycle bin (REQ-STU-027/028): permanently deletes roster
/// records that have sat soft-deleted past the 10-day retention window.
///
/// WHY THIS EXISTS: <c>ITeacherStudentService.PurgeExpiredRecycleBinRecordsAsync</c> had ZERO
/// callers and no job registration, so the retention the recycle-bin UI advertises
/// (<c>daysRemaining</c>) never actually expired anything — deleted students accumulated
/// forever, holding their unique student code / phone.
///
/// ARCHITECTURE (§6.2): thin Infrastructure wrapper over the Application service — Hangfire
/// types never leak into the Application layer. Registered as a recurring job in Program.cs.
///
/// IDEMPOTENT (§6.4): the service re-reads the expired set each pass and purges each student
/// in its own transaction, so a retry after a partial run just finishes the leftovers.
/// </summary>
public class RecycleBinPurgeJob
{
    private readonly ITeacherStudentService _studentService;
    private readonly ILogger<RecycleBinPurgeJob> _logger;

    public RecycleBinPurgeJob(
        ITeacherStudentService studentService,
        ILogger<RecycleBinPurgeJob> logger)
    {
        _studentService = studentService;
        _logger = logger;
    }

    /// <summary>Hangfire entry point. Registered as a daily recurring job in Program.cs.</summary>
    public async Task RunAsync()
    {
        int purged = await _studentService.PurgeExpiredRecycleBinRecordsAsync();

        if (purged > 0)
            _logger.LogInformation(
                "recycle-bin-purge: permanently deleted {PurgedCount} expired student record(s).",
                purged);
    }
}
