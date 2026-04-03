using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security;
using System.Text;

namespace Edvanz.Application.Dtos.AssistantDtos
{
    public class CreateAssistantDto
    {
        [Required, MaxLength(150)]
        public string fullName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string username { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string password { get; set; } = string.Empty;
        public string? email { get; set; }
        public string? phoneNumber { get; set; }

        /// <summary>Zero or more profiles to assign immediately at creation</summary>
        public List<long>? permissionProfileIds { get; set; } = new();
        public List<long>? permissionIds { get; set; } = new();

        public long teacherId { get; set; }
        /// <summary>Individual permission overrides on top of the assigned profiles</summary>
        //public List<PermissionKey> Permissions { get; set; } = new();
    }
}
