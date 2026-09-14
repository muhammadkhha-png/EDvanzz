using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Logging;

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
    /// <summary>
    /// Rows per statement. Comfortably under SQL Server's ~5000-lock escalation threshold, so each
    /// batch takes row/page locks and releases them on autocommit.
    /// </summary>
    private const int BatchSize = 2000;

    /// <summary>
    /// Ceiling per table per run — 100k rows each, which clears any realistic backlog in one or two
    /// nights while keeping a single run bounded. A backlog larger than this resumes tomorrow.
    /// </summary>
    private const int MaxBatchesPerTable = 50;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SessionNameReconcileJob> _logger;

    public SessionNameReconcileJob(
        IUnitOfWork unitOfWork,
        ILogger<SessionNameReconcileJob> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Runs one bounded pass. Logs only when it actually repaired something — in steady state this
    /// job writes nothing and should stay silent.
    /// </summary>
    public async Task RunAsync()
    {
        int repaired = await _unitOfWork.SessionsRepo
            .ReconcileStoredSessionNamesAsync(BatchSize, MaxBatchesPerTable);

        if (repaired > 0)
        {
            _logger.LogInformation(
                "Session-name reconcile repaired {Rows} stored name(s). A run that hits the " +
                "per-table ceiling ({Ceiling} rows) resumes on the next pass.",
                repaired, BatchSize * MaxBatchesPerTable);
        }
    }
}
