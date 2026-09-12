using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

// ════════════════════════════════════════════════════════════════════════════
// ADMIN CONSOLE — REPOSITORY PROJECTION TYPES
// ════════════════════════════════════════════════════════════════════════════
//
// The dashboard, the trend chart, the per-card drill-downs and the global search. These sit
// alongside TeacherUsageProjections.cs and follow the same rule: repos return these, services map
// them onward. They are NOT DTOs.
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// One teacher reduced to everything the dashboard counts, and nothing else.
///
/// WHY ONE ROW PER TEACHER RATHER THAN TWENTY SQL COUNTS: the dashboard's headline promise is that
/// its numbers RECONCILE — independent + centre-owned equals every teacher, using plus not-using
/// equals the subscriber count, and each card's number equals the length of the list behind it.
/// Twenty independent aggregate queries cannot guarantee that; they drift the moment one of them
/// filters slightly differently (BUG-17 was exactly that bug, one list at a time). Counting a
/// single materialised population in memory makes every one of those identities true by
/// construction.
///
/// The row is kept deliberately small — a handful of scalars, no strings beyond identity — so the
/// cost stays linear and trivial: at the platform's present size this is a few hundred rows, and an
/// order of magnitude of growth still lands under a megabyte.
/// </summary>
public sealed record ConsoleTeacherRow(
    // ── Identity, enough to name and phone them without a second query ──
    long TeacherId,
    long UserId,
    string FullName,
    string TeacherCode,
    string? PhoneNumber,
    DateTime RegisteredAt,

    /// <summary>A centre-owned teacher. Excluded from every subscriber figure by decision — their
    /// commercial relationship is with the centre, and folding them in double-counts the money.</summary>
    bool IsCenterOwned,

    // ── Commercial ──
    SubscriptionStatus SubscriptionStatus,
    SubscriptionPlanType? PlanType,
    DateTime? SubscriptionStartDate,
    DateTime? SubscriptionEndDate,

    /// <summary>The seat count the Full plan is priced on.</summary>
    int LinkedStudentCapacity,

    /// <summary>What the CURRENT row actually recorded as paid. Zero on every admin activation.</summary>
    decimal RecordedAmountPaidEGP,

    /// <summary>How many subscription rows this teacher has ever had. 1 = still on their first.</summary>
    int SubscriptionCount,

    // ── Usage (from the nightly snapshot) ──
    int ActiveDays7,
    int ActiveDays30,
    int EntitledMask,
    int UsedMask30,
    int EverUsedMask,
    int StudentCount,
    int StudentsAssignedToSession,
    DateTime? FirstActivityAt,
    DateTime? LastActivityAt,

    /// <summary>Null when the rollup has never reached this teacher — "not computed yet", not zero.</summary>
    DateTime? ComputedAt);

/// <summary>Platform-wide totals that are not per-teacher: the small footer row of the dashboard.</summary>
public sealed record ConsolePlatformTotals(
    int Students,
    int LinkedStudentAccounts,
    int Assistants,
    int Centers);

/// <summary>
/// How many approvals are waiting, and how much money they represent. Surfaced on the dashboard AND
/// as sidebar badges, so the queues stop being places someone has to remember to visit.
/// </summary>
public sealed record ConsolePendingApprovals(
    int SubscriptionPayments,
    decimal SubscriptionPaymentsEGP,
    int SubscriptionRequests,
    decimal SubscriptionRequestsEGP,
    int CapacityRequests,
    int CenterSubscriptionRequests,
    decimal CenterSubscriptionRequestsEGP,
    int TeacherIndependenceRequests);

/// <summary>
/// One subscription row reduced to its two dates and its teacher. The whole renewal and trend
/// story is reconstructed from these: a renewal is a LATER row after an earlier one ended, which
/// is why <c>extend</c> and <c>set-end-date</c> — both of which mutate a row in place rather than
/// inserting one — correctly do not count as renewals.
/// </summary>
public sealed record ConsoleSubscriptionSpan(
    long TeacherId,
    DateTime StartDate,
    DateTime EndDate,

    /// <summary>
    /// Whether this is the teacher's current row RIGHT NOW. Historical liveness has to be
    /// reconstructed from the dates — the flag only describes today — but at today it is exact, and
    /// the platform gates on it: an admin can pre-date a subscription to start tomorrow, which
    /// expires the teacher immediately even though the period it replaced is still running.
    /// Reconstructing from dates alone counts that teacher as a subscriber on a morning they cannot
    /// open the app.
    /// </summary>
    bool IsCurrent);

/// <summary>
/// When each module was LAST actually written to, and how much, over all stored history.
///
/// "Does he really use it" is not answered by a tick. A teacher who opened Payments once in June
/// and a teacher who collects money every week both read as "has used Payments" — the date and the
/// count are what separate them, and they are the two things a yes/no mask cannot carry.
/// </summary>
public sealed record ConsoleModuleUsage(
    string Module,
    DateOnly? LastUsedOn,
    int Writes30,
    int WritesAllTime,
    int DaysUsedAllTime);

/// <summary>
/// One admin extension: days added to a period that was already running. Counted as a renewal in
/// the month it was GRANTED, because that is when the decision to keep the teacher was made.
/// </summary>
public sealed record ConsoleExtension(
    long TeacherId,
    DateTime GrantedAt,
    int DaysAdded,
    decimal? AmountPaidEGP);

/// <summary>One hit from the global search. Kind-agnostic so four searches share one row shape.</summary>
public sealed record ConsoleSearchHit(
    string Kind,
    long Id,
    long? UserId,
    string Title,
    string? Code,
    string? PhoneNumber,
    long? TeacherId,
    string? TeacherName);

/// <summary>One recorded sign-in or sign-out, for any account type.</summary>
public sealed record ConsoleLoginEvent(
    long Id,
    string Action,
    DateTime OccurredAt,
    string? DeviceOrBrowser,
    string? IpAddress);

/// <summary>A class as the teacher's account holds it, for the read-only data snapshot.</summary>
public sealed record ConsoleClassRow(
    long SessionId,
    string SessionName,
    string? GroupName,
    string? SelectedDays,
    TimeSpan StartTime,
    short DurationMinutes,
    DateTime StartDate,
    DateTime EndDate,
    int StudentCount,
    int OccurrenceCount);

/// <summary>A content library (videos, online exams, offline exams) reduced to "how much, how recent".</summary>
public sealed record ConsoleContentSummary(
    int Total,
    DateTime? LatestAt,
    IReadOnlyList<string> LatestTitles);

/// <summary>
/// Which card a drill-down is for. String on the wire; the two parameterised families
/// (<c>ModuleUsing:Videos</c>, <c>Renewed:2026-08</c>) carry their argument after the colon and are
/// parsed by <c>AdminSegmentKeyParser</c>.
/// </summary>
public enum AdminSegmentKey
{
    // ── Since yesterday ──
    RegisteredYesterday = 0,
    SubscribedYesterday = 1,
    ExpiredYesterday = 2,
    StartedUsingYesterday = 3,
    UsedYesterday = 4,

    // ── Growth, within the selected window ──
    NewlyRegistered = 10,
    NewlySubscribed = 11,
    EndingSoon = 12,
    Expired = 13,
    CenterTeachers = 14,
    SubscribedNow = 15,

    // ── Are subscribers using it ──
    UsingApp7 = 20,
    UsingApp30 = 21,
    NotUsing7 = 22,
    NotUsing30 = 23,
    NoStudents = 24,
    StudentsNotInClasses = 25,
    UploadedStudents = 26,
    NeedsCall = 27,

    // ── Per feature (parameterised: ModuleUsing:{module}) ──
    ModuleUsing = 30,
    ModuleNeverOpened = 31,
    ModuleHaveIt = 32,

    // ── Money (Renewed/NotRenewed parameterised by month: Renewed:2026-08) ──
    Renewed = 40,
    NotRenewed = 41,
    FirstSubOnly = 42,
    RenewedAtLeastOnce = 43
}
