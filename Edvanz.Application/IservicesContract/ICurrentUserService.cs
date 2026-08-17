using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface ICurrentUserService
    {
       public long? UserId { get; }
        public string? Username { get; }
        public string? Role { get; }
        public List<string> Permissions { get; }
        public Task<Assistant?> GetAssistantDataAsync();

        /// <summary>
        /// The teacher id a Center/CenterAssistant login is currently acting as, taken from the
        /// <c>X-Acting-Teacher-Id</c> request header (raw, UNVALIDATED). Null when absent/unparseable
        /// or the caller is not a center-tier login. Use <see cref="ResolveActingTeacherIdAsync"/>
        /// to get the VALIDATED (membership-checked) value.
        /// </summary>
        long? ActingTeacherId { get; }

        /// <summary>
        /// Loads the <see cref="Center"/> for a Center login, or the CenterAssistant's center for a
        /// CenterAssistant login; null otherwise. Result is cached for the request.
        /// </summary>
        Task<Center?> GetCenterDataAsync();

        /// <summary>
        /// For a Center/CenterAssistant login, returns the acting teacher id ONLY when it belongs to
        /// the caller's center (fail-closed membership check — honors §3.3/BUG-12). Returns null for
        /// non-center callers, when no acting teacher is selected, or when the teacher is not in the
        /// center. This is the single shared helper every tenant resolver delegates to.
        /// </summary>
        Task<long?> ResolveActingTeacherIdAsync();
    }
}
