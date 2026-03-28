using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Edvanz.Application.Dtos
{
 
   public class OtpVerificationDto
    {
        [Required]
        public string phoneNumber { get; set; }
        [Required]
        public string otp { get; set; }
    }
}
