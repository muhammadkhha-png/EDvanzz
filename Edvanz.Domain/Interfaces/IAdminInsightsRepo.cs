using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Read queries behind the admin insights screens: the usage grid, the overview aggregates, and the
/// named teacher lists on each insight card.
///
/// PERFORMANCE CONTRACT — the reason this repo exists at all. The legacy
/// <c>TeacherService.GetTeachersAsync</c> loads every teacher, user, subscription and subject into
/// memory and filters with LINQ-to-Objects. Nothing here may do that. Every method filters, sorts,
/// counts and pages IN SQL against the pre-computed <c>TeacherUsageSnapshots</c> table, which is
/// exactly why the nightly rollup exists.
/// </summary>
public interface IAdminInsightsRepo
{
    /// <summary>
    /// One page of the usage grid, already filtered and sorted in SQL, plus the total count.
    /// <paramref name="searchTerms"/> arrive pre-normalised (Arabic variants folded) from the service.
    /// </summary>
    Task<(IReadOnlyList<TeacherUsageRow> Rows, int TotalCount)> GetUsageGridAsync(
        AdminUsageFilter filter, CancellationToken ct = default);

    /// <summary>One teacher's usage row, or null when they do not exist.</summary>
    Task<TeacherUsageRow?> GetUsageRowAsync(long teacherId, CancellationToken ct = default);

    /// <summary>
    /// Daily totals for a set of teachers over a window, for the grid's inline sparklines. ONE query
    /// for the whole page — a per-row fetch would be an N+1 on every scroll.
    /// </summary>
    Task<IReadOnlyDictionary<long, IReadOnlyList<UsageDayTotals>>> GetDayTotalsForTeachersAsync(
        IReadOnlyCollection<long> teacherIds, DateOnly fromDate, DateOnly toDate,
        CancellationToken ct = default);

    /// <summary>Per-module write totals for one teacher over a window.</summary>
    Task<IReadOnlyList<TeacherUsageDay>> GetDaysAsync(
        long teacherId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

    /// <summary>The overview's headline counts and axis distributions, as SQL GROUP BYs.</summary>
    Task<AdminOverviewAggregates> GetOverviewAggregatesAsync(
        DateOnly today, CancellationToken ct = default);

    /// <summary>
    /// The named teachers behind one insight card. <paramref name="take"/> caps the preview; the
    /// returned total is the full count so the card can say "and 34 more".
    /// </summary>
    Task<(IReadOnlyList<TeacherUsageRow> Rows, int TotalCount)> GetInsightTeachersAsync(
        AdminInsightKind kind, DateOnly today, int take, CancellationToken ct = default);

    /// <summary>Note counts and latest note timestamp per teacher, for the grid. One query per page.</summary>
    Task<IReadOnlyDictionary<long, (int Count, DateTime LastAt)>> GetNoteStatsAsync(
        IReadOnlyCollection<long> teacherIds, CancellationToken ct = default);

    /// <summary>
    /// One teacher's live notes, already ordered pinned-first then newest. Soft-deleted notes are
    /// excluded by the entity's global query filter.
    /// </summary>
    Task<IReadOnlyList<AdminNote>> GetNotesForTeacherAsync(long teacherId, CancellationToken ct = default);

    /// <summary>
    /// Notes for MANY teachers in one query, for the CSV export. A per-teacher fetch across a few
    /// hundred exported rows would be a few hundred round trips for a single button press.
    /// </summary>
    Task<IReadOnlyDictionary<long, IReadOnlyList<AdminNote>>> GetNotesForTeachersAsync(
        IReadOnlyCollection<long> teacherIds, CancellationToken ct = default);

    /// <summary>The people who work an account — the teacher plus every assistant, removed included.</summary>
    Task<IReadOnlyList<TeacherOperatorRow>> GetOperatorsAsync(
        long teacherId, CancellationToken ct = default);

    /// <summary>
    /// The raw material for the landing page: one tiny row per teacher in scope (masks, plan,
    /// activity). Bit arithmetic happens in the service — SQL Server has no bitwise aggregate, and
    /// these rows are a handful of ints each.
    /// </summary>
    Task<IReadOnlyList<TeacherAdoptionRow>> GetAdoptionRowsAsync(
        bool subscribedOnly, CancellationToken ct = default);

    /// <summary>Every sales rep with their teachers' usage outcomes rolled up. One GROUP BY.</summary>
    Task<IReadOnlyList<SalesRepPerformanceRow>> GetSalesRepPerformanceAsync(
        bool includeInactive, CancellationToken ct = default);

    // ════════════════════════════════════════════════════════════════════════
    // THE CONSOLE — dashboard, trends, drill-downs, search, support lookups
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Every teacher on the platform, reduced to what the dashboard counts. ONE query; see
    /// <see cref="ConsoleTeacherRow"/> for why the dashboard materialises a population rather than
    /// issuing an aggregate per card.
    /// </summary>
    Task<IReadOnlyList<ConsoleTeacherRow>> GetConsoleTeacherRowsAsync(
        DateTime nowUtc, CancellationToken ct = default);

    /// <summary>
    /// Teachers who did something real on one teacher-local calendar day. Backs both "used the app
    /// yesterday" and the 7/30-day activity windows when they must agree with a named list.
    /// </summary>
    Task<IReadOnlySet<long>> GetTeacherIdsActiveOnAsync(
        DateOnly activityDate, CancellationToken ct = default);

    /// <summary>The platform footer: students, linked student accounts, assistants, centres.</summary>
    Task<ConsolePlatformTotals> GetPlatformTotalsAsync(CancellationToken ct = default);

    /// <summary>Everything waiting for an admin decision, with the money each queue represents.</summary>
    Task<ConsolePendingApprovals> GetPendingApprovalsAsync(CancellationToken ct = default);

    /// <summary>
    /// Subscription periods for INDEPENDENT teachers that overlap or start on/after
    /// <paramref name="fromUtc"/>, oldest first. The trend chart and the renewal maths are both
    /// reconstructed from these spans rather than from stored daily counters.
    /// </summary>
    Task<IReadOnlyList<ConsoleSubscriptionSpan>> GetSubscriptionSpansAsync(
        DateTime fromUtc, CancellationToken ct = default);

    // ── Global search. Every group folds Arabic variants through dbo.ArabicNormalize, so
    //    مصطفي finds مصطفى in a student list exactly as it already does on the teacher grid. ──

    /// <summary>Teachers matching name, username, teacher code or phone.</summary>
    Task<IReadOnlyList<ConsoleSearchHit>> SearchTeachersAsync(
        string normalizedTerm, int take, CancellationToken ct = default);

    /// <summary>Roster students (TeacherStudents) matching name, student code or either phone.</summary>
    Task<IReadOnlyList<ConsoleSearchHit>> SearchStudentsAsync(
        string normalizedTerm, int take, CancellationToken ct = default);

    /// <summary>Student APP accounts matching name, username, account code or phone.</summary>
    Task<IReadOnlyList<ConsoleSearchHit>> SearchStudentAccountsAsync(
        string normalizedTerm, int take, CancellationToken ct = default);

    /// <summary>Assistants matching name, username or phone, with the teacher they work for.</summary>
    Task<IReadOnlyList<ConsoleSearchHit>> SearchAssistantsAsync(
        string normalizedTerm, int take, CancellationToken ct = default);

    // ── Support lookups for the teacher page ──

    /// <summary>
    /// Recorded sign-ins per assistant user for one teacher's account, newest first.
    ///
    /// TEACHERS ARE ABSENT ON PURPOSE, and it is not an oversight in this query: the platform has
    /// never written a per-login row for a teacher account. <c>LoginActivityAssistantLog</c> is
    /// keyed on <c>AssistantId</c> and written only by the assistant sign-in path, so a teacher's
    /// history is exactly <c>User.LastLoginAt</c>. The read endpoint says so rather than rendering
    /// an empty list that reads as "never logged in".
    /// </summary>
    Task<IReadOnlyDictionary<long, IReadOnlyList<ConsoleLoginEvent>>> GetAssistantLoginEventsAsync(
        long teacherId, int takePerAssistant, CancellationToken ct = default);

    /// <summary>
    /// Grid rows for an explicit set of teachers — the page of a drill-down, hydrated through the
    /// SAME join the grid uses so a card's list and the table it links to describe one population.
    /// Order is not guaranteed; the caller re-imposes the segment's own relevance ordering.
    /// </summary>
    Task<IReadOnlyList<TeacherUsageRow>> GetUsageRowsByIdsAsync(
        IReadOnlyCollection<long> teacherIds, CancellationToken ct = default);

    /// <summary>The teacher's classes with schedule and per-class counts, for the data snapshot.</summary>
    Task<IReadOnlyList<ConsoleClassRow>> GetClassesForTeacherAsync(
        long teacherId, CancellationToken ct = default);

    /// <summary>
    /// LIVE roster and account counts for the data snapshot. Deliberately not the nightly
    /// snapshot's copies: this tab answers "what is in their account right now" during a support
    /// call, and a figure up to a day stale is exactly the kind of answer that sends someone
    /// looking for a bug that was fixed this morning.
    /// </summary>
    Task<(int Students, int StudentsInClasses, int ActiveLinks, int BoundLinks)> GetSnapshotCountsAsync(
        long teacherId, CancellationToken ct = default);

    /// <summary>Video library size and the most recent few titles.</summary>
    Task<ConsoleContentSummary> GetVideoSummaryAsync(
        long teacherId, int takeTitles, CancellationToken ct = default);

    /// <summary>Online-exam count and the most recent few titles.</summary>
    Task<ConsoleContentSummary> GetOnlineExamSummaryAsync(
        long teacherId, int takeTitles, CancellationToken ct = default);

    /// <summary>Offline exam/homework template count and the most recent few names.</summary>
    Task<ConsoleContentSummary> GetAssignmentSummaryAsync(
        long teacherId, int takeTitles, CancellationToken ct = default);
}

/// <summary>The named insight lists on the overview. Each is a question someone can act on today.</summary>
public enum AdminInsightKind
{
    /// <summary>Was active, then went silent. The churn-risk list.</summary>
    WentQuiet = 0,

    /// <summary>Assistants are working the account but the teacher themself is not.</summary>
    AssistantOnly = 1,

    /// <summary>Registered long enough ago to have started, and has nothing that works.</summary>
    NeverStarted = 2,

    /// <summary>Properly set up, but nobody has touched it in 30 days.</summary>
    SetUpNotRunning = 3,

    /// <summary>Using exactly one module. Onboarding or upsell.</summary>
    SingleModule = 4,

    /// <summary>Has students, none assigned to a session — those students see an empty app.</summary>
    SessionLessRoster = 5,

    /// <summary>First ever activity within the last week. The wins.</summary>
    NewlyLive = 6,

    /// <summary>Active teachers whose subscription has expired or is about to.</summary>
    ExpiringWhileActive = 7,

    /// <summary>
    /// Running fine, but entitled to features they have NEVER opened. The adoption gap — needs the
    /// teacher's own entitlement, so it is the one kind that cannot be derived from usage alone.
    /// </summary>
    UnusedEntitlements = 9,

    /// <summary>
    /// Subscribed recently. Ordered so the ones who have NOT started yet come first — a teacher who has
    /// just handed over money and cannot get going is the most urgent call on the platform, and the
    /// easiest refund request to avoid.
    /// </summary>
    NewlySubscribed = 8
}

/// <summary>Everything the grid can filter and sort by. All filters optional; they compose.</summary>
public sealed class AdminUsageFilter
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Arabic-normalised search term, or null. Matched against equally-normalised columns.</summary>
    public string? NormalizedSearch { get; set; }

    public UsageCadence? Cadence { get; set; }
    public UsageDepth? Depth { get; set; }
    public OperatorMix? Operators { get; set; }

    /// <summary>Bit of <see cref="UsageModules"/> that must be set in the 30-day mask.</summary>
    public int? UsingModuleMask { get; set; }

    public bool? HasRealData { get; set; }
    public long? SalesRepId { get; set; }
    public bool? UnassignedSalesRep { get; set; }
    public SubscriptionStatus? SubscriptionStatus { get; set; }
    public DateTime? RegisteredFrom { get; set; }
    public DateTime? RegisteredToExclusive { get; set; }

    /// <summary>
    /// Only teachers whose CURRENT subscription started within this many days. Mirrors the existing
    /// `subscribedWithinDays` on the teacher list, so "newly subscribed" means the same thing on
    /// both screens. When set, it also forces newest-subscription-first ordering.
    /// </summary>
    public int? SubscribedWithinDays { get; set; }

    /// <summary>True = did something in the last 30 days; false = did nothing at all.</summary>
    public bool? IsActive { get; set; }

    /// <summary>Only subscribers (Active or ExpiringSoon) — the paying base.</summary>
    public bool? SubscribedOnly { get; set; }

    /// <summary>
    /// Entitled to this feature and never once opened it. The adoption gap, as a filter — this is
    /// what every number on the Numbers page links to.
    /// </summary>
    public int? NeverUsedFeatureMask { get; set; }

    /// <summary>Column to order by, as a stable token the repo maps to an expression.</summary>
    public string SortBy { get; set; } = "LastActivity";
    public bool Descending { get; set; } = true;
}

/// <summary>
/// A teacher's usage row as read from SQL: the snapshot joined to the identity and commercial columns
/// the grid shows. Null snapshot fields mean the rollup has not reached this teacher yet.
/// </summary>
public sealed class TeacherUsageRow
{
    public long TeacherId { get; set; }
    public long UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Username { get; set; }
    public string TeacherCode { get; set; } = null!;
    public string? PhoneNumber { get; set; }

    /// <summary>Contact email. Carried for the CSV export — a rep needs a second way to reach
    /// someone when the phone number is dead.</summary>
    public string? Email { get; set; }

    public DateTime RegisteredAt { get; set; }
    public AccountStatus AccountStatus { get; set; }

    /// <summary>
    /// True when the teacher belongs to a centre. The console excludes them from every subscriber
    /// figure — their money is the centre's, counted once on the Centres page — and gives them
    /// their own card, so "independent + centre-owned = every teacher" always holds.
    /// </summary>
    public bool IsCenterOwned { get; set; }

    public SubscriptionStatus? SubscriptionStatus { get; set; }

    /// <summary>When the CURRENT subscription started. Drives the "just subscribed" list — a
    /// teacher who has paid and not yet started is the highest-priority onboarding call there is.</summary>
    public DateTime? SubscriptionStartDate { get; set; }

    public DateTime? SubscriptionEndDate { get; set; }

    /// <summary>Full / Managerial / ManagerialPlus on the current subscription.</summary>
    public SubscriptionPlanType? PlanType { get; set; }
    public long? SalesRepId { get; set; }
    public string? SalesRepName { get; set; }
    public string? AcquisitionSource { get; set; }

    public UsageCadence Cadence { get; set; }
    public UsageDepth Depth { get; set; }
    public OperatorMix Operators { get; set; }
    public int ActiveDays7 { get; set; }
    public int ActiveDays30 { get; set; }
    public int ActiveDays90 { get; set; }
    public int TotalWrites30 { get; set; }
    public int ModulesUsedMask { get; set; }
    public int ModulesUsedAllTimeMask { get; set; }

    /// <summary>What they are ENTITLED to — grants plus the plan-derived parent portal. The gap
    /// against <see cref="ModulesUsedAllTimeMask"/> is what they pay for and never opened.</summary>
    public int EntitledModulesMask { get; set; }

    public DateTime? FirstActivityAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public DateTime? LastTeacherActivityAt { get; set; }
    public DateTime? LastAssistantActivityAt { get; set; }
    public int ActiveAssistantCount { get; set; }

    public int StudentCount { get; set; }
    public int StudentsAssignedToSession { get; set; }
    public int SessionCount { get; set; }
    public int SessionsWithOccurrences { get; set; }
    public int LinkedAccountCount { get; set; }
    public int BoundAccountCount { get; set; }
    public bool HasEverMarkedAttendance { get; set; }
    public bool HasEverCollectedPayment { get; set; }
    public bool HasRealData { get; set; }

    public DateTime? ComputedAt { get; set; }
}

/// <summary>The overview's pre-aggregated counts, produced by a handful of GROUP BYs.</summary>
public sealed record AdminOverviewAggregates(
    int TotalTeachers,
    int Live,
    int LivePrevious,
    int Dormant,
    int NeverStarted,
    int WithRealData,
    int AssistantOnly,
    DateTime? OldestSnapshotAt,
    IReadOnlyDictionary<UsageCadence, int> ByCadence,
    IReadOnlyDictionary<UsageDepth, int> ByDepth,
    IReadOnlyDictionary<OperatorMix, int> ByOperator,
    IReadOnlyDictionary<UsageModules, int> ModuleAdoption);

/// <summary>One teacher reduced to what the adoption maths needs. Deliberately tiny.</summary>
public sealed record TeacherAdoptionRow(
    long TeacherId,
    SubscriptionPlanType? PlanType,
    bool IsSubscribed,
    int ActiveDays30,
    bool HasRealData,
    int EntitledMask,
    int UsedMask30,
    int EverUsedMask);

/// <summary>One person who works a teacher's account.</summary>
public sealed record TeacherOperatorRow(
    long UserId,
    string FullName,
    string? Username,
    string Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime? LastActivityAt);

/// <summary>A sales rep with their book of accounts rolled up by outcome.</summary>
public sealed record SalesRepPerformanceRow(
    long Id,
    string Name,
    string? PhoneNumber,
    bool IsActive,
    DateTime CreatedAt,
    int TeachersAssigned,
    int TeachersLive,
    int TeachersDormant,
    int TeachersNeverStarted,
    int TeachersWithRealData);
