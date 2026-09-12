namespace Edvanz.Application.Dtos.AdminInsights;

// ════════════════════════════════════════════════════════════════════════════
// ADMIN CONSOLE — WIRE CONTRACTS
// ════════════════════════════════════════════════════════════════════════════
//
// THE RULE EVERY SHAPE HERE OBEYS: a number arrives with the words that explain it and a key that
// opens the people inside it. A count nobody can drill into is a number to study, not one to act
// on, and the console exists to be acted on.
//
// Calendar days are DateOnly (wire "2026-09-12"); moments are DateTime and the global converter
// stamps them Z. See CLAUDE.md §11b.
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The landing page in ONE call: what happened yesterday, how the business is doing, whether
/// subscribers are actually using it, feature by feature, the money, and the platform footer.
/// </summary>
public class AdminDashboardDto
{
    public DateTime GeneratedAt { get; set; }

    /// <summary>7, 30 or 90 — governs every "newly" figure on the page.</summary>
    public int WindowDays { get; set; }

    /// <summary>
    /// The last day the usage figures are complete for. The rollup runs nightly, so this is
    /// yesterday; the page footer says so rather than letting a reader assume live numbers.
    /// </summary>
    public DateOnly ComputedThrough { get; set; }

    /// <summary>
    /// Oldest <c>ComputedAt</c> across all snapshots. If the nightly job stalled, this is how an
    /// admin finds out instead of quietly trusting stale figures.
    /// </summary>
    public DateTime? OldestSnapshotAt { get; set; }

    /// <summary>Teachers the rollup has never reached — reported as unknown, never as zeros.</summary>
    public int TeachersNotYetComputed { get; set; }

    /// <summary>
    /// What "ending soon" means on this console: seven days, computed off the end date. Shipped so
    /// the screen can label its own card without hardcoding a number that lives on the server — and
    /// so nobody is tempted to reach for the subscription module's five-day ExpiringSoon band,
    /// which would put two different "expiring" definitions on one page.
    /// </summary>
    public int EndingSoonThresholdDays { get; set; }

    public DashboardYesterdayDto Yesterday { get; set; } = new();
    public DashboardGrowthDto Growth { get; set; } = new();
    public DashboardUsageDto Usage { get; set; } = new();

    /// <summary>Per-feature, among the subscribers who HAVE that feature.</summary>
    public IReadOnlyList<DashboardFeatureRowDto> Features { get; set; } = Array.Empty<DashboardFeatureRowDto>();

    public DashboardMoneyDto Money { get; set; } = new();
    public DashboardPlatformDto Platform { get; set; } = new();
}

// ── A. Since yesterday ──────────────────────────────────────────────────────

/// <summary>The morning answer: what changed yesterday, with names already on the page.</summary>
public class DashboardYesterdayDto
{
    /// <summary>The teacher-local (Africa/Cairo) day these five lists describe.</summary>
    public DateOnly Date { get; set; }

    public DashboardNamedCountDto Registered { get; set; } = new();
    public DashboardNamedCountDto Subscribed { get; set; } = new();
    public DashboardNamedCountDto Expired { get; set; } = new();

    /// <summary>First-ever activity was yesterday. The wins.</summary>
    public DashboardNamedCountDto StartedUsing { get; set; } = new();

    public DashboardNamedCountDto UsedApp { get; set; } = new();
}

/// <summary>
/// A count that already carries its first few names. Anything past the cap lives behind
/// <see cref="SegmentKey"/> on the segments endpoint.
/// </summary>
public class DashboardNamedCountDto
{
    /// <summary>What to pass to <c>GET insights/segments/{key}</c> for the full list.</summary>
    public string SegmentKey { get; set; } = null!;

    public int Count { get; set; }

    /// <summary>The first few, capped. <see cref="Count"/> is always the true total.</summary>
    public IReadOnlyList<DashboardTeacherDto> Teachers { get; set; } = Array.Empty<DashboardTeacherDto>();
}

/// <summary>
/// A teacher as every card, list and search result shows them: enough to recognise, phone and open.
/// One shape, so "a call and a WhatsApp button on every teacher, everywhere" is built once.
/// </summary>
public class DashboardTeacherDto
{
    public long TeacherId { get; set; }

    /// <summary>The login user id — what the admin password-reset endpoint takes.</summary>
    public long UserId { get; set; }

    public string FullName { get; set; } = null!;
    public string TeacherCode { get; set; } = null!;
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// The two or three facts that explain why this teacher is in THIS card, in plain words —
    /// "subscribed 5 days ago · 0 students". Card-specific by design; a generic row would make
    /// every list look the same and none of them worth reading.
    /// </summary>
    public string? Evidence { get; set; }
}

// ── B. Growth & subscriptions ───────────────────────────────────────────────

/// <summary>How the business is doing. Every stock figure carries a delta over the window.</summary>
public class DashboardGrowthDto
{
    /// <summary>Independent teachers — centre-owned are counted separately, never folded in.</summary>
    public DashboardStatDto Teachers { get; set; } = new();

    public DashboardStatDto SubscribedNow { get; set; } = new();

    /// <summary>Subscribers split by plan: Full / Managerial / ManagerialPlus.</summary>
    public IReadOnlyList<BandCountDto> ByPlan { get; set; } = Array.Empty<BandCountDto>();

    public DashboardStatDto NewlyRegistered { get; set; } = new();
    public DashboardStatDto NewlySubscribed { get; set; } = new();

    /// <summary>
    /// Ending within 7 days, computed explicitly off the end date. The subscription module's own
    /// ExpiringSoon band is FIVE days; the console uses 7 everywhere and never shows both, so two
    /// "expiring" numbers can never disagree on screen.
    /// </summary>
    public DashboardStatDto EndingWithin7Days { get; set; } = new();

    /// <summary>Had a subscription, it ended, nothing replaced it.</summary>
    public DashboardStatDto Expired { get; set; } = new();

    /// <summary>Centre-owned teachers. Their money stays on the Centres page.</summary>
    public DashboardStatDto CenterTeachers { get; set; } = new();

    public IReadOnlyList<BandCountDto> CenterTeachersByPlan { get; set; } = Array.Empty<BandCountDto>();
}

/// <summary>A number, optionally a movement, and the key that opens the people inside it.</summary>
public class DashboardStatDto
{
    public int Count { get; set; }

    /// <summary>Null when the card has no list behind it (a total that is not a population).</summary>
    public string? SegmentKey { get; set; }

    /// <summary>Movement over the selected window. Null when a period-over-period figure would be a guess.</summary>
    public int? Delta { get; set; }
}

// ── C. Are subscribers using it ─────────────────────────────────────────────

/// <summary>
/// Everything in this group is counted over SUBSCRIBERS ONLY — the free and expired base
/// outnumbers them and made every figure read as a failure when the paying base was fine.
/// </summary>
public class DashboardUsageDto
{
    /// <summary>The denominator. Every pair below sums to it.</summary>
    public int Subscribers { get; set; }

    public DashboardStatDto UsingApp7 { get; set; } = new();
    public DashboardStatDto NotUsing7 { get; set; } = new();
    public DashboardStatDto UsingApp30 { get; set; } = new();
    public DashboardStatDto NotUsing30 { get; set; } = new();

    /// <summary>Subscribers with at least one student on the roster.</summary>
    public DashboardStatDto UploadedStudents { get; set; } = new();
    public DashboardStatDto NoStudents { get; set; } = new();

    /// <summary>
    /// Has students, none of them in a class. Those students open an empty app — the platform's
    /// most common silent misconfiguration, and the reason both student numbers are shown.
    /// </summary>
    public DashboardStatDto StudentsNotInClasses { get; set; } = new();

    /// <summary>Every student on a subscriber's roster.</summary>
    public int TotalStudents { get; set; }

    /// <summary>Of those, how many are actually assigned to a class. The gap is the story.</summary>
    public int TotalStudentsInClasses { get; set; }

    /// <summary>Backed by the call list, so this figure and that list can never disagree.</summary>
    public DashboardStatDto NeedsCall { get; set; } = new();
}

// ── D. Feature by feature ───────────────────────────────────────────────────

/// <summary>
/// One feature row: who has it, who uses it, who never opened it. Counted only over teachers
/// ENTITLED to it — a Managerial teacher is not "not using Videos", they simply do not have them.
/// </summary>
public class DashboardFeatureRowDto
{
    public string Feature { get; set; } = null!;

    /// <summary>Subscribers entitled to this feature. The denominator for the two below.</summary>
    public int HaveIt { get; set; }

    public int Using30 { get; set; }
    public int NeverOpened { get; set; }

    public string HaveItSegmentKey { get; set; } = null!;
    public string UsingSegmentKey { get; set; } = null!;
    public string NeverOpenedSegmentKey { get; set; } = null!;
}

// ── E. Money ────────────────────────────────────────────────────────────────

/// <summary>What the subscribed base is worth, whether it renews, and what is waiting to be approved.</summary>
public class DashboardMoneyDto
{
    /// <summary>
    /// Monthly value of every live independent subscription, priced through the SAME plan-aware
    /// calculator the teacher's own renewal screen uses.
    /// </summary>
    public decimal ActiveValueEGP { get; set; }

    /// <summary>
    /// Always "TodayPrices". Subscription rows DO carry an <c>AmountPaidEGP</c>, but every admin
    /// activation writes 0 into it and admin activation is the only live path — so a sum of the
    /// stored column would report almost nothing. <see cref="RecordedPaidEGP"/> reports that sum
    /// anyway, unmixed, so the difference stays visible instead of being quietly averaged away.
    /// </summary>
    public string ValueBasis { get; set; } = "TodayPrices";

    /// <summary>Sum of what the current subscription rows actually recorded as paid.</summary>
    public decimal RecordedPaidEGP { get; set; }

    /// <summary>The most recent completed renewal month; the full series is on /renewals.</summary>
    public RenewalMonthDto? LatestRenewals { get; set; }

    /// <summary>Teachers who have subscribed more than once — they converted off the free month.</summary>
    public DashboardStatDto RenewedAtLeastOnce { get; set; } = new();

    /// <summary>Teachers still on their first (granted) subscription.</summary>
    public DashboardStatDto FirstSubscriptionOnly { get; set; } = new();

    public DashboardPendingDto Pending { get; set; } = new();
}

/// <summary>Approvals waiting, with the money each queue represents.</summary>
public class DashboardPendingDto
{
    public int SubscriptionPayments { get; set; }
    public decimal SubscriptionPaymentsEGP { get; set; }
    public int SubscriptionRequests { get; set; }
    public decimal SubscriptionRequestsEGP { get; set; }

    /// <summary>No amount: an increase is granted, then billed on the next renewal at the new seat count.</summary>
    public int CapacityRequests { get; set; }

    public int CenterSubscriptionRequests { get; set; }
    public decimal CenterSubscriptionRequestsEGP { get; set; }
    public int TeacherIndependenceRequests { get; set; }

    /// <summary>Everything waiting, for the one badge that says whether anything needs doing.</summary>
    public int Total { get; set; }

    /// <summary>Every queue's money added up.</summary>
    public decimal TotalEGP { get; set; }
}

// ── G. Platform footer ──────────────────────────────────────────────────────

/// <summary>The small row of platform totals. Teachers reconcile: independent + centre-owned = all.</summary>
public class DashboardPlatformDto
{
    public int TeachersTotal { get; set; }
    public int TeachersIndependent { get; set; }
    public int TeachersCenterOwned { get; set; }
    public int Students { get; set; }

    /// <summary>Student app accounts that are Active AND bound — an unbound link sees nothing.</summary>
    public int LinkedStudentAccounts { get; set; }

    public int Assistants { get; set; }
    public int Centers { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
// TRENDS
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Registrations and new subscriptions over time, zero-filled, Cairo-local buckets.</summary>
public class AdminTrendsDto
{
    /// <summary>Weekly | Monthly.</summary>
    public string Granularity { get; set; } = null!;

    public IReadOnlyList<TrendPointDto> Points { get; set; } = Array.Empty<TrendPointDto>();
}

/// <summary>One bucket on the trend chart.</summary>
public class TrendPointDto
{
    /// <summary>First day of the bucket, teacher-local.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Ready-to-render label — "8 Sep" weekly, "Sep 2026" monthly.</summary>
    public string Label { get; set; } = null!;

    public int Registrations { get; set; }
    public int NewSubscriptions { get; set; }

    /// <summary>
    /// How many independent teachers held a live subscription at the END of the bucket,
    /// reconstructed from subscription spans rather than from a stored daily counter.
    ///
    /// For the bucket still in progress this is measured at NOW, not at its future end — otherwise
    /// it reports how many subscriptions will survive past next month rather than how many exist
    /// today, and a growing platform draws a chart that falls off at its right-hand edge.
    /// </summary>
    public int SubscribersAtEnd { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
// SEGMENTS — the list behind every card
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A grid row plus the one line explaining why this teacher is in this segment. Inherits the grid
/// item so a drill-down and the teachers table render from the same shape.
/// </summary>
public class AdminSegmentTeacherDto : TeacherUsageListItemDto
{
    /// <summary>Why they are here, in the segment's own terms.</summary>
    public string? Evidence { get; set; }

    /// <summary>
    /// Whole days until the subscription ends — negative once it has. Null when there is no
    /// subscription at all.
    ///
    /// RENDER THIS, NOT <c>subscriptionStatus</c>, wherever the screen talks about expiry. The
    /// status enum carries the subscription module's own five-day ExpiringSoon band, while this
    /// console counts "ending soon" at seven (<c>endingSoonThresholdDays</c> on the dashboard). A
    /// row ending in six days is "Active" by the enum and inside the console's card at the same
    /// time — two true statements that read as a contradiction side by side. A plain day count
    /// cannot contradict anything.
    /// </summary>
    public int? SubscriptionEndsInDays { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
// RENEWALS
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Did subscriptions that ended actually come back?</summary>
public class AdminRenewalsDto
{
    public IReadOnlyList<RenewalMonthDto> Months { get; set; } = Array.Empty<RenewalMonthDto>();

    /// <summary>Teachers still on their first (granted) subscription — the trial they never converted.</summary>
    public int FirstSubOnlyCount { get; set; }

    /// <summary>Teachers who have subscribed at least twice.</summary>
    public int RenewedAtLeastOnceCount { get; set; }

    /// <summary>
    /// How a renewal is judged, stated on the wire so the screen can explain its own number:
    /// a LATER subscription row starting within this many days of an earlier one's end.
    /// Extending a subscription mutates the same row and is deliberately not a renewal.
    /// </summary>
    public int RenewalWindowDays { get; set; }
}

/// <summary>One month of the renewal story.</summary>
public class RenewalMonthDto
{
    /// <summary>"2026-08" — stable, sortable, and what the segment keys take.</summary>
    public string Month { get; set; } = null!;

    /// <summary>"August 2026".</summary>
    public string Label { get; set; } = null!;

    /// <summary>
    /// TEACHERS whose subscription ended in this month — people, not periods. A teacher with two
    /// periods ending in one month is one person to call, and one row in the list this number
    /// opens; counting periods here would make the tile disagree with its own drill-down.
    /// </summary>
    public int Ended { get; set; }

    /// <summary>Of those, how many took out a new subscription. Always sums with Churned to Ended.</summary>
    public int Renewed { get; set; }

    public int Churned { get; set; }

    /// <summary>Renewed ÷ ended, 0-100. Zero when nothing ended.</summary>
    public int RatePercent { get; set; }

    public string RenewedSegmentKey { get; set; } = null!;
    public string ChurnedSegmentKey { get; set; } = null!;
}

// ════════════════════════════════════════════════════════════════════════════
// GLOBAL SEARCH
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Grouped top-few across the four things an admin is ever asked to find.</summary>
public class AdminSearchDto
{
    public string Query { get; set; } = null!;

    public IReadOnlyList<AdminSearchHitDto> Teachers { get; set; } = Array.Empty<AdminSearchHitDto>();

    /// <summary>Roster records a teacher created — NOT app accounts.</summary>
    public IReadOnlyList<AdminSearchHitDto> Students { get; set; } = Array.Empty<AdminSearchHitDto>();

    /// <summary>The student's own login on the platform.</summary>
    public IReadOnlyList<AdminSearchHitDto> StudentAccounts { get; set; } = Array.Empty<AdminSearchHitDto>();

    public IReadOnlyList<AdminSearchHitDto> Assistants { get; set; } = Array.Empty<AdminSearchHitDto>();

    /// <summary>Everything found, so an empty search says so once rather than four times.</summary>
    public int TotalHits { get; set; }
}

/// <summary>One search result, kind-agnostic.</summary>
public class AdminSearchHitDto
{
    /// <summary>Teacher | Student | StudentAccount | Assistant.</summary>
    public string Kind { get; set; } = null!;

    /// <summary>The row's own id — teacher, roster record, student account or assistant.</summary>
    public long Id { get; set; }

    /// <summary>The login user id where one exists. What password reset takes.</summary>
    public long? UserId { get; set; }

    public string FullName { get; set; } = null!;

    /// <summary>Teacher code, roster student code or student account code, as applicable.</summary>
    public string? Code { get; set; }

    public string? PhoneNumber { get; set; }

    /// <summary>The teacher this row belongs to, for navigation. Null for a student account.</summary>
    public long? TeacherId { get; set; }

    public string? TeacherName { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
// TEACHER PAGE — logins and the data snapshot
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Who signed in to this account and when — the first question on a support call.</summary>
public class AdminTeacherLoginsDto
{
    public long TeacherId { get; set; }

    public AdminLoginPersonDto Teacher { get; set; } = new();
    public IReadOnlyList<AdminLoginPersonDto> Assistants { get; set; } = Array.Empty<AdminLoginPersonDto>();

    /// <summary>
    /// True: teacher sign-ins are recorded. Kept on the wire because it used to be false — the
    /// platform wrote a per-login row for assistants only — and a client that reads it keeps
    /// working either way.
    /// </summary>
    public bool TeacherHistoryRecorded { get; set; }

    /// <summary>
    /// The oldest sign-in on record platform-wide. Null before anything has been recorded.
    ///
    /// READ THIS BEFORE RENDERING AN EMPTY EVENT LIST. Recording started at a deploy, so an account
    /// that has not signed in since shows nothing — which means "no record yet", not "never signed
    /// in". The two send a support call in opposite directions.
    /// </summary>
    public DateTime? RecordedSince { get; set; }
}

/// <summary>One person on the account with whatever sign-in history exists for them.</summary>
public class AdminLoginPersonDto
{
    public long UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Username { get; set; }

    /// <summary>Teacher | Assistant.</summary>
    public string Role { get; set; } = null!;

    /// <summary>False for a removed assistant, whose past work still counts but who cannot sign in.</summary>
    public bool IsActive { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastActivityAt { get; set; }

    /// <summary>Recorded sign-ins, newest first. Always empty for the teacher — see the flag above.</summary>
    public IReadOnlyList<AdminLoginEventDto> Events { get; set; } = Array.Empty<AdminLoginEventDto>();
}

/// <summary>One recorded sign-in or sign-out.</summary>
public class AdminLoginEventDto
{
    public string Action { get; set; } = null!;
    public DateTime OccurredAt { get; set; }
    public string? DeviceOrBrowser { get; set; }
    public string? IpAddress { get; set; }
}

/// <summary>
/// What the teacher's account actually CONTAINS, read-only. This is "see what they see" — the tab
/// that answers a support call about a misconfiguration without anyone guessing.
/// </summary>
public class AdminTeacherSnapshotDto
{
    public long TeacherId { get; set; }

    public IReadOnlyList<SnapshotClassDto> Classes { get; set; } = Array.Empty<SnapshotClassDto>();

    public SnapshotContentDto Videos { get; set; } = new();
    public SnapshotContentDto OnlineExams { get; set; } = new();

    /// <summary>Offline exams and homework — the assignment templates behind /api/exams.</summary>
    public SnapshotContentDto ExamsAndHomework { get; set; } = new();

    public SnapshotAccountsDto StudentAccounts { get; set; } = new();

    /// <summary>Roster size and how much of it is actually in a class.</summary>
    public int StudentCount { get; set; }
    public int StudentsInClasses { get; set; }
}

/// <summary>A class as the account holds it.</summary>
public class SnapshotClassDto
{
    public long SessionId { get; set; }
    public string SessionName { get; set; } = null!;
    public string? GroupName { get; set; }

    /// <summary>Class days as stored — the same string the app renders.</summary>
    public string? ScheduleDays { get; set; }

    /// <summary>Start time of day. A wall-clock time, so a TimeOnly on the wire (CLAUDE.md §11b).</summary>
    public TimeOnly StartTime { get; set; }

    public short DurationMinutes { get; set; }

    /// <summary>Calendar days, not moments.</summary>
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public int StudentCount { get; set; }

    /// <summary>Generated class days. Zero means nothing downstream of this class can work.</summary>
    public int OccurrenceCount { get; set; }
}

/// <summary>A content library reduced to how much there is and how recent it is.</summary>
public class SnapshotContentDto
{
    public int Total { get; set; }
    public DateTime? LatestAt { get; set; }

    /// <summary>The newest few titles — enough to recognise what the account is for.</summary>
    public IReadOnlyList<string> LatestTitles { get; set; } = Array.Empty<string>();
}

/// <summary>Student app accounts against this teacher.</summary>
public class SnapshotAccountsDto
{
    /// <summary>Connected accounts.</summary>
    public int Active { get; set; }

    /// <summary>Of those, bound to a roster record. An unbound account is connected and sees nothing.</summary>
    public int Bound { get; set; }
}
