using System;

namespace Edvanz.Application.Dtos.AssistantDtos
{
    /// <summary>
    /// Assistant row for the Admin Portal's platform-wide assistant list.
    /// Same shape as <see cref="AssistantListDto"/>, plus <see cref="userId"/>
    /// (Admin Portal needs the underlying User.Id for account-level actions —
    /// deactivate/reset — that the per-teacher list has no reason to expose).
    /// </summary>
    public class AssistantAdminListDto
    {
        public long id { get; set; }
        public long userId { get; set; }
        public string fullName { get; set; } = null!;
        public string username { get; set; } = null!;
        public string? email { get; set; }
        public string phoneNumber { get; set; } = null!;
        public bool isActive { get; set; }
        public long teacherId { get; set; }
        public string teacherName { get; set; } = null!;
        public DateTime createdAt { get; set; }
        public string accountStatus { get; set; } = null!;
        public DateTime? deletedAt { get; set; }
        public DateTime updatedAt { get; set; }
        public string? languagePreference { get; set; }

        /// <summary>
        /// UTC timestamp of the assistant's most recent successful login, or null if the
        /// account has never logged in. Sourced from User.LastLoginAt (Activity Monitor).
        /// </summary>
        public DateTime? lastLoginAt { get; set; }

        /// <summary>
        /// UTC "last seen" — most recent authenticated request, stamped by
        /// SessionActivitySlidingMiddleware (±5-minute throttle). Null until the account's
        /// first request after the LastActivityAt column shipped.
        /// </summary>
        public DateTime? lastActivityAt { get; set; }
    }
}