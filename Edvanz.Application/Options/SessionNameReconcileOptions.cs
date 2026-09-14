namespace Edvanz.Application.Options;

/// <summary>
/// Configuration for the nightly session-name reconcile (SessionNameReconcileJob →
/// ISessionRepo.ReconcileStoredSessionNamesAsync). Bound from the appsettings.json section
/// "SessionNameReconcile" so behaviour can be tuned WITHOUT a code change — locally via
/// appsettings.json, on Azure App Service via application settings
/// "SessionNameReconcile__Enabled" / "SessionNameReconcile__BatchSize" etc. (a save restarts the
/// app; no redeploy, no DDL). Defaults below apply when the section, or any single key, is absent,
/// and reproduce the pre-configuration behaviour exactly.
///
/// WHY EACH KNOB EXISTS:
/// - <see cref="Enabled"/> is the kill switch. This job rewrites eight tables platform-wide —
///   including <c>AttendanceRecords</c> and <c>PaymentPeriods</c>, the two hottest write paths on
///   the platform — and it is scheduled to keep doing so every night forever. Being able to stop it
///   instantly, without unscheduling or redeploying, is the difference between a five-second fix and
///   an incident. Mirrors AutoAbsentOptions.Enabled and AdminInsightsOptions.Enabled.
/// - <see cref="BatchSize"/> is the rows-per-statement bound. Keep it comfortably under SQL Server's
///   ~5000-lock escalation threshold so each statement takes row/page locks and releases them on
///   autocommit; raise it past that and one batch escalates to a TABLE lock on AttendanceRecords,
///   blocking every attendance write until it commits. Lower it if the database tier proves too
///   small for the default; the only cost is more statements per run.
/// - <see cref="MaxBatchesPerTable"/> bounds ONE run, so a huge backlog is spread over several
///   nights instead of turning a single pass into a long-running write. A run that hits the ceiling
///   simply resumes on the next pass.
/// - <see cref="CronExpression"/> schedules the recurring job, evaluated in Africa/Cairo.
///
/// NOTE: nothing here changes what the job DOES — every statement is still guarded on the name
/// actually differing (§6.4), so a steady-state run writes nothing regardless of these values.
/// </summary>
public class SessionNameReconcileOptions
{
    public const string Section = "SessionNameReconcile";

    /// <summary>Master on/off switch for the nightly reconcile. Default true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Rows per statement. Default 2000 — comfortably under SQL Server's ~5000-lock escalation
    /// threshold, so each batch takes row/page locks and releases them on autocommit. Clamped to
    /// [1, 5000] at use — above that threshold one batch escalates to a table lock.
    /// </summary>
    public int BatchSize { get; set; } = 2000;

    /// <summary>
    /// Ceiling per table per run. Default 50 — 100k rows each at the default batch size, which
    /// clears any realistic backlog in one or two nights while keeping a single run bounded. A
    /// backlog larger than this resumes tomorrow. Clamped to [1, 1000] at use.
    /// </summary>
    public int MaxBatchesPerTable { get; set; } = 50;

    /// <summary>
    /// Cron for the recurring job, evaluated in Africa/Cairo. Default "30 3 * * *" (03:30) — after
    /// the 02:30 auto-absent sweep and the 03:00 recycle-bin purge, well clear of any class.
    /// </summary>
    public string CronExpression { get; set; } = "30 3 * * *";
}
