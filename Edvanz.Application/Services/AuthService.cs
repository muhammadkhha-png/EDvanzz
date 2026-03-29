using Edvanz.Application.Dtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Edvanz.Domain.ServiceContract;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    /// <summary>
    /// Implements authentication operations including OTP verification.
    /// 
    /// All database access goes through IUnitOfWork.Users (IUserRepo) — no direct
    /// GetRepository calls with raw expression predicates.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStringLocalizer<Messages> _localizer;
        // FIX B5: Added IPasswordService dependency to properly verify hashed OTPs
        private readonly IPasswordService _passwordService;

        /// <summary>
        /// Initializes a new instance of AuthService with required dependencies.
        /// </summary>
        /// <param name="unitOfWork">Unit of work for database operations.</param>
        /// <param name="localizer">String localizer for multilingual messages.</param>
        /// <param name="passwordService">Password hashing and verification service.</param>
        public AuthService(
            IUnitOfWork unitOfWork,
            IStringLocalizer<Messages> localizer,
            IPasswordService passwordService)
        {
            _unitOfWork = unitOfWork;
            _localizer = localizer;
            _passwordService = passwordService;
        }

        /// <summary>
        /// Verifies an OTP code for a given phone number and marks the user as verified.
        /// 
        /// FIX B5: Previously compared plain-text OTP against the stored hashed OTP using
        /// string equality (user.OtpCode != otp), which ALWAYS fails because OtpService
        /// stores a hashed version. Now correctly uses IPasswordService.VerifyPassword()
        /// to compare the plain-text input against the hashed stored value.
        /// </summary>
        /// <param name="phone">The user's phone number.</param>
        /// <param name="otp">The plain-text OTP code entered by the user.</param>
        /// <returns>Result indicating success or failure of verification.</returns>
        public async Task<Result<string>> VerifyOtp(string phone, string otp)
        {
            var user = await _unitOfWork.Users.GetByPhoneAsync(phone);

            if (user == null)
                return Result<string>.Failure(_localizer, "UserNotFound");

            // Check if OTP exists and is not expired
            if (user.OtpCode == null || user.OtpExpiry == null)
                return Result<string>.Failure(_localizer, "OtpNotCreated");

            if (user.OtpExpiry < DateTime.UtcNow)
                return Result<string>.Failure(_localizer, "OtpExpired");

            // FIX B5: Use VerifyPassword to compare plain-text OTP against the hashed stored value.
            // Previously: user.OtpCode != otp — plain-text vs hash comparison, always fails.
            bool isOtpValid = _passwordService.VerifyPassword(user.OtpCode, otp);
            if (!isOtpValid)
                return Result<string>.Failure(_localizer, "Invalid or expired OTP");

            user.IsVerified = true;
            user.OtpCode = null;

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            return Result<string>.Success(null, _localizer, "Account Verified");
        }
    }
}