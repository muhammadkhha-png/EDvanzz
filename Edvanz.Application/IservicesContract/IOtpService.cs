using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Auth;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IOtpService
    {
        public string GenerateOtp();
        public bool IsOtpExpired(DateTime? expiryTime);
        public Task<Result<string>> AskForOtp(string phoneNumber);
        public Task<Result<string>> VerifyOtp(OtpVerificationDto req);
         
    }
}
