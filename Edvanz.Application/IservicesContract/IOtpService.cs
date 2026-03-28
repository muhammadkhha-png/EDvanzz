using Edvanz.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IOtpService
    {
        public string GenerateOtp();
        public Task<Result<string>> AskForOtp(string phoneNumber);
         
    }
}
