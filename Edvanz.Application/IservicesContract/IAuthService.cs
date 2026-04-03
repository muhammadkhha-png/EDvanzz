using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Auth;
using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IAuthService
    {
        public Task<Result<string>> VerifyOtp(string phone, string otp);
        //public  Task<Result<AuthResponse>> Login(LoginDto user);
        public Task<Result<string>> ChangePassword(ChangePasswordDto req);
        public Task<Result<AuthResponse>> Refresh(string refreshToken);
        Task<Result<AuthResponse>> SigUpByGoogle(string idToken);

    }
}
