namespace Edvanz.Domain.Constants;

/// <summary>
/// Every tunable number behind the admin usage model, in ONE place. The band thresholds are judgement
/// calls, not facts — expect to move them once real data is in — so no service may hardcode its own.
/// </summary>
public static class AdminInsightsConstants
{
    // ── Hangfire ───────────────────────────────────────────────────────────────

    /// <summary>Recurring job id for the nightly usage rollup.</summary>
    public const string UsageRollupJobId = "teacher-usage-rollup";

    /// <summary>Queue the per-teacher rollup workers run on. Kept off the notification queues so a slow
    /// backfill can never delay a subscription reminder or a payment-rejection message.</summary>
    public const string UsageRollupQueue = "usage-rollup";

    /// <summary>
    /// Default schedule: 03:15 Africa/Cairo. Deliberately AFTER the 02:30 auto-absent sweep so that
    /// night's system rows have settled — the rollup excludes them either way, but running second keeps
    /// the two jobs off the same connection-pool spike.
    /// </summary>
    public const string DefaultCronExpression = "15 3 * * *";

    // ── Rollup windows ─────────────────────────────────────────────────────────

    /// <summary>
    /// How many recent days each nightly run recomputes, not just yesterday. Offline attendance and
    /// payment syncs arrive days late (CLAUDE.md §7.2c), so a past day's counts legitimately change
    /// after the fact; anything inside this tail self-heals on the next run.
    /// </summary>
    public const int RecomputeTailDays = 7;

    /// <summary>How far back the FIRST run reaches when building history from existing timestamps.</summary>
    public const int BackfillDays = 180;

    // ── Axis 1: cadence bands (active days within the last 30) ─────────────────

    /// <summary>20+ active days in 30 ⇒ Daily.</summary>
    public const int CadenceDailyMinDays = 20;

    /// <summary>10-19 ⇒ MostDays.</summary>
    public const int CadenceMostDaysMinDays = 10;

    /// <summary>4-9 ⇒ Weekly.</summary>
    public const int CadenceWeeklyMinDays = 4;

    /// <summary>1-3 ⇒ Rarely. Below this, 0 days means Dormant (or Never, if there is no history).</summary>
    public const int CadenceRarelyMinDays = 1;

    // ── Axis 2: depth bands (distinct modules used) ────────────────────────────

    /// <summary>6+ modules ⇒ Full.</summary>
    public const int DepthFullMinModules = 6;

    /// <summary>4-5 ⇒ Broad.</summary>
    public const int DepthBroadMinModules = 4;

    /// <summary>2-3 ⇒ Core. Exactly 1 ⇒ Single.</summary>
    public const int DepthCoreMinModules = 2;

    // ── Insight-card thresholds ────────────────────────────────────────────────

    /// <summary>Silence (in days) after real activity before a teacher counts as "went quiet".</summary>
    public const int WentQuietAfterDays = 14;

    /// <summary>Grace period after registration before an empty account counts as "never started" —
    /// nobody sets up their whole term on day one.</summary>
    public const int NeverStartedGraceDays = 7;

    /// <summary>Window in which a first-ever activity counts as "newly live".</summary>
    public const int NewlyLiveWithinDays = 7;

    /// <summary>
    /// Window in which a subscription start counts as "newly subscribed". 30 days rather than 7:
    /// onboarding a teacher takes weeks, not days, and a signup that has still not started after a
    /// fortnight is exactly the one worth calling.
    /// </summary>
    public const int NewlySubscribedWithinDays = 30;

    /// <summary>How many named teachers each insight card carries inline; the rest sit behind "see all".
    /// Counts alone are not actionable — every card names names.</summary>
    public const int InsightCardPreviewSize = 5;

    /// <summary>
    /// Hard cap on rows in one CSV export. Generous enough to cover the whole platform many times
    /// over, but bounded so a mis-filtered export can never build an unbounded string in memory on
    /// a small App Service plan.
    /// </summary>
    public const int CsvExportMaxRows = 5000;

    // ── The console ────────────────────────────────────────────────────────────

    /// <summary>The windows the dashboard's "newly" figures may be asked for.</summary>
    public static readonly int[] ConsoleWindowDays = { 7, 30, 90 };

    /// <summary>Default window. A week is the question someone actually asks each morning.</summary>
    public const int ConsoleDefaultWindowDays = 7;

    /// <summary>
    /// "Ending soon" on the console means SEVEN days, computed off the end date — deliberately not
    /// the subscription module's own five-day ExpiringSoon band. One definition, used everywhere on
    /// these screens, so two "expiring" numbers can never sit side by side disagreeing.
    /// </summary>
    public const int ConsoleEndingSoonDays = 7;

    /// <summary>How many names each "since yesterday" card carries inline before "+N more".</summary>
    public const int ConsoleYesterdayPreviewSize = 8;

    /// <summary>Buckets on the trend chart — 12 weeks or 12 months.</summary>
    public const int ConsoleTrendBuckets = 12;

    /// <summary>Months of renewal history the money section reports by default.</summary>
    public const int ConsoleRenewalMonths = 6;

    /// <summary>
    /// How long after a subscription ends a NEW subscription row still counts as a renewal. A row
    /// is the unit: <c>activate</c> inserts one, while <c>extend</c> and <c>set-end-date</c> mutate
    /// the existing row in place and are correctly not renewals.
    /// </summary>
    public const int ConsoleRenewalWindowDays = 30;

    /// <summary>Results per group in the global search dropdown.</summary>
    public const int ConsoleSearchTakePerGroup = 5;

    /// <summary>Ceiling on results per group when a caller asks for more.</summary>
    public const int ConsoleSearchMaxTakePerGroup = 25;

    /// <summary>Shortest query the global search will run. One letter matches the platform.</summary>
    public const int ConsoleSearchMinLength = 2;

    /// <summary>Recorded sign-ins kept per assistant on the teacher page.</summary>
    public const int ConsoleLoginEventsPerPerson = 25;

    /// <summary>Newest titles carried per content library on the data-snapshot tab.</summary>
    public const int ConsoleSnapshotTitles = 5;

    /// <summary>
    /// Ceiling on the call list. It used to be 200, which was fine while the list was only ever a
    /// screen someone worked from the top; the "needs a call" drill-down pages through the WHOLE
    /// list, and a card claiming 240 that can only open 200 of them is the same count-vs-list
    /// mismatch as BUG-17.
    /// </summary>
    public const int CallListMaxTake = 500;
}
