using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Edvanz.Application.Dtos.Auth
{
    public class ChangePasswordDto
    {
        [Required]
        public string oldPassword { get; set; }
        [Required]
        public string newPassword { get; set; }
        [Required]
        public string confirmPassword { get; set; }

        /// <summary>OBSOLETE — kept only so older clients' bodies still bind. A password change now
        /// ALWAYS signs out every other device; this flag is ignored (see currentRefreshToken).</summary>
        public bool logOutFromAllDevices { get; set; }

        /// <summary>The calling device's own refresh token. When it matches one of the user's live
        /// refresh-token rows, that row is KEPT so THIS device silently re-authenticates after the
        /// SecurityStamp bump (401 → interceptor refresh) while every other device is signed out.
        /// Absent/unknown (older clients) → every session is revoked and the user signs in again.</summary>
        public string? currentRefreshToken { get; set; }
    }
}
