using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using static System.Collections.Specialized.BitVector32;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Computes session occurrence dates from recurrence rules (OccurrenceType, SelectedDays, etc.).
/// REQ-ATT-001/002: Determines which dates a session occurs on.
/// REQ-ATT-005: Respects StartDate/EndDate boundaries.
///
/// Pure computation — no database access, no side effects.
/// Uses the same day-of-week mapping as the Session Module:
///   0=Saturday, 1=Sunday, 2=Monday, 3=Tuesday, 4=Wednesday, 5=Thursday, 6=Friday
///
/// For BiWeekly: StartDate anchors the first week. Occurrences happen on selected days
/// of week 1, skip week 2, repeat.
/// </summary>
public class OccurrenceGeneratorService : IOccurrenceGeneratorService
{
    /// <summary>
    /// Maps the Edvanz day index (0=Saturday..6=Friday) to .NET DayOfWeek.
    /// </summary>
    private static readonly DayOfWeek[] DayIndexToDayOfWeek =
    {
        DayOfWeek.Saturday,  // 0
        DayOfWeek.Sunday,    // 1
        DayOfWeek.Monday,    // 2
        DayOfWeek.Tuesday,   // 3
        DayOfWeek.Wednesday, // 4
        DayOfWeek.Thursday,  // 5
        DayOfWeek.Friday     // 6
    };

    /// <inheritdoc />
    public IReadOnlyList<DateTime> ComputeOccurrenceDates(Session session)
    {
        return session.OccurrenceType switch
        {
            OccurrenceType.Weekly => ComputeWeeklyDates(session, biWeekly: false),
            OccurrenceType.BiWeekly => ComputeWeeklyDates(session, biWeekly: true),
            OccurrenceType.Monthly => ComputeMonthlyDates(session),
            _ => new List<DateTime>()
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<(DateTime Date, DateTime WeekStartDate, int DayPositionIndex)> ComputeOccurrenceKeys(Session session)
    {
        // Reuse the deterministic ascending date computation — the slot key is a pure function of
        // each date (+ SelectedDays for the position), so no running counter is needed and a missing
        // early occurrence never shifts the others.
        var dates = ComputeOccurrenceDates(session);
        var result = new List<(DateTime, DateTime, int)>(dates.Count);

        if (session.OccurrenceType == OccurrenceType.Monthly)
        {
            // One occurrence per month → bucket by the month, single slot.
            foreach (var d in dates)
                result.Add((d, new DateTime(d.Year, d.Month, 1), 1));
            return result;
        }

        // Weekly / BiWeekly: slot = (Saturday-anchored calendar week, rank of the weekday within
        // the session's sorted SelectedDays). appDayIndex uses the app's 0=Sat..6=Fri model.
        var selectedDays = ParseSelectedDays(session.SelectedDays)
            .Where(x => x >= 0 && x <= 6)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        foreach (var d in dates)
        {
            int appDayIndex = ((int)d.DayOfWeek + 1) % 7;               // Sat=0, Sun=1 … Fri=6
            DateTime weekStart = d.AddDays(-appDayIndex);               // that week's Saturday
            int dayPosition = selectedDays.Count(x => x <= appDayIndex); // 1-based rank (date is a selected day)
            result.Add((d, weekStart, dayPosition == 0 ? 1 : dayPosition));
        }

        return result;
    }

    /// <inheritdoc />
    public bool OccursOnDate(Session session, DateTime date)
    {
        // Quick boundary check
        if (date.Date < session.StartDate.Date || date.Date > session.EndDate.Date)
            return false;

        return session.OccurrenceType switch
        {
            OccurrenceType.Weekly => IsWeeklyOccurrence(session, date, biWeekly: false),
            OccurrenceType.BiWeekly => IsWeeklyOccurrence(session, date, biWeekly: true),
            OccurrenceType.Monthly => IsMonthlyOccurrence(session, date),
            _ => false
        };
    }

    // ══════════════════════════════════════════════
    // WEEKLY / BI-WEEKLY COMPUTATION
    // ══════════════════════════════════════════════

    /// <summary>
    /// Generates all dates for Weekly or BiWeekly occurrence within the session's date range.
    /// For BiWeekly, StartDate anchors the first week — occurrences happen every other week.
    /// </summary>
    private static List<DateTime> ComputeWeeklyDates(Session session, bool biWeekly)
    {
        var dates = new List<DateTime>();
        var selectedDays = ParseSelectedDays(session.SelectedDays);
        if (selectedDays.Count == 0)
            return dates;

        var targetDaysOfWeek = selectedDays
            .Where(d => d >= 0 && d <= 6)
            .Select(d => DayIndexToDayOfWeek[d])
            .ToHashSet();

        var current = session.StartDate.Date;
        var endDate = session.EndDate.Date;

        // Determine the "week number" relative to start date for biweekly calculation
        while (current <= endDate)
        {
            if (targetDaysOfWeek.Contains(current.DayOfWeek))
            {
                if (!biWeekly || IsInActiveWeek(session.StartDate.Date, current))
                {
                    dates.Add(current);
                }
            }
            current = current.AddDays(1);
        }

        return dates;
    }

    /// <summary>
    /// Checks if a specific date falls on an "active" week for BiWeekly sessions.
    /// The first week (containing StartDate) is always active, then alternating.
    /// </summary>
    private static bool IsInActiveWeek(DateTime startDate, DateTime checkDate)
    {
        // Calculate the number of weeks between start date and check date
        int daysDiff = (checkDate - startDate).Days;
        int weekNumber = daysDiff / 7;

        // Even week numbers (0, 2, 4...) are active weeks
        return weekNumber % 2 == 0;
    }

    /// <summary>
    /// Checks if a single date matches a Weekly/BiWeekly occurrence pattern.
    /// </summary>
    private static bool IsWeeklyOccurrence(Session session, DateTime date, bool biWeekly)
    {
        var selectedDays = ParseSelectedDays(session.SelectedDays);
        if (selectedDays.Count == 0)
            return false;

        var targetDaysOfWeek = selectedDays
            .Where(d => d >= 0 && d <= 6)
            .Select(d => DayIndexToDayOfWeek[d])
            .ToHashSet();

        if (!targetDaysOfWeek.Contains(date.DayOfWeek))
            return false;

        if (biWeekly && !IsInActiveWeek(session.StartDate.Date, date))
            return false;

        return true;
    }

    // ══════════════════════════════════════════════
    // MONTHLY COMPUTATION
    // ══════════════════════════════════════════════

    /// <summary>
    /// Generates all dates for Monthly occurrence within the session's date range.
    /// REQ-SES-007: Specific day of month (1-31). Handles months with fewer days gracefully.
    /// </summary>
    private static List<DateTime> ComputeMonthlyDates(Session session)
    {
        var dates = new List<DateTime>();
        if (!session.MonthlyDayOfMonth.HasValue)
            return dates;

        int targetDay = session.MonthlyDayOfMonth.Value;
        var startDate = session.StartDate.Date;
        var endDate = session.EndDate.Date;

        // Start from the month of the start date
        var current = new DateTime(startDate.Year, startDate.Month, 1);

        while (current <= endDate)
        {
            int daysInMonth = DateTime.DaysInMonth(current.Year, current.Month);
            int effectiveDay = Math.Min(targetDay, daysInMonth);
            var occurrenceDate = new DateTime(current.Year, current.Month, effectiveDay);

            if (occurrenceDate >= startDate && occurrenceDate <= endDate)
            {
                dates.Add(occurrenceDate);
            }

            // Move to next month
            current = current.AddMonths(1);
        }

        return dates;
    }

    /// <summary>
    /// Checks if a single date matches a Monthly occurrence pattern.
    /// </summary>
    private static bool IsMonthlyOccurrence(Session session, DateTime date)
    {
        if (!session.MonthlyDayOfMonth.HasValue)
            return false;

        int targetDay = session.MonthlyDayOfMonth.Value;
        int daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
        int effectiveDay = Math.Min(targetDay, daysInMonth);

        return date.Day == effectiveDay;
    }

    // ══════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Parses the comma-separated SelectedDays string into a list of day indices.
    /// Same logic as SessionService.ParseSelectedDays — DRY via shared utility.
    /// </summary>
    private static List<int> ParseSelectedDays(string? daysString)
    {
        if (string.IsNullOrWhiteSpace(daysString))
            return new List<int>();

        return daysString.Split(',')
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .ToList();
    }
}