using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Edvanz.Application.Dtos.Auth
{
    public class SigUpByGoogle
    {
        [Required]
        public string clientDeviceToken { get; set; }
    }
}
