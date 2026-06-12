using System.ComponentModel.DataAnnotations;

namespace Edvanz.Application.Dtos.Auth
{
    public class LogoutDto
    {
        [Required]
        public string token { get; set; }

        public bool logoutAllSessions { get; set; } = false;
    }
}