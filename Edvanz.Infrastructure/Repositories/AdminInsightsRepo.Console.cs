using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Helpers;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// The admin CONSOLE half of the insights repo: the dashboard population, the trend/renewal spans,
/// the global search, and the two support lookups on a teacher's page.
///
/// Kept in its own partial file purely for readability — it shares <c>BuildRowQuery</c> and the
/// context with the grid half, which is the point: a drill-down list and the grid row it opens must
/// come from the same join or they will eventually disagree about who exists.
/// </summary>
public partial class AdminInsightsRepo
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ConsoleTeacherRow>> GetConsoleTeacherRowsAsync(
        DateTime nowUtc, CancellationToken ct = default)
    {
        DateTime soonUtc = nowUtc.AddDays(SubscriptionStatusCalculator.ExpiringSoonThresholdDays);

        // One SELECT over every teacher. The two correlated subqueries (capacity comes off the
        // teacher row itself; the subscription COUNT and the current-row join) stay inside it, so
        // this is a single round trip regardless of how many cards the dashboard draws.
        return await (
            from t in _context.Teachers.AsNoTracking()
            join sn in _context.TeacherUsageSnapshots.AsNoTracking()
                on t.Id equals sn.TeacherId into snj
            from sn in snj.DefaultIfEmpty()
            join sub in _context.TeacherSubscriptions.AsNoTracking().Where(x => x.IsCurrent)
                on t.Id equals sub.TeacherId into subj
            from sub in subj.DefaultIfEmpty()
            select new ConsoleTeacherRow(
                t.Id,
                t.UserId,
                t.User.FullName,
                t.TeacherCode,
                t.User.PhoneNumber,
                t.CreateAt,
                t.CenterId != null,

                // The status rules documented on SubscriptionStatusCalculator, as a translatable
                // CASE — the same expression the grid's BuildRowQuery uses, so the dashboard and
                // the list it drills into can never classify one teacher two different ways.
                sub == null ? SubscriptionStatus.Expired
                    : nowUtc < sub.StartDate ? SubscriptionStatus.Expired
                    : nowUtc >= sub.EndDate ? SubscriptionStatus.Expired
                    : sub.EndDate <= soonUtc ? SubscriptionStatus.ExpiringSoon
                    : SubscriptionStatus.Active,
                sub == null ? null : (SubscriptionPlanType?)sub.PlanType,
                sub == null ? null : (DateTime?)sub.StartDate,
                sub == null ? null : (DateTime?)sub.EndDate,

                t.LinkedStudentCapacity,
                sub == null ? 0m : sub.AmountPaidEGP,
                _context.TeacherSubscriptions.Count(s => s.TeacherId == t.Id),

                sn == null ? 0 : sn.ActiveDays7,
                sn == null ? 0 : sn.ActiveDays30,
                sn == null ? 0 : sn.EntitledModulesMask,
                sn == null ? 0 : sn.ModulesUsedMask,
                sn == null ? 0 : sn.ModulesUsedAllTimeMask,
                sn == null ? 0 : sn.StudentCount,
                sn == null ? 0 : sn.StudentsAssignedToSession,
                sn == null ? null : sn.FirstActivityAt,
                sn == null ? null : sn.LastActivityAt,
                sn == null ? null : (DateTime?)sn.ComputedAt))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<long>> GetTeacherIdsActiveOnAsync(
        DateOnly activityDate, CancellationToken ct = default)
    {
        // TeacherUsageDay.ActivityDate is ALREADY the teacher's local calendar day (the rollup
        // buckets it that way), so this compares days to days — never a UTC instant to a local one.
        var ids = await _context.TeacherUsageDays
            .AsNoTracking()
            .Where(d => d.ActivityDate == activityDate && d.TotalWrites > 0)
            .Select(d => d.TeacherId)
            .Distinct()
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    /// <inheritdoc />
    public async Task<ConsolePlatformTotals> GetPlatformTotalsAsync(CancellationToken ct = default)
    {
        // Soft-deleted rows are excluded by the entities' global query filters.
        int students = await _context.TeacherStudents.AsNoTracking().CountAsync(ct);

        // A linked account is one that is BOUND to a roster record — an Active-but-unbound link is
        // connected and sees nothing, so counting it as a live account overstates the platform.
        int linkedAccounts = await _context.StudentTeacherLinks
            .AsNoTracking()
            .CountAsync(l => l.LinkStatus == LinkStatus.Active && l.TeacherStudentId != null, ct);

        int assistants = await _context.Assistants
            .AsNoTracking()
            .CountAsync(a => a.RemovedAt == null && a.DeletedAt == null, ct);

        int centers = await _context.Centers.AsNoTracking().CountAsync(ct);

        return new ConsolePlatformTotals(students, linkedAccounts, assistants, centers);
    }

    /// <inheritdoc />
    public async Task<ConsolePendingApprovals> GetPendingApprovalsAsync(CancellationToken ct = default)
    {
        // AwaitingSuperAdminApproval, not Initiated: the SAME predicate the admin queue screen
        // pages over. An Initiated row is a teacher part-way through the flow with nothing for an
        // admin to decide, and badging it would send someone to an empty queue.
        var pendingPayments = await _context.PendingSubscriptionPayments
            .AsNoTracking()
            .Where(p => p.Status == PendingPaymentStatus.AwaitingSuperAdminApproval)
            .GroupBy(p => 1)
            .Select(g => new { Count = g.Count(), Amount = g.Sum(p => p.AmountEGP) })
            .FirstOrDefaultAsync(ct);

        var subRequests = await _context.SubscriptionRequests
            .AsNoTracking()
            .Where(r => r.Status == SubscriptionRequestStatus.Pending)
            .GroupBy(r => 1)
            .Select(g => new { Count = g.Count(), Amount = g.Sum(r => r.ComputedAmountEGP) })
            .FirstOrDefaultAsync(ct);

        // Capacity requests carry no price — an increase is granted, then billed on the next
        // renewal at the new seat count. A money column here would be an invention.
        int capacityRequests = await _context.CapacityIncreaseRequests
            .AsNoTracking()
            .CountAsync(r => r.Status == CapacityRequestStatus.Pending, ct);

        var centerRequests = await _context.CenterSubscriptionRequests
            .AsNoTracking()
            .Where(r => r.Status == SubscriptionRequestStatus.Pending)
            .GroupBy(r => 1)
            .Select(g => new { Count = g.Count(), Amount = g.Sum(r => r.ComputedAmountEGP) })
            .FirstOrDefaultAsync(ct);

        int independence = await _context.TeacherIndependenceRequests
            .AsNoTracking()
            .CountAsync(r => r.Status == SubscriptionRequestStatus.Pending, ct);

        return new ConsolePendingApprovals(
            pendingPayments?.Count ?? 0, pendingPayments?.Amount ?? 0m,
            subRequests?.Count ?? 0, subRequests?.Amount ?? 0m,
            capacityRequests,
            centerRequests?.Count ?? 0, centerRequests?.Amount ?? 0m,
            independence);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConsoleSubscriptionSpan>> GetSubscriptionSpansAsync(
        DateTime fromUtc, CancellationToken ct = default)
        // Centre-owned teachers are excluded here rather than filtered out afterwards: they never
        // belong in an independent-subscriber trend, and leaving them in the raw set invites a
        // later caller to forget.
        => await _context.TeacherSubscriptions
            .AsNoTracking()
            .Where(s => s.EndDate >= fromUtc && s.Teacher.CenterId == null)
            .OrderBy(s => s.StartDate)
            .Select(s => new ConsoleSubscriptionSpan(s.TeacherId, s.StartDate, s.EndDate, s.IsCurrent))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConsoleExtension>> GetSubscriptionExtensionsAsync(
        DateTime fromUtc, CancellationToken ct = default)
        => await _context.SubscriptionExtensions
            .AsNoTracking()
            .Where(e => e.CreateAt >= fromUtc && e.Teacher.CenterId == null)
            .OrderBy(e => e.CreateAt)
            .Select(e => new ConsoleExtension(e.TeacherId, e.CreateAt, e.DaysAdded, e.AmountPaidEGP))
            .ToListAsync(ct);

    // ════════════════════════════════════════════════════════════════════════
    // GLOBAL SEARCH
    // ════════════════════════════════════════════════════════════════════════
    //
    // Every group folds Arabic orthographic variants through the dbo.ArabicNormalize UDF on BOTH
    // sides, exactly as the teacher grid already does. Phone numbers are matched raw — they carry
    // no Arabic letters, and normalising them would only cost an index.

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConsoleSearchHit>> SearchTeachersAsync(
        string normalizedTerm, int take, CancellationToken ct = default)
        => await _context.Teachers
            .AsNoTracking()
            .Where(t => DbSearch.ArabicNormalize(t.User.FullName).Contains(normalizedTerm)
                     || DbSearch.ArabicNormalize(t.TeacherCode).Contains(normalizedTerm)
                     || (t.User.Username != null &&
                         DbSearch.ArabicNormalize(t.User.Username).Contains(normalizedTerm))
                     || (t.User.PhoneNumber != null && t.User.PhoneNumber.Contains(normalizedTerm)))
            .OrderBy(t => t.User.FullName)
            .Take(take)
            .Select(t => new ConsoleSearchHit(
                "Teacher", t.Id, t.UserId, t.User.FullName,
                t.TeacherCode, t.User.PhoneNumber, t.Id, null))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConsoleSearchHit>> SearchStudentsAsync(
        string normalizedTerm, int take, CancellationToken ct = default)
        => await _context.TeacherStudents
            .AsNoTracking()
            .Where(s => DbSearch.ArabicNormalize(s.StudentName).Contains(normalizedTerm)
                     || DbSearch.ArabicNormalize(s.StudentCode).Contains(normalizedTerm)
                     || (s.StudentPhoneNumber != null && s.StudentPhoneNumber.Contains(normalizedTerm))
                     || (s.ParentPhoneNumber != null && s.ParentPhoneNumber.Contains(normalizedTerm)))
            .OrderBy(s => s.StudentName)
            .Take(take)
            // A roster student means nothing without the teacher who owns them — the same code is
            // reused across accounts, so the teacher's name IS the disambiguator.
            .Select(s => new ConsoleSearchHit(
                "Student", s.Id, null, s.StudentName,
                s.StudentCode, s.StudentPhoneNumber ?? s.ParentPhoneNumber,
                s.TeacherId, s.Teacher.User.FullName))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConsoleSearchHit>> SearchStudentAccountsAsync(
        string normalizedTerm, int take, CancellationToken ct = default)
        => await _context.StudentUsers
            .AsNoTracking()
            .Where(u => DbSearch.ArabicNormalize(u.User.FullName).Contains(normalizedTerm)
                     || DbSearch.ArabicNormalize(u.StudentAccountCode).Contains(normalizedTerm)
                     || (u.User.Username != null &&
                         DbSearch.ArabicNormalize(u.User.Username).Contains(normalizedTerm))
                     || (u.User.PhoneNumber != null && u.User.PhoneNumber.Contains(normalizedTerm)))
            .OrderBy(u => u.User.FullName)
            .Take(take)
            .Select(u => new ConsoleSearchHit(
                "StudentAccount", u.Id, u.UserId, u.User.FullName,
                u.StudentAccountCode, u.User.PhoneNumber, null, null))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConsoleSearchHit>> SearchAssistantsAsync(
        string normalizedTerm, int take, CancellationToken ct = default)
        => await _context.Assistants
            .AsNoTracking()
            .Where(a => DbSearch.ArabicNormalize(a.User.FullName).Contains(normalizedTerm)
                     || (a.User.Username != null &&
                         DbSearch.ArabicNormalize(a.User.Username).Contains(normalizedTerm))
                     || (a.User.PhoneNumber != null && a.User.PhoneNumber.Contains(normalizedTerm)))
            // Working assistants first; a removed one still answers "who is this person?" for
            // support, which is what the search is for.
            .OrderBy(a => a.RemovedAt != null || a.DeletedAt != null)
            .ThenBy(a => a.User.FullName)
            .Take(take)
            .Select(a => new ConsoleSearchHit(
                "Assistant", a.Id, a.UserId, a.User.FullName,
                null, a.User.PhoneNumber, a.TeacherAccountId, a.Teacher.User.FullName))
            .ToListAsync(ct);

    // ════════════════════════════════════════════════════════════════════════
    // TEACHER PAGE — support lookups
    // ════════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<long, IReadOnlyList<ConsoleLoginEvent>>> GetLoginEventsAsync(
        IReadOnlyCollection<long> userIds, int takePerUser, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<long, IReadOnlyList<ConsoleLoginEvent>>();

        // ONE query for everyone on the account; the per-person cap is applied in memory because a
        // per-group TOP-N is the kind of SQL that silently falls back to client evaluation. These
        // rows are written on sign-in and sign-out only, so a person's log is small.
        var rows = await _context.UserLoginActivity
            .AsNoTracking()
            .Where(l => userIds.Contains(l.UserId))
            .OrderByDescending(l => l.CreateAt)
            .Select(l => new { l.Id, l.UserId, l.ActionType, l.CreateAt, l.DeviceOrBrowser, l.IpAddress })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ConsoleLoginEvent>)(takePerUser > 0 ? g.Take(takePerUser) : g)
                    .Select(r => new ConsoleLoginEvent(
                        r.Id, r.ActionType.ToString(), r.CreateAt, r.DeviceOrBrowser, r.IpAddress))
                    .ToList());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeacherUsageRow>> GetUsageRowsByIdsAsync(
        IReadOnlyCollection<long> teacherIds, CancellationToken ct = default)
    {
        if (teacherIds.Count == 0) return Array.Empty<TeacherUsageRow>();

        return await BuildRowQuery(DateTime.UtcNow)
            .Where(r => teacherIds.Contains(r.TeacherId))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<(int Students, int StudentsInClasses, int ActiveLinks, int BoundLinks)> GetSnapshotCountsAsync(
        long teacherId, CancellationToken ct = default)
    {
        var roster = await _context.TeacherStudents
            .AsNoTracking()
            .Where(s => s.TeacherId == teacherId)
            .GroupBy(s => 1)
            .Select(g => new
            {
                Students = g.Count(),
                InClasses = g.Count(s => s.SessionId != null)
            })
            .FirstOrDefaultAsync(ct);

        var links = await _context.StudentTeacherLinks
            .AsNoTracking()
            .Where(l => l.TeacherId == teacherId && l.LinkStatus == LinkStatus.Active)
            .GroupBy(l => 1)
            .Select(g => new
            {
                Active = g.Count(),
                Bound = g.Count(l => l.TeacherStudentId != null)
            })
            .FirstOrDefaultAsync(ct);

        return (roster?.Students ?? 0, roster?.InClasses ?? 0, links?.Active ?? 0, links?.Bound ?? 0);
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLoginHistoryStartAsync(CancellationToken ct = default)
        => await _context.UserLoginActivity
            .AsNoTracking()
            .OrderBy(l => l.CreateAt)
            .Select(l => (DateTime?)l.CreateAt)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConsoleModuleUsage>> GetModuleUsageAsync(
        long teacherId, DateOnly from30, CancellationToken ct = default)
    {
        // The day rows for this teacher, projected to just the ten module columns. Pulled rather
        // than aggregated in SQL because there are ten separate columns and no bitwise or dynamic
        // aggregate to fold them with — and a teacher's day rows are one per ACTIVE day, so even a
        // year of daily use is a few hundred tiny rows.
        var days = await _context.TeacherUsageDays
            .AsNoTracking()
            .Where(d => d.TeacherId == teacherId)
            .Select(d => new
            {
                d.ActivityDate,
                d.StudentWrites,
                d.SessionWrites,
                d.AttendanceWrites,
                d.PaymentWrites,
                d.VideoWrites,
                d.OnlineExamWrites,
                d.ExamHomeworkWrites,
                d.MessagingWrites,
                d.ParentPortalWrites,
                d.EventPaymentWrites,
            })
            .ToListAsync(ct);

        // Name → how to read that module's count off a day row. One list, so a module cannot be
        // measured in one place and forgotten in another.
        var readers = new (string Module, Func<dynamic, int> Count)[]
        {
            (nameof(UsageModules.Students), d => d.StudentWrites),
            (nameof(UsageModules.Sessions), d => d.SessionWrites),
            (nameof(UsageModules.Attendance), d => d.AttendanceWrites),
            (nameof(UsageModules.Payments), d => d.PaymentWrites),
            (nameof(UsageModules.Videos), d => d.VideoWrites),
            (nameof(UsageModules.OnlineExams), d => d.OnlineExamWrites),
            (nameof(UsageModules.ExamsHomework), d => d.ExamHomeworkWrites),
            (nameof(UsageModules.Messaging), d => d.MessagingWrites),
            (nameof(UsageModules.ParentPortal), d => d.ParentPortalWrites),
            (nameof(UsageModules.EventPayments), d => d.EventPaymentWrites),
        };

        var result = new List<ConsoleModuleUsage>(readers.Length);
        foreach (var (module, count) in readers)
        {
            DateOnly? lastUsed = null;
            int writes30 = 0, writesAll = 0, daysUsed = 0;

            foreach (var d in days)
            {
                int n = count(d);
                if (n <= 0) continue;

                writesAll += n;
                daysUsed++;
                if (d.ActivityDate >= from30) writes30 += n;
                if (lastUsed is null || d.ActivityDate > lastUsed) lastUsed = d.ActivityDate;
            }

            result.Add(new ConsoleModuleUsage(module, lastUsed, writes30, writesAll, daysUsed));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConsoleClassRow>> GetClassesForTeacherAsync(
        long teacherId, CancellationToken ct = default)
        => await _context.Sessions
            .AsNoTracking()
            .Where(s => s.TeacherId == teacherId)
            .OrderBy(s => s.SessionName)
            .Select(s => new ConsoleClassRow(
                s.Id,
                s.SessionName,
                s.SessionGroup == null ? null : s.SessionGroup.GroupName,
                s.SelectedDays,
                s.StartTime,
                s.DurationMinutes,
                s.StartDate,
                s.EndDate,
                // The students the CLASS holds, counted the same way the rest of the platform does:
                // through TeacherStudents.SessionId, the column the audience resolves through.
                _context.TeacherStudents.Count(ts => ts.SessionId == s.Id),
                _context.SessionOccurrences.Count(o => o.SessionId == s.Id)))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<ConsoleContentSummary> GetVideoSummaryAsync(
        long teacherId, int takeTitles, CancellationToken ct = default)
    {
        var query = _context.VideoAssets.AsNoTracking().Where(v => v.TeacherId == teacherId);

        int total = await query.CountAsync(ct);
        var latest = await query
            .OrderByDescending(v => v.CreateAt)
            .Select(v => new { v.Title, v.CreateAt })
            .Take(takeTitles)
            .ToListAsync(ct);

        return new ConsoleContentSummary(
            total,
            latest.FirstOrDefault()?.CreateAt,
            latest.Select(v => v.Title).ToList());
    }

    /// <inheritdoc />
    public async Task<ConsoleContentSummary> GetOnlineExamSummaryAsync(
        long teacherId, int takeTitles, CancellationToken ct = default)
    {
        var query = _context.OnlineExams.AsNoTracking().Where(e => e.TeacherId == teacherId);

        int total = await query.CountAsync(ct);
        var latest = await query
            .OrderByDescending(e => e.CreateAt)
            .Select(e => new { e.Title, e.CreateAt })
            .Take(takeTitles)
            .ToListAsync(ct);

        return new ConsoleContentSummary(
            total,
            latest.FirstOrDefault()?.CreateAt,
            latest.Select(e => e.Title).ToList());
    }

    /// <inheritdoc />
    public async Task<ConsoleContentSummary> GetAssignmentSummaryAsync(
        long teacherId, int takeTitles, CancellationToken ct = default)
    {
        var query = _context.AssignmentTemplates.AsNoTracking().Where(a => a.TeacherId == teacherId);

        int total = await query.CountAsync(ct);
        var latest = await query
            .OrderByDescending(a => a.CreateAt)
            .Select(a => new { a.Name, a.CreateAt })
            .Take(takeTitles)
            .ToListAsync(ct);

        return new ConsoleContentSummary(
            total,
            latest.FirstOrDefault()?.CreateAt,
            latest.Select(a => a.Name).ToList());
    }
}
