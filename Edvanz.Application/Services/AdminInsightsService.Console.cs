using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AdminInsights;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Helpers;
using Edvanz.Domain.Interfaces;
using System.Globalization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// The admin CONSOLE half of the insights service: the dashboard, the trend chart, the drill-down
/// behind every number, the renewal story, global search, and the two support lookups on a
/// teacher's page.
///
/// THE ONE STRUCTURAL IDEA HERE: every card's COUNT and the LIST behind it come from the same
/// predicate, evaluated over the same materialised population (<see cref="SegmentPredicate"/>).
/// A card that says 162 and opens 161 people is not a rounding error — it is a paging bug and a
/// credibility problem, and it is exactly what happens when a count is written once for the tile
/// and again for the list (BUG-17). Here it cannot: the tile literally counts the list.
/// </summary>
public partial class AdminInsightsService
{
    // ════════════════════════════════════════════════════════════════════════
    // THE DASHBOARD
    // ════════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<AdminDashboardDto>> GetDashboardAsync(int windowDays)
    {
        int window = NormalizeWindow(windowDays);
        var ctx = await BuildContextAsync(window);
        var rows = ctx.Rows;

        var independent = rows.Where(r => !r.IsCenterOwned).ToList();
        var subscribers = independent.Where(IsSubscribed).ToList();

        var rates = await _unitOfWork.SubscriptionPricingRepo.GetRatesAsync();
        var platform = await _unitOfWork.AdminInsightsRepo.GetPlatformTotalsAsync();
        var pending = await _unitOfWork.AdminInsightsRepo.GetPendingApprovalsAsync();

        var dto = new AdminDashboardDto
        {
            GeneratedAt = DateTime.UtcNow,
            WindowDays = window,
            // The rollup runs overnight, so the usage half of this page is complete through
            // yesterday and no further. Saying so is cheaper than explaining it later.
            ComputedThrough = ctx.Yesterday,
            OldestSnapshotAt = rows.Where(r => r.ComputedAt is not null).Min(r => r.ComputedAt),
            TeachersNotYetComputed = rows.Count(r => r.ComputedAt is null),

            Yesterday = new DashboardYesterdayDto
            {
                Date = ctx.Yesterday,
                Registered = NamedCount(ctx, AdminSegmentKey.RegisteredYesterday),
                Subscribed = NamedCount(ctx, AdminSegmentKey.SubscribedYesterday),
                Expired = NamedCount(ctx, AdminSegmentKey.ExpiredYesterday),
                StartedUsing = NamedCount(ctx, AdminSegmentKey.StartedUsingYesterday),
                UsedApp = NamedCount(ctx, AdminSegmentKey.UsedYesterday)
            },

            Growth = new DashboardGrowthDto
            {
                // No segment key: "every independent teacher" is the page's scope, not a card.
                Teachers = new DashboardStatDto
                {
                    Count = independent.Count,
                    Delta = independent.Count(r => r.RegisteredAt >= ctx.WindowStartUtc)
                },
                SubscribedNow = Stat(ctx, AdminSegmentKey.SubscribedNow,
                    delta: subscribers.Count(r => r.SubscriptionStartDate >= ctx.WindowStartUtc)),
                ByPlan = PlanBreakdown(subscribers),
                NewlyRegistered = Stat(ctx, AdminSegmentKey.NewlyRegistered),
                NewlySubscribed = Stat(ctx, AdminSegmentKey.NewlySubscribed),
                EndingWithin7Days = Stat(ctx, AdminSegmentKey.EndingSoon),
                Expired = Stat(ctx, AdminSegmentKey.Expired),
                CenterTeachers = Stat(ctx, AdminSegmentKey.CenterTeachers),
                CenterTeachersByPlan = PlanBreakdown(rows.Where(r => r.IsCenterOwned))
            },

            Usage = new DashboardUsageDto
            {
                Subscribers = subscribers.Count,
                UsingApp7 = Stat(ctx, AdminSegmentKey.UsingApp7),
                NotUsing7 = Stat(ctx, AdminSegmentKey.NotUsing7),
                UsingApp30 = Stat(ctx, AdminSegmentKey.UsingApp30),
                NotUsing30 = Stat(ctx, AdminSegmentKey.NotUsing30),
                UploadedStudents = Stat(ctx, AdminSegmentKey.UploadedStudents),
                NoStudents = Stat(ctx, AdminSegmentKey.NoStudents),
                StudentsNotInClasses = Stat(ctx, AdminSegmentKey.StudentsNotInClasses),
                // Both numbers, always: the gap between them is the story the pair exists to tell.
                TotalStudents = subscribers.Sum(r => r.StudentCount),
                TotalStudentsInClasses = subscribers.Sum(r => r.StudentsAssignedToSession),
                NeedsCall = Stat(ctx, AdminSegmentKey.NeedsCall)
            },

            Features = BuildFeatureRows(ctx, subscribers),

            Money = new DashboardMoneyDto
            {
                ActiveValueEGP = subscribers.Sum(r => SubscriptionPricing.MonthlyValueEGP(
                    r.PlanType, r.LinkedStudentCapacity,
                    rates.PerStudentEGP, rates.ManagerialMonthlyEGP, rates.ManagerialPlusMonthlyEGP)),
                ValueBasis = "TodayPrices",
                RecordedPaidEGP = subscribers.Sum(r => r.RecordedAmountPaidEGP),
                LatestRenewals = ctx.Renewals.Months.LastOrDefault(),
                RenewedAtLeastOnce = Stat(ctx, AdminSegmentKey.RenewedAtLeastOnce),
                FirstSubscriptionOnly = Stat(ctx, AdminSegmentKey.FirstSubOnly),
                Pending = new DashboardPendingDto
                {
                    SubscriptionPayments = pending.SubscriptionPayments,
                    SubscriptionPaymentsEGP = pending.SubscriptionPaymentsEGP,
                    SubscriptionRequests = pending.SubscriptionRequests,
                    SubscriptionRequestsEGP = pending.SubscriptionRequestsEGP,
                    CapacityRequests = pending.CapacityRequests,
                    CenterSubscriptionRequests = pending.CenterSubscriptionRequests,
                    CenterSubscriptionRequestsEGP = pending.CenterSubscriptionRequestsEGP,
                    TeacherIndependenceRequests = pending.TeacherIndependenceRequests,
                    Total = pending.SubscriptionPayments + pending.SubscriptionRequests
                          + pending.CapacityRequests + pending.CenterSubscriptionRequests
                          + pending.TeacherIndependenceRequests,
                    TotalEGP = pending.SubscriptionPaymentsEGP + pending.SubscriptionRequestsEGP
                             + pending.CenterSubscriptionRequestsEGP
                }
            },

            Platform = new DashboardPlatformDto
            {
                // These three reconcile by construction — the same list, split on one flag.
                TeachersTotal = rows.Count,
                TeachersIndependent = independent.Count,
                TeachersCenterOwned = rows.Count - independent.Count,
                Students = platform.Students,
                LinkedStudentAccounts = platform.LinkedStudentAccounts,
                Assistants = platform.Assistants,
                Centers = platform.Centers
            }
        };

        return Result<AdminDashboardDto>.Success(dto, _localizer);
    }

    // ════════════════════════════════════════════════════════════════════════
    // TRENDS
    // ════════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<AdminTrendsDto>> GetTrendsAsync(string? granularity)
    {
        bool monthly = string.Equals(granularity, "Monthly", StringComparison.OrdinalIgnoreCase);
        DateOnly today = LocalToday();

        // Bucket bounds first, so the spans query knows exactly how far back it must reach.
        var buckets = monthly ? MonthBuckets(today) : WeekBuckets(today);
        DateTime fromUtc = LocalDayStartUtc(buckets[0].Start);

        var rows = await _unitOfWork.AdminInsightsRepo.GetConsoleTeacherRowsAsync(DateTime.UtcNow);
        var spans = await _unitOfWork.AdminInsightsRepo.GetSubscriptionSpansAsync(fromUtc);

        // Registrations are counted over INDEPENDENT teachers only, matching every other figure on
        // the console — a centre onboarding twelve teachers at once would otherwise read as a
        // twelve-teacher week that no sales rep produced.
        var registrations = rows
            .Where(r => !r.IsCenterOwned)
            .Select(r => DateOnly.FromDateTime(_timeZone.ConvertUtcToLocal(r.RegisteredAt)))
            .ToList();

        DateTime nowUtc = DateTime.UtcNow;
        var points = new List<TrendPointDto>(buckets.Count);

        foreach (var (start, endExclusive) in buckets)
        {
            // CLAMPED TO NOW for the bucket still in progress. Its end is a FUTURE instant, and
            // subscriptions run 30 days — so measuring "who held one at period end" there counts
            // how many survive until October rather than how many exist today, and the last column
            // of a growing chart slumps. September read 27 against August's 41 for exactly that
            // reason, on a platform that had just gained subscribers.
            DateTime endUtc = LocalDayStartUtc(endExclusive);
            DateTime measureAtUtc = endUtc > nowUtc ? nowUtc : endUtc;

            points.Add(new TrendPointDto
            {
                PeriodStart = start,
                Label = monthly
                    ? start.ToString("MMM yyyy", CultureInfo.InvariantCulture)
                    : start.ToString("d MMM", CultureInfo.InvariantCulture),
                Registrations = registrations.Count(d => d >= start && d < endExclusive),
                NewSubscriptions = spans.Count(s =>
                {
                    var local = DateOnly.FromDateTime(_timeZone.ConvertUtcToLocal(s.StartDate));
                    return local >= start && local < endExclusive;
                }),
                // Who held a live subscription at the last moment of the bucket. Reconstructed from
                // the spans rather than stored daily — a stored counter would need a job, and a job
                // that stops leaves a chart that lies rather than one that stops.
                SubscribersAtEnd = spans
                    .Where(s => s.StartDate < measureAtUtc && s.EndDate >= measureAtUtc)
                    .Select(s => s.TeacherId)
                    .Distinct()
                    .Count()
            });
        }

        return Result<AdminTrendsDto>.Success(new AdminTrendsDto
        {
            Granularity = monthly ? "Monthly" : "Weekly",
            Points = points
        }, _localizer);
    }

    /// <summary>
    /// The last 12 weeks, each starting on a SATURDAY — the Egyptian week, the same one the class
    /// schedule uses (day index 0 = Saturday). A Monday-start chart would cut every week across the
    /// weekend a teacher actually works.
    /// </summary>
    private static List<(DateOnly Start, DateOnly EndExclusive)> WeekBuckets(DateOnly today)
    {
        int sinceSaturday = ((int)today.DayOfWeek + 1) % 7;
        DateOnly thisWeek = today.AddDays(-sinceSaturday);

        var buckets = new List<(DateOnly, DateOnly)>(AdminInsightsConstants.ConsoleTrendBuckets);
        for (int i = AdminInsightsConstants.ConsoleTrendBuckets - 1; i >= 0; i--)
        {
            DateOnly start = thisWeek.AddDays(-7 * i);
            buckets.Add((start, start.AddDays(7)));
        }
        return buckets;
    }

    /// <summary>The last 12 calendar months, ending with the one in progress.</summary>
    private static List<(DateOnly Start, DateOnly EndExclusive)> MonthBuckets(DateOnly today)
    {
        var thisMonth = new DateOnly(today.Year, today.Month, 1);

        var buckets = new List<(DateOnly, DateOnly)>(AdminInsightsConstants.ConsoleTrendBuckets);
        for (int i = AdminInsightsConstants.ConsoleTrendBuckets - 1; i >= 0; i--)
        {
            DateOnly start = thisMonth.AddMonths(-i);
            buckets.Add((start, start.AddMonths(1)));
        }
        return buckets;
    }

    // ════════════════════════════════════════════════════════════════════════
    // SEGMENTS — the list behind every number
    // ════════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<AdminSegmentTeacherDto>>>> GetSegmentAsync(
        string key, int windowDays, int page, int pageSize)
    {
        var parsed = AdminSegmentKeyParser.Parse(key);
        if (parsed is null)
            return Result<PaginatedResponse<List<AdminSegmentTeacherDto>>>.Failure(
                _localizer, "AdminSegmentUnknown", HttpStatusCode.BadRequest);

        // Clamped, never rejected. The grid 400s an out-of-range pageSize, which turns a fat-fingered
        // URL into an error screen where a sensible page would do.
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);

        var ctx = await BuildContextAsync(
            NormalizeWindow(windowDays),
            needCallList: parsed.Key == AdminSegmentKey.NeedsCall,
            needRenewals: parsed.Key is AdminSegmentKey.Renewed or AdminSegmentKey.NotRenewed);

        var ordered = ResolveSegment(ctx, parsed);

        // The count is taken on the ordered id list the page is cut from — the same population,
        // one statement apart, so the tile and its list cannot disagree (BUG-17).
        int total = ordered.Count;
        var pageIds = ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(r => r.TeacherId).ToList();

        var gridRows = await _unitOfWork.AdminInsightsRepo.GetUsageRowsByIdsAsync(pageIds);
        var byId = gridRows.ToDictionary(r => r.TeacherId);

        // Re-imposed in the segment's own order: GetUsageRowsByIdsAsync makes no promise about it,
        // and an IN-list comes back in whatever order the index scan produced.
        var pageRows = pageIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
        var enriched = await EnrichRowsAsync(pageRows);

        var evidenceById = ordered.ToDictionary(r => r.TeacherId, r => r.Evidence);
        var items = enriched.Select(item =>
        {
            var dto = CopyToSegmentItem(item);
            dto.Evidence = evidenceById.GetValueOrDefault(item.TeacherId);
            return dto;
        }).ToList();

        var response = new PaginatedResponse<List<AdminSegmentTeacherDto>>
        {
            totalCount = total,
            page = page,
            pageSize = pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize),
            data = items
        };

        return Result<PaginatedResponse<List<AdminSegmentTeacherDto>>>.Success(response, _localizer);
    }

    // ════════════════════════════════════════════════════════════════════════
    // RENEWALS
    // ════════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<AdminRenewalsDto>> GetRenewalsAsync(int months)
    {
        int count = Math.Clamp(months <= 0 ? AdminInsightsConstants.ConsoleRenewalMonths : months, 1, 24);
        var ctx = await BuildContextAsync(
            AdminInsightsConstants.ConsoleDefaultWindowDays,
            needCallList: false,
            needRenewals: true,
            renewalMonths: count);

        var dto = new AdminRenewalsDto
        {
            Months = ctx.Renewals.Months,
            FirstSubOnlyCount = ctx.Rows.Count(r => !r.IsCenterOwned && r.SubscriptionCount == 1),
            RenewedAtLeastOnceCount = ctx.Rows.Count(r => !r.IsCenterOwned && r.SubscriptionCount > 1),
            RenewalWindowDays = AdminInsightsConstants.ConsoleRenewalWindowDays
        };

        return Result<AdminRenewalsDto>.Success(dto, _localizer);
    }

    // ════════════════════════════════════════════════════════════════════════
    // GLOBAL SEARCH
    // ════════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<AdminSearchDto>> SearchAsync(string? query, int takePerGroup)
    {
        string term = query?.Trim() ?? string.Empty;

        // One character matches most of the platform and returns noise in four groups at once.
        if (term.Length < AdminInsightsConstants.ConsoleSearchMinLength)
            return Result<AdminSearchDto>.Success(
                new AdminSearchDto { Query = term }, _localizer);

        int take = Math.Clamp(
            takePerGroup <= 0 ? AdminInsightsConstants.ConsoleSearchTakePerGroup : takePerGroup,
            1, AdminInsightsConstants.ConsoleSearchMaxTakePerGroup);

        // Folded in C# exactly as the SQL side folds the column, so مصطفي finds مصطفى. The fold
        // happens HERE and the UDF handles the column — ArabicTextNormalizer itself must never go
        // inside an IQueryable, where EF cannot translate it and silently evaluates client-side.
        string normalized = ArabicTextNormalizer.Normalize(term);
        var repo = _unitOfWork.AdminInsightsRepo;

        var teachers = await repo.SearchTeachersAsync(normalized, take);
        var students = await repo.SearchStudentsAsync(normalized, take);
        var accounts = await repo.SearchStudentAccountsAsync(normalized, take);
        var assistants = await repo.SearchAssistantsAsync(normalized, take);

        var dto = new AdminSearchDto
        {
            Query = term,
            Teachers = teachers.Select(ToSearchHit).ToList(),
            Students = students.Select(ToSearchHit).ToList(),
            StudentAccounts = accounts.Select(ToSearchHit).ToList(),
            Assistants = assistants.Select(ToSearchHit).ToList(),
            TotalHits = teachers.Count + students.Count + accounts.Count + assistants.Count
        };

        return Result<AdminSearchDto>.Success(dto, _localizer);
    }

    private static AdminSearchHitDto ToSearchHit(ConsoleSearchHit h) => new()
    {
        Kind = h.Kind,
        Id = h.Id,
        UserId = h.UserId,
        FullName = h.Title,
        Code = h.Code,
        PhoneNumber = h.PhoneNumber,
        TeacherId = h.TeacherId,
        TeacherName = h.TeacherName
    };

    // ════════════════════════════════════════════════════════════════════════
    // TEACHER PAGE — logins and the data snapshot
    // ════════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<AdminTeacherLoginsDto>> GetTeacherLoginsAsync(long teacherId)
    {
        var repo = _unitOfWork.AdminInsightsRepo;

        var operators = await repo.GetOperatorsAsync(teacherId);
        if (operators.Count == 0)
            return Result<AdminTeacherLoginsDto>.Failure(
                _localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        var events = await repo.GetAssistantLoginEventsAsync(
            teacherId, AdminInsightsConstants.ConsoleLoginEventsPerPerson);

        AdminLoginPersonDto ToPerson(TeacherOperatorRow o) => new()
        {
            UserId = o.UserId,
            FullName = o.FullName,
            Username = o.Username,
            Role = o.Role,
            IsActive = o.IsActive,
            LastLoginAt = o.LastLoginAt,
            LastActivityAt = o.LastActivityAt,
            Events = events.GetValueOrDefault(o.UserId)?
                .Select(e => new AdminLoginEventDto
                {
                    Action = e.Action,
                    OccurredAt = e.OccurredAt,
                    DeviceOrBrowser = e.DeviceOrBrowser,
                    IpAddress = e.IpAddress
                })
                .ToList() ?? (IReadOnlyList<AdminLoginEventDto>)Array.Empty<AdminLoginEventDto>()
        };

        var dto = new AdminTeacherLoginsDto
        {
            TeacherId = teacherId,
            Teacher = ToPerson(operators.First(o => o.Role == "Teacher")),
            Assistants = operators.Where(o => o.Role != "Teacher").Select(ToPerson).ToList(),
            // Stated, not implied. There is no teacher login log on this platform — only assistants
            // get a row — so the teacher's block carries a last-login and nothing else, and the
            // screen must not render an empty list that reads as "never signed in".
            TeacherHistoryRecorded = false
        };

        return Result<AdminTeacherLoginsDto>.Success(dto, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<AdminTeacherSnapshotDto>> GetTeacherSnapshotAsync(long teacherId)
    {
        var repo = _unitOfWork.AdminInsightsRepo;

        var row = await repo.GetUsageRowAsync(teacherId);
        if (row is null)
            return Result<AdminTeacherSnapshotDto>.Failure(
                _localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        int titles = AdminInsightsConstants.ConsoleSnapshotTitles;

        var classes = await repo.GetClassesForTeacherAsync(teacherId);
        var counts = await repo.GetSnapshotCountsAsync(teacherId);
        var videos = await repo.GetVideoSummaryAsync(teacherId, titles);
        var onlineExams = await repo.GetOnlineExamSummaryAsync(teacherId, titles);
        var assignments = await repo.GetAssignmentSummaryAsync(teacherId, titles);

        var dto = new AdminTeacherSnapshotDto
        {
            TeacherId = teacherId,
            Classes = classes.Select(c => new SnapshotClassDto
            {
                SessionId = c.SessionId,
                SessionName = c.SessionName,
                GroupName = c.GroupName,
                ScheduleDays = c.SelectedDays,
                StartTime = TimeOnly.FromTimeSpan(c.StartTime),
                DurationMinutes = c.DurationMinutes,
                StartDate = DateOnly.FromDateTime(c.StartDate),
                EndDate = DateOnly.FromDateTime(c.EndDate),
                StudentCount = c.StudentCount,
                OccurrenceCount = c.OccurrenceCount
            }).ToList(),
            Videos = ToContent(videos),
            OnlineExams = ToContent(onlineExams),
            ExamsAndHomework = ToContent(assignments),
            StudentAccounts = new SnapshotAccountsDto
            {
                Active = counts.ActiveLinks,
                Bound = counts.BoundLinks
            },
            StudentCount = counts.Students,
            StudentsInClasses = counts.StudentsInClasses
        };

        return Result<AdminTeacherSnapshotDto>.Success(dto, _localizer);
    }

    private static SnapshotContentDto ToContent(ConsoleContentSummary s) => new()
    {
        Total = s.Total,
        LatestAt = s.LatestAt,
        LatestTitles = s.LatestTitles
    };

    // ════════════════════════════════════════════════════════════════════════
    // THE SHARED CONTEXT — loaded once, counted many times
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Everything the dashboard and the drill-downs both need: the population, the day boundaries,
    /// and the two id sets that cannot be derived from a teacher row alone.
    /// </summary>
    private sealed class ConsoleContext
    {
        public required IReadOnlyList<ConsoleTeacherRow> Rows { get; init; }
        public required DateOnly Today { get; init; }
        public required DateOnly Yesterday { get; init; }
        public required DateTime NowUtc { get; init; }
        public required DateTime YesterdayStartUtc { get; init; }
        public required DateTime TodayStartUtc { get; init; }
        public required DateTime WindowStartUtc { get; init; }
        public required DateTime EndingSoonCutoffUtc { get; init; }
        public required int WindowDays { get; init; }

        /// <summary>Teachers with real activity on the "yesterday" day.</summary>
        public required IReadOnlySet<long> ActiveYesterday { get; init; }

        /// <summary>The call list's own answer, so the card and the list share one definition.</summary>
        public required IReadOnlySet<long> NeedsCall { get; init; }

        /// <summary>Position in the call list, so the drill-down keeps its worklist order.</summary>
        public required IReadOnlyDictionary<long, int> NeedsCallRank { get; init; }

        public required RenewalAnalysis Renewals { get; init; }
    }

    /// <summary>The renewal story: the per-month rows, plus who is in each month's two lists.</summary>
    private sealed record RenewalAnalysis(
        IReadOnlyList<RenewalMonthDto> Months,
        IReadOnlyDictionary<string, IReadOnlyList<long>> RenewedByMonth,
        IReadOnlyDictionary<string, IReadOnlyList<long>> ChurnedByMonth);

    private static int NormalizeWindow(int windowDays) =>
        AdminInsightsConstants.ConsoleWindowDays.Contains(windowDays)
            ? windowDays
            : AdminInsightsConstants.ConsoleDefaultWindowDays;

    /// <summary>Today in the tenant's zone (Africa/Cairo) — never UtcNow.Date, which is still
    /// yesterday between midnight and 3 AM Cairo.</summary>
    private DateOnly LocalToday() => DateOnly.FromDateTime(_timeZone.ConvertUtcToLocal(DateTime.UtcNow));

    /// <summary>The UTC instant a teacher-local calendar day begins.</summary>
    private DateTime LocalDayStartUtc(DateOnly day) =>
        _timeZone.ConvertLocalToUtc(day.ToDateTime(TimeOnly.MinValue));

    /// <summary>
    /// Loads the population and whichever of the two EXPENSIVE extras the caller actually needs.
    ///
    /// The call list walks eight insight queries and deduplicates them; the renewal analysis reads
    /// every subscription span in the window. The dashboard needs both. A drill-down into
    /// "registered yesterday" needs neither, and paying for them on every click would make the
    /// cheapest list on the console the slowest thing it does.
    /// </summary>
    private async Task<ConsoleContext> BuildContextAsync(
        int windowDays,
        bool needCallList = true,
        bool needRenewals = true,
        int? renewalMonths = null)
    {
        DateTime nowUtc = DateTime.UtcNow;
        DateOnly today = LocalToday();
        DateOnly yesterday = today.AddDays(-1);

        var rows = await _unitOfWork.AdminInsightsRepo.GetConsoleTeacherRowsAsync(nowUtc);
        var activeYesterday = await _unitOfWork.AdminInsightsRepo.GetTeacherIdsActiveOnAsync(yesterday);

        // The call list is the ONE definition of "needs a call". Taken in full rather than a page,
        // because the dashboard card reports its size and the drill-down pages through it.
        var needsCallOrder = new List<long>();
        if (needCallList)
        {
            var callList = await GetCallListAsync(null, AdminInsightsConstants.CallListMaxTake);
            needsCallOrder = callList.Data?.Items.Select(i => i.TeacherId).ToList() ?? new List<long>();
        }

        var needsCallRank = needsCallOrder
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);

        int months = renewalMonths ?? AdminInsightsConstants.ConsoleRenewalMonths;
        var renewals = needRenewals
            ? await BuildRenewalsAsync(today, nowUtc, months)
            : new RenewalAnalysis(
                Array.Empty<RenewalMonthDto>(),
                new Dictionary<string, IReadOnlyList<long>>(),
                new Dictionary<string, IReadOnlyList<long>>());

        return new ConsoleContext
        {
            Rows = rows,
            Today = today,
            Yesterday = yesterday,
            NowUtc = nowUtc,
            YesterdayStartUtc = LocalDayStartUtc(yesterday),
            TodayStartUtc = LocalDayStartUtc(today),
            WindowStartUtc = LocalDayStartUtc(today.AddDays(-windowDays)),
            EndingSoonCutoffUtc = nowUtc.AddDays(AdminInsightsConstants.ConsoleEndingSoonDays),
            WindowDays = windowDays,
            ActiveYesterday = activeYesterday,
            NeedsCall = needsCallOrder.ToHashSet(),
            NeedsCallRank = needsCallRank,
            Renewals = renewals
        };
    }

    /// <summary>
    /// Renewed vs churned, month by month.
    ///
    /// A RENEWAL IS A NEW ROW. <c>activate</c> inserts one and flips the previous to non-current;
    /// <c>extend</c> and <c>set-end-date</c> mutate the row that is already there. Only the first
    /// is a renewal, and this is the query that depends on that being true.
    /// </summary>
    private async Task<RenewalAnalysis> BuildRenewalsAsync(DateOnly today, DateTime nowUtc, int months)
    {
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
        DateTime fromUtc = LocalDayStartUtc(firstMonth);

        var spans = await _unitOfWork.AdminInsightsRepo.GetSubscriptionSpansAsync(fromUtc);
        var byTeacher = spans.GroupBy(s => s.TeacherId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.StartDate).ToList());

        var rows = new List<RenewalMonthDto>(months);
        var renewedByMonth = new Dictionary<string, IReadOnlyList<long>>();
        var churnedByMonth = new Dictionary<string, IReadOnlyList<long>>();

        for (int i = 0; i < months; i++)
        {
            DateOnly monthStart = firstMonth.AddMonths(i);
            DateTime startUtc = LocalDayStartUtc(monthStart);
            DateTime endUtc = LocalDayStartUtc(monthStart.AddMonths(1));
            string monthKey = monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture);

            var renewed = new List<long>();
            var churned = new List<long>();

            // COUNTED PER TEACHER, NOT PER PERIOD. Both are defensible readings of "how many
            // renewed", but only one of them lets the card and the list behind it agree: a teacher
            // with two periods ending in one month is two subscriptions and one person to call, and
            // the drill-down can only show the person. Counting people keeps the tile honest about
            // what clicking it opens, and it is the question being asked anyway.
            //
            // Only periods that have ACTUALLY ended. A subscription running into next week has not
            // failed to renew; counting it as churn would report every healthy customer as lost.
            var endedByTeacher = spans
                .Where(s => s.EndDate >= startUtc && s.EndDate < endUtc && s.EndDate <= nowUtc)
                .GroupBy(s => s.TeacherId);

            foreach (var group in endedByTeacher)
            {
                // Renewed if ANY period that ended this month was followed by a new row. Judged per
                // teacher so someone who let one class lapse and renewed another is not counted in
                // both lists, which would make the two halves sum to more than the whole.
                bool anyRenewed = group.Any(span => byTeacher[group.Key].Any(later =>
                    later.StartDate > span.StartDate &&
                    later.StartDate >= span.EndDate.AddDays(-1) &&
                    later.StartDate <= span.EndDate.AddDays(AdminInsightsConstants.ConsoleRenewalWindowDays)));

                if (anyRenewed) renewed.Add(group.Key);
                else churned.Add(group.Key);
            }

            int ended = renewed.Count + churned.Count;
            rows.Add(new RenewalMonthDto
            {
                Month = monthKey,
                Label = monthStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                Ended = ended,
                Renewed = renewed.Count,
                Churned = churned.Count,
                RatePercent = ended == 0 ? 0 : (int)Math.Round(renewed.Count * 100d / ended),
                RenewedSegmentKey = $"{AdminSegmentKey.Renewed}:{monthKey}",
                ChurnedSegmentKey = $"{AdminSegmentKey.NotRenewed}:{monthKey}"
            });

            // Already one entry per teacher — these ARE the lists the two counts above describe.
            renewedByMonth[monthKey] = renewed;
            churnedByMonth[monthKey] = churned;
        }

        return new RenewalAnalysis(rows, renewedByMonth, churnedByMonth);
    }

    // ════════════════════════════════════════════════════════════════════════
    // THE PREDICATES — one definition per card, used by BOTH the count and the list
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>A live subscription: Active or ExpiringSoon. An expired teacher is not a customer.</summary>
    private static bool IsSubscribed(ConsoleTeacherRow r) =>
        r.SubscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.ExpiringSoon;

    /// <summary>
    /// The predicate behind one card. EVERY branch states its own scope — independent-only, or
    /// subscribers-only — rather than relying on the caller to have filtered first. That is what
    /// makes the reconciliation identities on the page true no matter which order things are
    /// counted in.
    /// </summary>
    private static Func<ConsoleTeacherRow, bool>? SegmentPredicate(
        ConsoleContext ctx, AdminSegmentKey key, UsageModules? module)
    {
        bool Independent(ConsoleTeacherRow r) => !r.IsCenterOwned;
        bool Subscriber(ConsoleTeacherRow r) => Independent(r) && IsSubscribed(r);
        int bit = module is null ? 0 : (int)module.Value;

        return key switch
        {
            AdminSegmentKey.RegisteredYesterday => r => Independent(r)
                && r.RegisteredAt >= ctx.YesterdayStartUtc && r.RegisteredAt < ctx.TodayStartUtc,

            AdminSegmentKey.SubscribedYesterday => r => Independent(r)
                && r.SubscriptionStartDate >= ctx.YesterdayStartUtc
                && r.SubscriptionStartDate < ctx.TodayStartUtc,

            AdminSegmentKey.ExpiredYesterday => r => Independent(r)
                && r.SubscriptionEndDate >= ctx.YesterdayStartUtc
                && r.SubscriptionEndDate < ctx.TodayStartUtc,

            // FirstActivityAt is a teacher-LOCAL calendar day stored at midnight, so comparing its
            // date to a local date is exact — no conversion, and none wanted.
            AdminSegmentKey.StartedUsingYesterday => r => Independent(r)
                && r.FirstActivityAt is not null
                && DateOnly.FromDateTime(r.FirstActivityAt.Value) == ctx.Yesterday,

            AdminSegmentKey.UsedYesterday => r => Independent(r)
                && ctx.ActiveYesterday.Contains(r.TeacherId),

            AdminSegmentKey.NewlyRegistered => r => Independent(r)
                && r.RegisteredAt >= ctx.WindowStartUtc,

            AdminSegmentKey.NewlySubscribed => r => Independent(r)
                && r.SubscriptionStartDate >= ctx.WindowStartUtc,

            AdminSegmentKey.SubscribedNow => Subscriber,

            // Explicitly seven days off the end date — see ConsoleEndingSoonDays for why this does
            // not reuse the subscription module's five-day band.
            AdminSegmentKey.EndingSoon => r => Subscriber(r)
                && r.SubscriptionEndDate is not null
                && r.SubscriptionEndDate <= ctx.EndingSoonCutoffUtc,

            // Had one, it ran out, nothing replaced it. Two exclusions, both deliberate: a teacher
            // who never subscribed is a lead, not a lapsed customer; and a FUTURE-dated
            // subscription also derives as Expired (it has not started yet), so the end date must
            // be in the past before anyone is called about a subscription that ran out.
            AdminSegmentKey.Expired => r => Independent(r)
                && r.SubscriptionStatus == SubscriptionStatus.Expired
                && r.SubscriptionEndDate is not null
                && r.SubscriptionEndDate <= ctx.NowUtc,

            AdminSegmentKey.CenterTeachers => r => r.IsCenterOwned,

            AdminSegmentKey.UsingApp7 => r => Subscriber(r) && r.ActiveDays7 > 0,
            AdminSegmentKey.NotUsing7 => r => Subscriber(r) && r.ActiveDays7 == 0,
            AdminSegmentKey.UsingApp30 => r => Subscriber(r) && r.ActiveDays30 > 0,
            AdminSegmentKey.NotUsing30 => r => Subscriber(r) && r.ActiveDays30 == 0,

            AdminSegmentKey.UploadedStudents => r => Subscriber(r) && r.StudentCount > 0,
            AdminSegmentKey.NoStudents => r => Subscriber(r) && r.StudentCount == 0,

            AdminSegmentKey.StudentsNotInClasses => r => Subscriber(r)
                && r.StudentCount > 0 && r.StudentsAssignedToSession == 0,

            AdminSegmentKey.NeedsCall => r => ctx.NeedsCall.Contains(r.TeacherId),

            AdminSegmentKey.ModuleHaveIt => r => Subscriber(r) && (r.EntitledMask & bit) != 0,
            AdminSegmentKey.ModuleUsing => r => Subscriber(r)
                && (r.EntitledMask & bit) != 0 && (r.UsedMask30 & bit) != 0,
            // Entitled AND never opened. Without the entitlement half this lists everyone who does
            // not have the feature as ignoring it.
            AdminSegmentKey.ModuleNeverOpened => r => Subscriber(r)
                && (r.EntitledMask & bit) != 0 && (r.EverUsedMask & bit) == 0,

            AdminSegmentKey.FirstSubOnly => r => Independent(r) && r.SubscriptionCount == 1,
            AdminSegmentKey.RenewedAtLeastOnce => r => Independent(r) && r.SubscriptionCount > 1,

            // The two per-month families are id sets, not row predicates — ResolveSegment handles them.
            _ => null
        };
    }

    /// <summary>A teacher in a segment, with the words explaining why they are in it.</summary>
    private sealed record SegmentMember(long TeacherId, string? Evidence);

    /// <summary>
    /// The ordered membership of one segment. Ordering is per-segment and deliberate: a "needs a
    /// call" list keeps the call list's own priority, a "just subscribed" list leads with the
    /// newest, and a stalled list leads with the most students at stake.
    /// </summary>
    private List<SegmentMember> ResolveSegment(ConsoleContext ctx, AdminSegmentKeyParser.Parsed parsed)
    {
        if (parsed.Key is AdminSegmentKey.Renewed or AdminSegmentKey.NotRenewed)
        {
            string monthKey = parsed.Month!.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var source = parsed.Key == AdminSegmentKey.Renewed
                ? ctx.Renewals.RenewedByMonth
                : ctx.Renewals.ChurnedByMonth;

            var ids = source.GetValueOrDefault(monthKey) ?? Array.Empty<long>();
            var byId = ctx.Rows.ToDictionary(r => r.TeacherId);

            return ids
                .Where(byId.ContainsKey)
                .Select(id => byId[id])
                // Churn ordered by roster size: the biggest account lost is the first call back.
                .OrderByDescending(r => r.StudentCount)
                .Select(r => new SegmentMember(r.TeacherId, Evidence(ctx, r, parsed.Key)))
                .ToList();
        }

        var predicate = SegmentPredicate(ctx, parsed.Key, parsed.Module);
        if (predicate is null) return new List<SegmentMember>();

        var matched = ctx.Rows.Where(predicate);

        matched = parsed.Key switch
        {
            AdminSegmentKey.RegisteredYesterday or AdminSegmentKey.NewlyRegistered =>
                matched.OrderByDescending(r => r.RegisteredAt),

            AdminSegmentKey.SubscribedYesterday or AdminSegmentKey.NewlySubscribed =>
                // Not-yet-started first: a teacher who has just paid and cannot get going is the
                // call that prevents a refund.
                matched.OrderBy(r => r.StudentsAssignedToSession > 0)
                       .ThenByDescending(r => r.SubscriptionStartDate),

            AdminSegmentKey.ExpiredYesterday or AdminSegmentKey.Expired or AdminSegmentKey.EndingSoon =>
                matched.OrderBy(r => r.SubscriptionEndDate),

            AdminSegmentKey.StartedUsingYesterday =>
                matched.OrderByDescending(r => r.StudentCount),

            AdminSegmentKey.UsedYesterday or AdminSegmentKey.UsingApp7 or AdminSegmentKey.UsingApp30 =>
                matched.OrderByDescending(r => r.ActiveDays30),

            AdminSegmentKey.NotUsing7 or AdminSegmentKey.NotUsing30
                or AdminSegmentKey.NoStudents or AdminSegmentKey.StudentsNotInClasses
                or AdminSegmentKey.UploadedStudents =>
                matched.OrderByDescending(r => r.StudentCount),

            // Keep the call list's own priority: it is already a worklist, and re-sorting it here
            // would throw away the one ordering on the platform that says what to do first.
            AdminSegmentKey.NeedsCall =>
                matched.OrderBy(r => ctx.NeedsCallRank.GetValueOrDefault(r.TeacherId, int.MaxValue)),

            _ => matched.OrderByDescending(r => r.StudentCount).ThenBy(r => r.FullName)
        };

        return matched
            .Select(r => new SegmentMember(r.TeacherId, Evidence(ctx, r, parsed.Key)))
            .ToList();
    }

    // ════════════════════════════════════════════════════════════════════════
    // MAPPING
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>A card's number, its delta, and the key that opens the people inside it.</summary>
    private static DashboardStatDto Stat(ConsoleContext ctx, AdminSegmentKey key, int? delta = null)
    {
        var predicate = SegmentPredicate(ctx, key, null);
        return new DashboardStatDto
        {
            Count = predicate is null ? 0 : ctx.Rows.Count(predicate),
            SegmentKey = key.ToString(),
            Delta = delta
        };
    }

    /// <summary>A count that already carries its first few names, so the morning read needs no click.</summary>
    private DashboardNamedCountDto NamedCount(ConsoleContext ctx, AdminSegmentKey key)
    {
        var predicate = SegmentPredicate(ctx, key, null);
        var matched = predicate is null
            ? new List<ConsoleTeacherRow>()
            : ctx.Rows.Where(predicate).ToList();

        return new DashboardNamedCountDto
        {
            SegmentKey = key.ToString(),
            Count = matched.Count,
            Teachers = matched
                .OrderByDescending(r => r.StudentCount)
                .Take(AdminInsightsConstants.ConsoleYesterdayPreviewSize)
                .Select(r => new DashboardTeacherDto
                {
                    TeacherId = r.TeacherId,
                    UserId = r.UserId,
                    FullName = r.FullName,
                    TeacherCode = r.TeacherCode,
                    PhoneNumber = r.PhoneNumber,
                    Evidence = Evidence(ctx, r, key)
                })
                .ToList()
        };
    }

    private static IReadOnlyList<BandCountDto> PlanBreakdown(IEnumerable<ConsoleTeacherRow> rows) =>
        rows.GroupBy(r => r.PlanType?.ToString() ?? "None")
            .Select(g => new BandCountDto { Key = g.Key, Count = g.Count() })
            .OrderByDescending(b => b.Count)
            .ToList();

    /// <summary>
    /// The feature table, counted only over subscribers who HAVE each feature. A feature nobody is
    /// entitled to is left out entirely rather than shown as a row of zeros — the table is read to
    /// find where the product is failing to land, and an empty row is not a failure.
    /// </summary>
    private static IReadOnlyList<DashboardFeatureRowDto> BuildFeatureRows(
        ConsoleContext ctx, IReadOnlyList<ConsoleTeacherRow> subscribers)
    {
        var rows = new List<DashboardFeatureRowDto>();

        foreach (var module in Enum.GetValues<UsageModules>())
        {
            if (module == UsageModules.None) continue;
            int bit = (int)module;

            var entitled = subscribers.Where(r => (r.EntitledMask & bit) != 0).ToList();
            if (entitled.Count == 0) continue;

            rows.Add(new DashboardFeatureRowDto
            {
                Feature = module.ToString(),
                HaveIt = entitled.Count,
                Using30 = entitled.Count(r => (r.UsedMask30 & bit) != 0),
                NeverOpened = entitled.Count(r => (r.EverUsedMask & bit) == 0),
                HaveItSegmentKey = $"{AdminSegmentKey.ModuleHaveIt}:{module}",
                UsingSegmentKey = $"{AdminSegmentKey.ModuleUsing}:{module}",
                NeverOpenedSegmentKey = $"{AdminSegmentKey.ModuleNeverOpened}:{module}"
            });
        }

        // Worst-adopted first: this table is read to find the gap, not to admire what already works.
        return rows
            .OrderBy(f => f.HaveIt == 0 ? 1d : (double)(f.HaveIt - f.NeverOpened) / f.HaveIt)
            .ThenByDescending(f => f.HaveIt)
            .ToList();
    }

    /// <summary>
    /// Why this teacher is in THIS card, in plain words with the real numbers in them. Every string
    /// is localized and none of them uses a word a new sales hire would have to be taught.
    /// </summary>
    private string? Evidence(ConsoleContext ctx, ConsoleTeacherRow r, AdminSegmentKey key)
    {
        string StudentsLine() => r.StudentCount == 0
            ? _localizer["ConsoleEvidenceNoStudents"]
            : _localizer["ConsoleEvidenceStudentsInClasses", r.StudentCount, r.StudentsAssignedToSession];

        int DaysSince(DateTime at) => Math.Max(0, (int)(ctx.NowUtc - at).TotalDays);
        int DaysUntil(DateTime at) => Math.Max(0, (int)Math.Ceiling((at - ctx.NowUtc).TotalDays));

        return key switch
        {
            AdminSegmentKey.RegisteredYesterday or AdminSegmentKey.NewlyRegistered =>
                _localizer["ConsoleEvidenceRegistered", Ago(DaysSince(r.RegisteredAt))],

            AdminSegmentKey.SubscribedYesterday or AdminSegmentKey.NewlySubscribed
                or AdminSegmentKey.SubscribedNow => r.SubscriptionStartDate is null
                    ? StudentsLine()
                    : $"{_localizer["ConsoleEvidenceSubscribed", Ago(DaysSince(r.SubscriptionStartDate.Value))]} · {StudentsLine()}",

            AdminSegmentKey.ExpiredYesterday or AdminSegmentKey.Expired
                or AdminSegmentKey.NotRenewed => r.SubscriptionEndDate is null
                    ? StudentsLine()
                    : $"{_localizer["ConsoleEvidenceEnded", Ago(DaysSince(r.SubscriptionEndDate.Value))]} · {StudentsLine()}",

            AdminSegmentKey.EndingSoon => r.SubscriptionEndDate is null
                ? StudentsLine()
                : $"{_localizer["ConsoleEvidenceEndingIn", DaysUntil(r.SubscriptionEndDate.Value)]} · {StudentsLine()}",

            AdminSegmentKey.Renewed => _localizer["ConsoleEvidenceRenewed", r.SubscriptionCount],

            AdminSegmentKey.StartedUsingYesterday =>
                $"{_localizer["ConsoleEvidenceStartedUsing"]} · {StudentsLine()}",

            AdminSegmentKey.UsedYesterday or AdminSegmentKey.UsingApp7 or AdminSegmentKey.UsingApp30 =>
                _localizer["ConsoleEvidenceActiveDays", r.ActiveDays30, 30],

            AdminSegmentKey.NotUsing7 or AdminSegmentKey.NotUsing30 => r.LastActivityAt is null
                ? _localizer["ConsoleEvidenceNeverUsed"]
                : _localizer["ConsoleEvidenceNothingSince", DaysSince(r.LastActivityAt.Value)],

            AdminSegmentKey.NoStudents or AdminSegmentKey.UploadedStudents
                or AdminSegmentKey.StudentsNotInClasses or AdminSegmentKey.CenterTeachers =>
                StudentsLine(),

            AdminSegmentKey.ModuleUsing or AdminSegmentKey.ModuleHaveIt =>
                _localizer["ConsoleEvidenceActiveDays", r.ActiveDays30, 30],

            AdminSegmentKey.ModuleNeverOpened => r.ActiveDays30 > 0
                ? _localizer["ConsoleEvidenceNeverOpenedActive", r.ActiveDays30]
                : _localizer["ConsoleEvidenceNeverOpenedQuiet"],

            AdminSegmentKey.FirstSubOnly => r.SubscriptionStartDate is null
                ? null
                : _localizer["ConsoleEvidenceFirstSubscription", Ago(DaysSince(r.SubscriptionStartDate.Value))],

            AdminSegmentKey.RenewedAtLeastOnce => _localizer["ConsoleEvidenceRenewed", r.SubscriptionCount],

            // NeedsCall already carries the call list's own "why" and "what to do"; repeating a
            // weaker version of it here would only compete with the line that matters.
            _ => StudentsLine()
        };
    }

    /// <summary>
    /// Widens a grid item into a segment item. Written out rather than reflected: the grid DTO is a
    /// shipped wire contract, and a silent reflection copy would drop any field added to it later
    /// without anything failing.
    /// </summary>
    private static AdminSegmentTeacherDto CopyToSegmentItem(TeacherUsageListItemDto s) => new()
    {
        TeacherId = s.TeacherId,
        UserId = s.UserId,
        FullName = s.FullName,
        Username = s.Username,
        TeacherCode = s.TeacherCode,
        PhoneNumber = s.PhoneNumber,
        Email = s.Email,
        RegisteredAt = s.RegisteredAt,
        AccountStatus = s.AccountStatus,
        SubscriptionStatus = s.SubscriptionStatus,
        SubscriptionStartDate = s.SubscriptionStartDate,
        SubscriptionEndDate = s.SubscriptionEndDate,
        PlanType = s.PlanType,
        SalesRepId = s.SalesRepId,
        SalesRepName = s.SalesRepName,
        AcquisitionSource = s.AcquisitionSource,
        Cadence = s.Cadence,
        ActiveDays7 = s.ActiveDays7,
        ActiveDays30 = s.ActiveDays30,
        ActiveDays90 = s.ActiveDays90,
        TotalWrites30 = s.TotalWrites30,
        Depth = s.Depth,
        Modules = s.Modules,
        ModulesAllTime = s.ModulesAllTime,
        FeaturesEntitled = s.FeaturesEntitled,
        FeaturesNeverUsed = s.FeaturesNeverUsed,
        FeaturesLapsed = s.FeaturesLapsed,
        FeaturesAdoptedCount = s.FeaturesAdoptedCount,
        FeaturesEntitledCount = s.FeaturesEntitledCount,
        Operators = s.Operators,
        LastTeacherActivityAt = s.LastTeacherActivityAt,
        LastAssistantActivityAt = s.LastAssistantActivityAt,
        ActiveAssistantCount = s.ActiveAssistantCount,
        FirstActivityAt = s.FirstActivityAt,
        LastActivityAt = s.LastActivityAt,
        StudentCount = s.StudentCount,
        StudentsAssignedToSession = s.StudentsAssignedToSession,
        SessionCount = s.SessionCount,
        SessionsWithOccurrences = s.SessionsWithOccurrences,
        LinkedAccountCount = s.LinkedAccountCount,
        BoundAccountCount = s.BoundAccountCount,
        HasEverMarkedAttendance = s.HasEverMarkedAttendance,
        HasEverCollectedPayment = s.HasEverCollectedPayment,
        HasRealData = s.HasRealData,
        NoteCount = s.NoteCount,
        LastNoteAt = s.LastNoteAt,
        Sparkline30 = s.Sparkline30,
        ComputedAt = s.ComputedAt
    };
}
