using Edvanz.Domain.Entities;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Provides timezone-aware date/time operations for teacher-scoped date comparisons.
/// FIX 1.5: All "today" comparisons must use the teacher's local timezone,
/// not DateTime.UtcNow.Date, which is wrong at midnight boundary for Egypt (UTC+2).
///
/// RATIONALE:
/// Edvanz targets Egyptian tutors. A teacher taking attendance at 1 AM Cairo time
/// would see DateTime.UtcNow.Date as the previous day (11 PM UTC). This service
/// ensures all date comparisons use the teacher's local timezone.
///
/// FUTURE: When teacher timezone becomes configurable in TeacherConfiguration,
/// this service will read the timezone from the database. For now, defaults to
/// "Africa/Cairo" (Egypt Standard Time, UTC+2 / UTC+3 during DST).
/// </summary>
public interface ITimeZoneService
{
    /// <summary>
    /// Gets the current local date for a teacher based on their timezone.
    /// Used for "today's sessions" detection, dashboard date, and attendance date defaulting.
    /// </summary>
    /// <param name="teacherId">The teacher's Id (for future per-teacher timezone lookup).</param>
    /// <returns>The teacher's current local date (date-only, no time component).</returns>
    DateTime GetTeacherLocalDate(long teacherId);

    /// <summary>
    /// Gets the current local date and time for a teacher based on their timezone.
    /// Used for timestamp comparisons where both date and time matter.
    /// </summary>
    /// <param name="teacherId">The teacher's Id (for future per-teacher timezone lookup).</param>
    /// <returns>The teacher's current local date and time.</returns>
    DateTime GetTeacherLocalNow(long teacherId);
}