using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Edvanz.Application.Dtos.Auth
{
    public class RefeshDto
    {
        [Required]
        public string token { get; set; }
    }
}
