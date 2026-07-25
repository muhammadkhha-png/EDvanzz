using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Application.Dtos.UserDto
{
    using Microsoft.AspNetCore.Http;
    using System.ComponentModel.DataAnnotations;

    public class AddUserDto

    {
        [Required]
        public UserType userType { get; set; }

        [Required]
        public string fullName { get; set; }

        [Required]
        public string username { get; set; }

        [EmailAddress]
        public string? email { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        public string password { get; set; }

        [Required]
        public string confirmedPassword { get; set; }

        
        public string? phoneNumber { get; set; }

        public IFormFile? idImage { get; set; }
    }


}
