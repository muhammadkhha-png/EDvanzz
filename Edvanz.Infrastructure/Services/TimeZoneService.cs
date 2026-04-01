using Edvanz.Application.ServiceContract;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Provides timezone-aware date/time operations for teacher-scoped date comparisons.
/// FIX 1.5: Resolves the UTC midnight boundary bug where DateTime.UtcNow.Date
/// returns the wrong "today" for Egyptian tutors between midnight and 2 AM Cairo time.
///
/// CURRENT IMPLEMENTATION:
/// Defaults to "Africa/Cairo" (Egypt Standard Time) for all teachers since
/// the Edvanz platform exclusively targets Egyptian tutors.
///
/// FUTURE EXTENSION:
/// When TeacherConfiguration gains a Timezone field, inject IUnitOfWork and
/// look up the teacher's configured timezone from the database. The interface
/// already accepts teacherId to support this without breaking changes.
///
/// Lives in Infrastructure layer because timezone resolution is an infrastructure concern.
/// </summary>
public class TimeZoneService : ITimeZoneService
{
    /// <summary>
    /// The default timezone for all Edvanz teachers.
    /// Egypt Standard Time (UTC+2). Handles DST transitions automatically via TimeZoneInfo.
    /// </summary>
    private static readonly TimeZoneInfo EgyptTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    /// <inheritdoc />
    public DateTime GetTeacherLocalDate(long teacherId)
    {
        // FUTURE: Look up teacher-specific timezone from TeacherConfiguration.
        // For now, all teachers use Egypt timezone.
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EgyptTimeZone).Date;
    }

    /// <inheritdoc />
    public DateTime GetTeacherLocalNow(long teacherId)
    {
        // FUTURE: Look up teacher-specific timezone from TeacherConfiguration.
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EgyptTimeZone);
    }
}