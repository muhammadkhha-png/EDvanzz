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
    /// Checks whether a session occurs on a specific date.
    /// REQ-ATT-001/002: Session eligibility check for a given day.
    /// </summary>
    /// <param name="session">The session entity.</param>
    /// <param name="date">The date to check.</param>
    /// <returns>True if the session occurs on the given date.</returns>
    bool OccursOnDate(Session session, DateTime date);
}