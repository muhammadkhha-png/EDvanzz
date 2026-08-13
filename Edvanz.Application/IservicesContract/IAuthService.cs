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
        /// <summary>
        /// Self-service account deletion (Apple 5.1.1(v) / Google Play). The acting user
        /// is resolved from the JWT — never from the body. Disables login
        /// (<c>IsActive = false</c>), revokes every refresh token, and bumps the
        /// SecurityStamp so all live access tokens are rejected on their next request.
        /// The disabled account is retained for operational review and permanent purge
        /// within 30 days.
        /// </summary>
        public Task<Result<string>> DeleteMyAccount();
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

        /// <summary>
        /// Resolves the authenticated caller from their JWT and returns the same
        /// <see cref="AuthResponse"/> shape as <see cref="Login"/> - a fresh access token
        /// plus the current account profile. No credentials are accepted; <paramref name="userId"/>
        /// must already be resolved from the validated token's claims by the caller.
        /// </summary>
        /// <param name="userId">The caller's user id, resolved from the JWT <c>NameIdentifier</c> claim.</param>
        /// <returns>
        /// Success with a rebuilt <see cref="AuthResponse"/> (userAccountData reflects the
        /// account's CURRENT state - role, modules, permissions - not what was true when the
        /// token was originally issued). <c>refreshToken</c> is always <c>null</c>: unlike
        /// <see cref="Login"/>, this does not mint or persist a new refresh token. Failure
        /// (401) with "UserNotFound" or "AccountInactive" if the account behind the token no
        /// longer exists or has been deactivated since the token was issued.
        /// </returns>
        Task<Result<AuthResponse>> GetCurrentUserAsync(long userId);

    }
}
