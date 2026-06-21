using Edvanz.Application.ServiceContract;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    public class TimeZoneService : ITimeZoneService
    {
        /// <inheritdoc />
        public DateTime ConvertUtcToLocal(DateTime utcDateTime, string? timeZoneId = null)
        {
            var tz = ResolveTimeZone(timeZoneId);
            var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
        }

        /// <inheritdoc />
        public DateTime ConvertLocalToUtc(DateTime localDateTime, string? timeZoneId = null)
        {
            var tz = ResolveTimeZone(timeZoneId);
            var local = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);

            // Guard the spring-forward gap: a local time that doesn't exist (e.g. Egypt's
            // midnight DST transition in late April) would otherwise throw. Nudge past the
            // standard 1-hour gap so a From=transition-date filter never 500s.
            if (tz.IsInvalidTime(local))
                local = local.AddHours(1);

            return TimeZoneInfo.ConvertTimeToUtc(local, tz);
        }

        /// <summary>
        /// Resolves an IANA timezone id to a <see cref="TimeZoneInfo"/>, falling back to
        /// Africa/Cairo when the id is null, empty, or not recognized by the host.
        /// </summary>
        private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
        {
            if (!string.IsNullOrWhiteSpace(timeZoneId) &&
                TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var tz))
            {
                return tz;
            }

            return EgyptTimeZone;
        }

        /// <summary>
        /// The default time zone for Egypt (Africa/Cairo).
        /// </summary>
        private static readonly TimeZoneInfo EgyptTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

        /// <inheritdoc />
        public DateTime GetTeacherLocalNow(long teacherId)
        {
            // teacherId is reserved for the future per-teacher timezone lookup documented on
            // ITimeZoneService. Until TeacherConfiguration exposes a configurable timezone,
            // every teacher resolves to Africa/Cairo. Routing through ConvertUtcToLocal keeps
            // the DST-gap handling and timezone resolution in a single place — when the
            // per-teacher lookup lands, only the timeZoneId argument here needs to change.
            return ConvertUtcToLocal(DateTime.UtcNow);
        }

        /// <inheritdoc />
        public DateTime GetTeacherLocalDate(long teacherId)
            => GetTeacherLocalNow(teacherId).Date;
    }
}
