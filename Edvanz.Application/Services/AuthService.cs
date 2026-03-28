using Edvanz.Application.Dtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    public class AuthService:IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStringLocalizer<Messages> localizer;

        public AuthService(IUnitOfWork unitOfWork, IStringLocalizer<Edvanz.Domain.Resources.Messages> localizer)
        {
            _unitOfWork = unitOfWork;
            this.localizer = localizer;
        }
        public async Task<Result<string>> VerifyOtp(string phone, string otp)
        {
            var user = await _unitOfWork.Users.GetByPhoneAsync(phone);

            if (user == null)
                return Result<string>.Failure(localizer, "UserNotFound");

            if (user.OtpCode != otp || user.OtpExpiry < DateTime.UtcNow)
                return Result<string>.Failure(localizer, "Invalid or expired OTP");

            user.IsVerified = true;
            user.OtpCode = null;

            await _unitOfWork.SaveChangesAsync();

            return Result<string>.Success(null,localizer,"Account Verified");
        }
    }
}
