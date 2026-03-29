using Edvanz.Domain.ServiceContract;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    /// <summary>
    /// Implements password hashing and verification using ASP.NET Core's PasswordHasher.
    /// Uses PBKDF2 with HMAC-SHA256 under the hood, which is suitable for production use.
    /// 
    /// Used by:
    /// - UserService: to hash passwords during registration.
    /// - OtpService: to hash OTP codes before storage and verify them during validation.
    /// - AuthService: to verify OTP codes during authentication.
    /// </summary>
    public class PasswordService : IPasswordService
    {
        private readonly PasswordHasher<object> _hasher;

        /// <summary>
        /// Initializes a new instance of PasswordService with a default PasswordHasher.
        /// </summary>
        public PasswordService()
        {
            _hasher = new PasswordHasher<object>();
        }

        /// <summary>
        /// Hashes a plain-text password using PBKDF2 with HMAC-SHA256.
        /// The resulting hash includes the salt, iteration count, and algorithm version
        /// so it can be verified later without storing the salt separately.
        /// </summary>
        /// <param name="password">The plain-text password to hash.</param>
        /// <returns>The hashed password string.</returns>
        public string HashPassword(string password)
        {
            return _hasher.HashPassword(null, password);
        }

        /// <summary>
        /// Verifies a plain-text password against a previously hashed password.
        /// Extracts the salt and parameters from the hash and re-hashes the provided password
        /// to check for a match.
        /// </summary>
        /// <param name="hashedPassword">The stored hashed password.</param>
        /// <param name="providedPassword">The plain-text password to verify.</param>
        /// <returns>True if the provided password matches the hash; false otherwise.</returns>
        public bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            var result = _hasher.VerifyHashedPassword(null, hashedPassword, providedPassword);
            return result == PasswordVerificationResult.Success;
        }
    }
}