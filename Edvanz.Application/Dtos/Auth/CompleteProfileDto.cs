using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Edvanz.Application.Dtos.Auth
{
    public class CompleteProfileDto
    {
        [Required] public UserType userType { get; set; }
        [Required] public string fullName { get; set; } = null!;
        [Required] public string username { get; set; } = null!;
        [Required] public string phoneNumber { get; set; } = null!;
        public string? languagePreference { get; set; }

        // Teacher-only — same shape as SigupDto
        public List<long>? subjectIds { get; set; }
        public int? studentCapacity { get; set; }
        public string? customSubject { get; set; }
    }
}
