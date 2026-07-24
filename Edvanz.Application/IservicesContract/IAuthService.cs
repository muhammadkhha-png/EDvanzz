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
        public  Task<Result<AuthResponse>> Login(LoginDto user);
        /// <summary>
        /// Authenticates a SuperAdmin user. Verifies credentials first, THEN gates on
        /// <c>UserType.SuperAdmin</c> so the endpoint cannot be used to enumerate which
        /// usernames belong to which user type.
        ///
        /// Returns the same generic "InvalidCredentials" message for wrong-password and
        /// not-an-admin cases — clients cannot distinguish between them.
        /// </summary>
        /// <param name="req">Username + password.</param>
        /// <returns>
        /// Success with an <see cref="AuthResponse"/> when the user exists, the password
        /// matches, AND <c>UserType == SuperAdmin</c>. Failure otherwise.
        /// </returns>
        public Task<Result<AuthResponse>> AdminLoginAsync(LoginDto req);
        public Task<Result<string>> ChangePassword(ChangePasswordDto req);
        public Task<Result<AuthResponse>> Refresh(string refreshToken);
        Task<Result<AuthResponse>> SigUpByGoogle(string idToken);
        public  Task<Result<string>> Logout(string refreshToken, bool logoutAllSessions = false);
        Task<Result<AuthResponse>> CompleteProfile(CompleteProfileDto dto);
        /// <summary>
        /// SuperAdmin-only forced password reset. Skips old-password verification by
        /// design. Unconditionally revokes every refresh token for the target user and
        /// bumps their SecurityStamp, so all of the target's existing access tokens are
        /// invalidated on their next request.
        /// </summary>
        /// <param name="req">Target user id, new password, and confirmation.</param>
        /// <returns>
        /// Success on completion. Failure with "UserNotFound" if the target user id
        /// doesn't exist, or "PasswordConfirmationMismatch" if newPassword/confirmPassword differ.
        /// </returns>
        Task<Result<string>> ForceChangePasswordAsync(ForceChangePasswordDto req);

    }
}
