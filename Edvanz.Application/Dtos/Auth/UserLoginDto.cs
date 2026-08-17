using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.Auth
{
    public class UserLoginDto
    {
        public long accountId { get; set; }
        public string? userName { get; set; }
        public string? fullName { get; set; }
        public string accountType { get; set; }
        public List<string>? models { get; set; }
        public List<string>? permissions { get; set; }
        public List<long> teacherIds { get; set; }

        // ── Center tier (populated only for accountType Center / CenterAssistant) ──
        /// <summary>The Center this login belongs to (Center owner or CenterAssistant); null otherwise.
        /// Lets the app route to center mode and preload the acting-as teacher switcher.</summary>
        public long? centerId { get; set; }

        /// <summary>Display name of the center; null for non-center logins.</summary>
        public string? centerName { get; set; }
    }
}
