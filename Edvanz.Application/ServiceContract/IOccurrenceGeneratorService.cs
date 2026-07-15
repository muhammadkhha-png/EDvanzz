using Edvanz.Domain.Entities;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Focused service for computing session occurrence dates from recurrence rules.
/// SRP: Separated from IAttendanceService because occurrence date computation
/// is a pure domain logic concern that other services (Session, Dashboard) also need.
///
/// Computes dates from the session's OccurrenceType, SelectedDays, MonthlyDayOfMonth,
/// StartDate, and EndDate fields without any database access.
///
/// REQ-ATT-001/002: Determines which dates a session occurs on.
/// REQ-ATT-005: Respects StartDate/EndDate boundaries.
/// </summary>
public interface IOccurrenceGeneratorService
{
    /// <summary>
    /// Computes all occurrence dates for a session within its active date range.
    /// Used when generating SessionOccurrence records after session creation or date change.
    /// </summary>
    /// <param name="session">The session entity with recurrence configuration.</param>
    /// <returns>List of dates the session occurs on, sorted ascending.</returns>
    IReadOnlyList<DateTime> ComputeOccurrenceDates(Session session);

    /// <summary>
    /// Computes each occurrence date together with its cross-session equivalence key
    /// (WeekStartDate = Saturday-anchored calendar week; DayPositionIndex = 1-based rank of the
    /// weekday within the session's sorted SelectedDays). This is the single source of truth for
    /// the keys persisted on <see cref="SessionOccurrence"/>; the migration backfill mirrors it.
    /// Monthly sessions yield WeekStartDate = 1st of the month and DayPositionIndex = 1.
    /// </summary>
    /// <param name="session">The session entity with recurrence configuration.</param>
    /// <returns>Occurrence keys, sorted ascending by date.</returns>
    IReadOnlyList<(DateTime Date, DateTime WeekStartDate, int DayPositionIndex)> ComputeOccurrenceKeys(Session session);

    /// <summary>
    /// Checks whether a session occurs on a specific date.
    /// REQ-ATT-001/002: Session eligibility check for a given day.
    /// </summary>
    /// <param name="session">The session entity.</param>
    /// <param name="date">The date to check.</param>
    /// <returns>True if the session occurs on the given date.</returns>
    bool OccursOnDate(Session session, DateTime date);
}