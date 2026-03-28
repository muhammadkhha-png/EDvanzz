using Edvanz.Domain.ServiceContract;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    public class PasswordService:IPasswordService
    {
        private readonly PasswordHasher<object> _hasher;

        public PasswordService()
        {
            _hasher = new PasswordHasher<object>();
        }

        public string HashPassword(string password)
        {
            return _hasher.HashPassword(null, password);
        }

        public bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            var result = _hasher.VerifyHashedPassword(null, hashedPassword, providedPassword);
            return result == PasswordVerificationResult.Success;
        }
    }
}
