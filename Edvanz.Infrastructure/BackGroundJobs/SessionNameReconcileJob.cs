using Edvanz.Application.Options;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Edvanz.Infrastructure.BackGroundJobs;

/// <summary>
/// Repairs stored session names that have drifted from the live <c>Sessions.SessionName</c>.
/// </summary>
/// <remarks>
/// Eight tables keep a denormalized copy of the session name so a name survives a session HARD
/// delete (BR-ATT-005), and every screen reads those columns directly — no join, on paths that run
/// per mark and per page. <see cref="ISessionRepo.PropagateSessionNameAsync"/> keeps them current
/// from the moment a teacher renames a class.
///
/// Two things it cannot do, which is why this job exists:
/// <list type="bullet">
///   <item>Rows that went stale BEFORE propagation shipped are never rewritten by anything else —
///         periods are pre-generated one per month to the session's end date, and attendance rows
///         are never revisited. On live data that backlog GREW every month (0 stale payment rows
///         in July, 8 in August, 50 in September, 118 in October).</item>
///   <item>If a future change ever adds a ninth session-name column and forgets to propagate it,
///         nothing else would catch the drift. Left running, this is that net.</item>
/// </list>
///
/// DELIBERATELY NOT A MIGRATION. The deploy applies migrations with <c>azure/sql-action</c> BEFORE
/// <c>az webapp deploy</c>, so the currently deployed app is live and teachers are marking
/// attendance while they run. A repair of unbounded size there takes an exclusive lock on more than
/// SQL Server's ~5000-row escalation threshold and holds a TABLE lock on <c>AttendanceRecords</c>
/// until the migration commits — every attendance write blocked — and its total runtime counts
/// against one command timeout, so a slow repair extends or fails the deploy. Here it runs after
/// the app is up, in batches that autocommit, and a run that does not finish simply resumes
/// tomorrow.
///
/// Idempotent (§6.4): every statement is guarded on the name actually differing, so a second pass
/// over repaired rows writes nothing.
/// </remarks>
public class SessionNameReconcileJob
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly SessionNameReconcileOptions _options;
    private readonly ILogger<SessionNameReconcileJob> _logger;

    public SessionNameReconcileJob(
        IUnitOfWork unitOfWork,
        IOptions<SessionNameReconcileOptions> options,
        ILogger<SessionNameReconcileJob> logger)
    {
        _unitOfWork = unitOfWork;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Runs one bounded pass. Logs only when it actually repaired something — in steady state this
    /// job writes nothing and should stay silent.
    /// </summary>
    /// <remarks>
    /// Batch size and the per-table ceiling come from <see cref="SessionNameReconcileOptions"/>, so
    /// a run that proves too heavy for the database tier can be shrunk — or stopped outright via
    /// the kill switch below — from App Service settings, with no redeploy and no DDL. The defaults
    /// reproduce the pre-configuration behaviour exactly.
    /// </remarks>
    public async Task RunAsync()
    {
        // THE KILL SWITCH. Checked here, before any statement runs, so a disabled reconcile writes
        // NOTHING rather than doing a pass that repairs nothing — the same shape as the auto-absent
        // sweep and the usage rollup. This job rewrites eight tables platform-wide, two of them the
        // hottest write paths here, so this is the one lever that stops it without a redeploy.
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Session-name reconcile is disabled (SessionNameReconcile__Enabled=false); nothing repaired.");
            return;
        }

        // The repository already clamps both to >= 1; the UPPER bounds are the point of clamping
        // here. A batch above SQL Server's ~5000-lock escalation threshold is the one setting that
        // turns this job into the table-lock-on-AttendanceRecords incident it was designed as a job
        // to avoid, and an unbounded ceiling would turn one nightly pass into a long-running write.
        // A typo in an App Service setting must not be able to reach either.
        int batchSize = Math.Clamp(_options.BatchSize, 1, 5000);
        int maxBatchesPerTable = Math.Clamp(_options.MaxBatchesPerTable, 1, 1000);

        int repaired = await _unitOfWork.SessionsRepo
            .ReconcileStoredSessionNamesAsync(batchSize, maxBatchesPerTable);

        if (repaired > 0)
        {
            _logger.LogInformation(
                "Session-name reconcile repaired {Rows} stored name(s). A run that hits the " +
                "per-table ceiling ({Ceiling} rows) resumes on the next pass.",
                repaired, batchSize * maxBatchesPerTable);
        }
    }
}
