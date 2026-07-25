using System.ComponentModel.DataAnnotations;

namespace Edvanz.Application.Dtos.Auth
{
    /// <summary>
    /// REQ-AUTH-XXX: SuperAdmin-initiated forced password reset.
    /// No old-password verification — that is the entire distinction from the
    /// self-service <see cref="ChangePasswordDto"/> flow. The acting admin is
    /// resolved from the JWT (never from the body); the target account is
    /// identified by <see cref="userId"/>.
    /// </summary>
    public class ForceChangePasswordDto
    {
        [Required]
        public long userId { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        public string newPassword { get; set; }

        [Required]
        public string confirmPassword { get; set; }
    }
}