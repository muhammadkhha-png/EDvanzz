using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Auth;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Edvanz.Domain.ServiceContract;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
// FIX B6: Added cryptographic namespace for secure OTP generation
using System.Security.Cryptography;
using System.Text;

namespace Edvanz.Application.Services
{
    /// <summary>
    /// Implements OTP (One-Time Password) generation and verification operations.
    /// Handles the complete OTP lifecycle: generation, hashing, storage, and verification.
    /// 
    /// All database access goes through IUnitOfWork.Users (IUserRepo) — no direct
    /// GetRepository calls with raw expression predicates.
    /// </summary>
    public class OtpService : IOtpService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStringLocalizer<Messages> _localizer;
        private readonly IPasswordService _passwordService;

        /// <summary>
        /// Initializes a new instance of OtpService with required dependencies.
        /// </summary>
        /// <param name="unitOfWork">Unit of work for database operations.</param>
        /// <param name="localizer">String localizer for multilingual messages.</param>
        /// <param name="passwordService">Password hashing service used to hash OTP codes.</param>
        public OtpService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer, IPasswordService passwordService)
        {
            _unitOfWork = unitOfWork;
            _localizer = localizer;
            _passwordService = passwordService;
        }

        /// <inheritdoc />
        public async Task<Result<string>> AskForOtp(string phoneNumber)
        {
            var user = await _unitOfWork.Users.GetByPhoneAsync(phoneNumber);
            if (user == null)
                return Result<string>.Failure(_localizer, "NoUserFoundForThisPhone");

            var otp = GenerateOtp();
            var hashedOtp = _passwordService.HashPassword(otp);
            user.OtpCode = hashedOtp;
            // FIX B3: Use DateTime.UtcNow consistently — previously used DateTime.Now,
            // but IsOtpExpired and VerifyOtp compare against DateTime.UtcNow.
            // Mixing Now and UtcNow causes OTPs to expire early or late depending on server timezone.
            user.OtpExpiry = DateTime.UtcNow.AddMinutes(5);
            await _unitOfWork.Users.UpdateAsync(user);
            var res = await _unitOfWork.SaveChangesAsync();
            if (res > 0)
                return Result<string>.Success(otp, _localizer, "GeneratedOtpSucc");

            return Result<string>.Failure(_localizer, "internalServerError", System.Net.HttpStatusCode.InternalServerError);
        }

        /// <summary>
        /// Generates a cryptographically secure 6-digit OTP code.
        /// AAM-NFR-03: Codes must be cryptographically unique and collision-resistant.
        /// </summary>
        /// <returns>A 6-digit numeric string.</returns>
        // FIX B6: Replaced new Random() with RandomNumberGenerator for cryptographic security.
        // new Random() is seeded by time and is predictable — not suitable for security codes.
        public string GenerateOtp()
        {
            // Generate a cryptographically secure random number between 100000 and 999999
            int otpValue = RandomNumberGenerator.GetInt32(100000, 1000000);
            return otpValue.ToString();
        }

        /// <summary>
        /// Checks whether an OTP has expired based on its expiry timestamp.
        /// </summary>
        /// <param name="expiryTime">The UTC expiry timestamp of the OTP.</param>
        /// <returns>True if the OTP is expired or if expiryTime is null; false otherwise.</returns>
        public bool IsOtpExpired(DateTime? expiryTime)
        {
            if (!expiryTime.HasValue)
                return true;

            return DateTime.UtcNow > expiryTime.Value;
        }

        /// <inheritdoc />
        public async Task<Result<string>> VerifyOtp(OtpVerificationDto req)
        {
            var user = await _unitOfWork.Users.GetByPhoneAsync(req.phoneNumber);
            if (user == null)
                return Result<string>.Failure(_localizer, "UserNotFound");

            if (user.OtpCode == null || user.OtpExpiry == null)
                return Result<string>.Failure(_localizer, "OtpNotCreated");

            var isExpired = IsOtpExpired(user.OtpExpiry);
            if (isExpired)
                return Result<string>.Failure(_localizer, "OtpExpired");

            var comparisionResult = _passwordService.VerifyPassword(user.OtpCode, req.otp);
            if (!comparisionResult)
                return Result<string>.Failure(_localizer, "NotMatchedOtp");

            user.IsVerified = true;

            await _unitOfWork.Users.UpdateAsync(user);
            var isUpdated = await _unitOfWork.SaveChangesAsync();
            // FIX B4: SaveChangesAsync() returns the number of rows affected (0 or positive).
            // It NEVER returns negative values. Previously used "< 0" which would never be true,
            // silently treating save failures as success. Now correctly checks "== 0".
            if (isUpdated <= 0)
                return Result<string>.Failure(_localizer, "ServerError", System.Net.HttpStatusCode.InternalServerError);

            return Result<string>.Success(null, _localizer, "accountVerified");
        }
    }
}