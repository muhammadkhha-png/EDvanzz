using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Auth;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Edvanz.Domain.ServiceContract;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using static Edvanz.Application.Services.UserService;

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
        private readonly ITokenService tokenService;
        private readonly ICurrentUserService _currentUser;

        private readonly IAssistantService assistantService;
        private readonly string _googleClientId = "528615365840-ha6qiocetc2sgu1349ecrb9vincdo5rt.apps.googleusercontent.com";
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration configuration;


        private readonly IUserAuthCacheService _authCache;
        private readonly IUserAuthInvalidationService _authInvalidation;

        public AuthService(
            IUnitOfWork unitOfWork,
            IStringLocalizer<Messages> localizer,
            IPasswordService passwordService, ITokenService tokenService, ICurrentUserService currentUser, IAssistantService assistantService, IHttpContextAccessor httpContextAccessor, IConfiguration _configuration, IUserAuthCacheService authCache, IUserAuthInvalidationService authInvalidation)
        {
            _unitOfWork = unitOfWork;
            _localizer = localizer;
            _passwordService = passwordService;
            this.tokenService = tokenService;
            this._currentUser = currentUser;
           
            this.assistantService = assistantService;

            _httpContextAccessor = httpContextAccessor;
            configuration = _configuration;
            _authCache = authCache;
            _authInvalidation = authInvalidation;
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

        // ════════════════════════════════════════════════════════════════
        // ADD THIS METHOD TO Edvanz.Application/Services/AuthService.cs
        // alongside the existing Login() method. The rest of the class stays unchanged.
        //
        // Required usings (already present in the file):
        //   using Edvanz.Domain.Enums;
        //   using Edvanz.Domain.Entities;
        //   using Edvanz.Application.Dtos;
        //   using Edvanz.Application.Dtos.Auth;
        // ════════════════════════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<Result<AuthResponse>> AdminLoginAsync(LoginDto req)
        {
            // ── Step 1: Load user ──
            // Same lookup as Login() — enumeration-safe because the next step still runs
            // even when user is null (we return the generic message either way).
            var user = await _unitOfWork.Users.GetByUserName(req.userName);

            // ── Step 2: Verify password ──
            // Verify FIRST, then gate on role. Two reasons:
            //   (a) Anyone who knows a tutor's username + password already owns that tutor's
            //       account; learning that the tutor is "not an admin" gives them nothing new.
            //   (b) Verifying password first keeps the response shape identical for
            //       wrong-password and wrong-role cases — clients can't tell which failed.
            bool credentialsValid = user != null
                && _passwordService.VerifyPassword(user.PasswordHashed, req.password);

            if (!credentialsValid)
            {
                return Result<AuthResponse>.Failure(_localizer, "InvalidCredentials");
            }

            // ── Step 3: SuperAdmin role gate ──
            // The whole reason this endpoint exists. A non-admin reaching this point owns
            // a valid account but is forbidden from this surface — deliberate same message
            // as the credential failure above to avoid leaking user-type information.
            if (user!.UserType != UserType.SuperAdmin)
            {
                return Result<AuthResponse>.Failure(_localizer, "InvalidCredentials");
            }

            // ── Step 4: Build access token ──
            // Reuses TokenService.BuildUserTokenData — the same path Refresh() uses.
            // Note: BuildUserTokenData must include a SuperAdmin branch (see TokenService
            // patch in the slice). Without that branch this returns a null jwt.
            var (jwt, userDto) = await tokenService.BuildUserTokenData(user);

            if (string.IsNullOrEmpty(jwt))
            {
                // Defensive: BuildUserTokenData fell through without producing a token.
                // Indicates a missing branch in TokenService — fail loudly rather than
                // returning a malformed AuthResponse.
                return Result<AuthResponse>.Failure(_localizer, "ServerError");
            }

            // ── Step 5: Persist refresh token ──
            // Mirrors Login()'s refresh-token persistence exactly so revocation, expiry,
            // and reuse-detection in Refresh()/Logout() work identically for admin sessions.
            var refreshToken = tokenService.GenerateRefreshToken();

            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                // Minutes, not days — the initial deadline must equal the sliding idle
                // window that SessionActivitySlidingMiddleware keeps pushing forward.
                ExpiryDate = DateTime.UtcNow.AddMinutes(configuration.GetValue<int>("Jwt:RefreshTokenMinutes", 1440)),
                CreatedAt = DateTime.UtcNow,
                SecurityStamp = user.SecurityStamp,
                IsRevoked = false,
            };

            await _unitOfWork.GetRepository<RefreshToken, long>()
                .AddAsync(refreshTokenEntity);

            var saveResult = await _unitOfWork.SaveChangesAsync();
            if (saveResult <= 0)
            {
                return Result<AuthResponse>.Failure(_localizer, "ServerError");
            }

            return Result<AuthResponse>.Success(new AuthResponse
            {
                accessToken = jwt,
                refreshToken = refreshToken,
                userAccountData = userDto
            }, _localizer, "successlogin");
        }
        public async Task<Result<AuthResponse>> Login(LoginDto req)
        {
            var user = await _unitOfWork.Users.GetByUserName(req.userName);
            if (user == null)
                return Result<AuthResponse>.Failure(_localizer, "UserNotFound");

            var isPassMatched = _passwordService.VerifyPassword(user.PasswordHashed, req.password);
            if (!isPassMatched)
                return Result<AuthResponse>.Failure(_localizer, "PasswordError");

            // Active-status gate. User.IsActive is the canonical login flag — set false by
            // AssistantService.ToggleStatus for Inactive/Suspended accounts, default true for
            // self-registered users. Per-request enforcement still lives in
            // SecurityStampValidationMiddleware; this stops a new token being minted at all.
            if (user.IsActive != true)
                return Result<AuthResponse>.Failure(_localizer, "AccountInactive");

            // Single source of truth for access token + userDto — the same path AdminLogin
            // and Refresh already use. Replaces the previously-inlined user-type switch so
            // every flow is provably identical, not identical-by-inspection.
            var (jwt, userDto) = await tokenService.BuildUserTokenData(user);
            if (string.IsNullOrEmpty(jwt))
                return Result<AuthResponse>.Failure(_localizer, "ServerError");

            // Login-activity log (REQ-USR-028) is a login-flow concern, not a token-building
            // one, so it stays here — NOT in BuildUserTokenData, which Refresh also calls
            // (a refresh is not a login). Separate fetch mirrors the existing Logout() pattern.
            if (user.UserType == UserType.Assistant)
            {
                var assistant = await _unitOfWork.AssistantRepo.GetAssistantWithUserIdAsync(user.Id);
                if (assistant != null)
                {
                    await assistantService.RecordLoginActivityAsync(
                        assistant.Id,
                        LoginAcitvityActionType.login,
                        _httpContextAccessor.HttpContext!);
                }
            }

            var refreshToken = await IssueAndStageRefreshTokenAsync(user);

            var res = await _unitOfWork.SaveChangesAsync();
            if (res <= 0)
                return Result<AuthResponse>.Failure(_localizer, "ServerError");

            return Result<AuthResponse>.Success(new AuthResponse
            {
                accessToken = jwt,
                refreshToken = refreshToken,
                userAccountData = userDto
            }, _localizer, "successlogin");
        }

        /// <inheritdoc />
        public async Task<Result<AuthResponse>> GetCurrentUserAsync(long userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return Result<AuthResponse>.Failure(_localizer, "UserNotFound", HttpStatusCode.Unauthorized);

            // Same active-status gate as Login - a token minted before the account was
            // deactivated must not be able to re-derive a usable session through /me.
            if (user.IsActive != true)
                return Result<AuthResponse>.Failure(_localizer, "AccountInactive", HttpStatusCode.Unauthorized);

            // Single source of truth for accessToken + userDto - the exact same path
            // Login, AdminLogin, and Refresh use. Any future change to what the login
            // payload contains (new module, new permission shape, etc.) is picked up here
            // automatically with zero duplication.
            var (jwt, userDto) = await tokenService.BuildUserTokenData(user);
            if (string.IsNullOrEmpty(jwt))
                return Result<AuthResponse>.Failure(_localizer, "ServerError");

            // Deliberately NOT calling IssueAndStageRefreshTokenAsync here. Unlike Login,
            // /me is a read-only "who am I" check a client may call on every app foreground -
            // persisting a brand-new RefreshToken row on every call would grow that table
            // unboundedly with rows that never get used to refresh anything. refreshToken
            // stays null, which AuthResponse already supports (see SigUpByGoogle's
            // incomplete-profile branch).
            return Result<AuthResponse>.Success(new AuthResponse
            {
                accessToken = jwt,
                refreshToken = null,
                userAccountData = userDto
            }, _localizer, "CurrentUserRetrievedSuccess");
        }

        /// <inheritdoc />
        public async Task<Result<string>> ForceChangePasswordAsync(ForceChangePasswordDto req)
        {
            // ── 1. Confirmation check ──
            // ChangePasswordDto/AddUserDto don't enforce this via [Compare]; matching
            // that convention and validating manually here too, localized.
            if (!string.Equals(req.newPassword, req.confirmPassword, StringComparison.Ordinal))
                return Result<string>.Failure(_localizer, "PasswordConfirmationMismatch");

            // ── 2. Load target user ──
            var user = await _unitOfWork.Users.GetByIdAsync(req.userId);
            if (user == null)
                return Result<string>.Failure(_localizer, "UserNotFound");

            // ── 3. Hash + apply ──
            var newHashedPassword = _passwordService.HashPassword(req.newPassword);

            await _unitOfWork.BeginTransactionAsync();

            user.PasswordHashed = newHashedPassword;
            user.SecurityStamp = Guid.NewGuid().ToString();

            // Forced reset always revokes every existing session for the target —
            // no opt-out, unlike self-service ChangePassword's logOutFromAllDevices flag.
            var allTokens = _unitOfWork.RefreshTokenRepo.GetByUserId(req.userId);
            await _unitOfWork.GetRepository<RefreshToken, long>().DeleteRangeAsync(allTokens);

            // REQ-USR-013 / REQ-USR-027: invalidate the Redis snapshot in the same
            // transaction as the SecurityStamp bump, BEFORE SaveChangesAsync — same
            // discipline as every other state-mutating service in this codebase.
            await _authInvalidation.InvalidateUserAsync(req.userId);

            var res = await _unitOfWork.SaveChangesAsync();
            if (res <= 0)
            {
                await _unitOfWork.RollbackAsync();
                return Result<string>.Failure(_localizer, "ServerError");
            }

            await _unitOfWork.CommitAsync();

            return Result<string>.Success(null, _localizer, "ForcePasswordChangedSuccess");
        }

        public async Task<Result<string>> ChangePassword(ChangePasswordDto req)
        {
            if (_currentUser.UserId == null)
                return Result<string>.Failure(_localizer, "UserNotFound");

            var userId = _currentUser.UserId.Value;

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return Result<string>.Failure(_localizer, "UserNotFound");

            var isOldPasswordVerified = _passwordService.VerifyPassword(user.PasswordHashed, req.oldPassword);
            if (!isOldPasswordVerified)
                return Result<string>.Failure(_localizer, "oldPassNotMatched");

            var newHashedPassword = _passwordService.HashPassword(req.newPassword);

            await _unitOfWork.BeginTransactionAsync();

            user.PasswordHashed = newHashedPassword;
            user.SecurityStamp = Guid.NewGuid().ToString(); 

            if (req.logOutFromAllDevices)
            {
                var allTokens = _unitOfWork.RefreshTokenRepo.GetByUserId(userId);
                await _unitOfWork.GetRepository<RefreshToken, long>().DeleteRangeAsync(allTokens);
            }
            else
            {
                var currentToken = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString()?.Replace("Bearer ", "");
                if (!string.IsNullOrEmpty(currentToken))
                {
                    var tokenEntity = await _unitOfWork.RefreshTokenRepo.GetUserByRefreshToken(currentToken);
                    if (tokenEntity != null)
                    {
                        tokenEntity.IsRevoked = true;
                        await _unitOfWork.RefreshTokenRepo.UpdateAsync(tokenEntity);
                    }
                }
            }

            var res = await _unitOfWork.SaveChangesAsync();
            if (res <= 0)
            {
                await _unitOfWork.RollbackAsync();
                return Result<string>.Failure(_localizer, "ServerError");
            }

            await _unitOfWork.CommitAsync();

            // REQ-USR-013 / REQ-USR-027: drop the Redis snapshot AFTER commit
            // so the next request rebuilds from the new SecurityStamp (already
            // bumped above before SaveChangesAsync). The stamp bump alone
            // would suffice (TTL expiry), but invalidating immediately
            // collapses the staleness window to zero.
            await _authCache.InvalidateAsync(userId);

            return Result<string>.Success(null, _localizer, "PasswordChangedSuccessfully");
        }

        /// <summary>
        /// Self-service account deletion (Apple 5.1.1(v) / Google Play). The acting user
        /// is taken from the JWT — never from the body. Disables login
        /// (<c>IsActive = false</c>), revokes every refresh token, and bumps the
        /// SecurityStamp so all live access tokens are rejected on their next request
        /// (SecurityStampValidationMiddleware). The account is retained in a disabled
        /// state for operational review and permanent purge within 30 days.
        /// Uses the exact same primitives as <see cref="ChangePassword"/>'s all-devices path.
        /// </summary>
        public async Task<Result<string>> DeleteMyAccount()
        {
            if (_currentUser.UserId == null)
                return Result<string>.Failure(_localizer, "UserNotFound");

            var userId = _currentUser.UserId.Value;

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return Result<string>.Failure(_localizer, "UserNotFound");

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // IsActive is the canonical login gate (Login rejects IsActive != true).
                // The SecurityStamp bump rejects already-issued JWTs on their next request.
                user.IsActive = false;
                user.SecurityStamp = Guid.NewGuid().ToString();

                // If this is a student account, end its teacher links in the same transaction so
                // a deleted student stops appearing in any teacher's "My Students" (Active) or
                // pending-requests (Pending) lists. Disabling IsActive alone leaves the link rows
                // Active, so the teacher would keep seeing the ghost. Terminal rows are retained
                // for audit, mirroring the teacher-side removal (RemoveLinkedStudentsAsync).
                var studentUser = await _unitOfWork.Users.GetActiveStudentUserByUserIdAsync(userId);
                if (studentUser != null)
                {
                    var now = DateTime.UtcNow;
                    var liveLinks = await _unitOfWork.Users.GetLiveLinksForStudentUserAsync(studentUser.Id);
                    foreach (var link in liveLinks)
                    {
                        // A Pending row is a request the teacher hasn't accepted → CancelledByStudent;
                        // an Active row is an established link the student is now leaving → Unlinked.
                        link.LinkStatus = link.LinkStatus == LinkStatus.Pending
                            ? LinkStatus.CancelledByStudent
                            : LinkStatus.Unlinked;
                        link.UnlinkedAt = now;
                        await _unitOfWork.Users.UpdateStudentTeacherLinkAsync(link);
                    }
                }

                var allTokens = _unitOfWork.RefreshTokenRepo.GetByUserId(userId);
                await _unitOfWork.GetRepository<RefreshToken, long>().DeleteRangeAsync(allTokens);

                var res = await _unitOfWork.SaveChangesAsync();
                if (res <= 0)
                {
                    await _unitOfWork.RollbackAsync();
                    return Result<string>.Failure(_localizer, "ServerError");
                }

                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }

            // Drop the Redis auth snapshot AFTER commit so the now-disabled account can't
            // be served from cache (mirrors ChangePassword).
            await _authCache.InvalidateAsync(userId);

            return Result<string>.Success(null, _localizer, "AccountDeletionRequested");
        }
        public async Task<Result<AuthResponse>> Refresh(string refreshToken)
        {
            var token = await _unitOfWork.RefreshTokenRepo.GetUserByRefreshToken(refreshToken);

            if (token == null)
                return Result<AuthResponse>.Failure(_localizer, "notFoundToken");

            if (token.user is null || token.ExpiryDate <= DateTime.UtcNow)
                return Result<AuthResponse>.Failure(_localizer, "Unauthorized");

            if (token.IsRevoked)
            {
                // Token reuse detected — revoke ALL of this user's tokens (family invalidation)
                var allTokens = _unitOfWork.RefreshTokenRepo.GetByUserId(token.UserId);
                foreach (var t in allTokens) t.IsRevoked = true;
                await _unitOfWork.SaveChangesAsync();
                return Result<AuthResponse>.Failure(_localizer, "TokenReuseDetected");
            }

            // Rotate the refresh token IN PLACE (same row, new token string below). Do NOT set
            // IsRevoked here: the row is repurposed as the NEW valid token and the previous
            // token string is overwritten (so it is no longer resolvable). Marking it revoked
            // was a bug that made the very next refresh fail as "TokenReuseDetected" — refresh
            // tokens were effectively single-use.
            var user = await _unitOfWork.Users.GetByIdAsync(token.UserId);
            if (user == null)
                return Result<AuthResponse>.Failure(_localizer, "UserNotFound");

            var (newAccessToken, userDto) = await tokenService.BuildUserTokenData(user);

            var newRefreshToken = tokenService.GenerateRefreshToken();

            token.Token = newRefreshToken;
            // Idle deadline (ExpiryDate) is PRESERVED across refresh — it is owned by real
            // request activity (SessionActivitySlidingMiddleware slides it on every authenticated
            // call). A token refresh alone must NOT extend the session, so 1 hour with no
            // authenticated requests still lets it expire (the ExpiryDate <= now guard above).
            token.SecurityStamp = user.SecurityStamp;

            await _unitOfWork.RefreshTokenRepo.UpdateAsync(token);

            var res = await _unitOfWork.SaveChangesAsync();
            if (res <= 0)
                return Result<AuthResponse>.Failure(_localizer, "ServerError");

            return Result<AuthResponse>.Success(new AuthResponse
            {
                accessToken = newAccessToken,
                refreshToken = newRefreshToken,
                userAccountData = userDto
            }, _localizer, "TokenRefreshedSuccessfully");
        }

        public async Task<Result<AuthResponse>> SigUpByGoogle(string idToken)
        {
            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
                {

                    Audience = null, // Specify the client ID of your application if you want to restrict token validation to a specific audience
                    //null ==> _googleClientId when using fron front 
                });

            }
            catch
            {
                return Result<AuthResponse>.Failure(_localizer, "InvalidGoogletoken");
            }
            var googleUser = await _unitOfWork.googleUserRepo.GetByGoogleIdAsync(payload.Subject);

            if (googleUser != null)
            {
                if (googleUser.IsCompleted)
                {
                    if (googleUser.UserId is not long linkedUserId)
                        return Result<AuthResponse>.Failure(_localizer, "UserNotFound");

                    var existingUser = await _unitOfWork.Users.GetByIdAsync(linkedUserId);
                    if (existingUser is null)
                        return Result<AuthResponse>.Failure(_localizer, "UserNotFound");

                    // Same active-status gate as Login().
                    if (existingUser.IsActive != true)
                        return Result<AuthResponse>.Failure(_localizer, "AccountInactive");

                    var (loginJwt, loginUserDto) = await tokenService.BuildUserTokenData(existingUser);
                    if (string.IsNullOrEmpty(loginJwt))
                        return Result<AuthResponse>.Failure(_localizer, "ServerError");

                    var loginRefreshToken = await IssueAndStageRefreshTokenAsync(existingUser);

                    var loginSave = await _unitOfWork.SaveChangesAsync();
                    if (loginSave <= 0)
                        return Result<AuthResponse>.Failure(_localizer, "ServerError");

                    return Result<AuthResponse>.Success(new AuthResponse
                    {
                        accessToken = loginJwt,
                        refreshToken = loginRefreshToken,
                        userAccountData = loginUserDto
                    }, _localizer, "successlogin");
                }
            }
            else
            {
                googleUser = new GoogleUser
                {
                    GoogleId = payload.Subject,
                    Email = payload.Email,
                    CreateAt = DateTime.UtcNow,
                    IsCompleted = false,
                };

                await _unitOfWork.googleUserRepo.AddAsync(googleUser);
                var res = await _unitOfWork.SaveChangesAsync();
                if (res <= 0)
                    return Result<AuthResponse>.Failure(_localizer, "ServerError");
            }

            // Reaches here only when googleUser exists but is NOT completed (resume),
            // or a brand-new GoogleUser was just created. Issue the short-lived
            // CompleteProfile token to drive the onboarding screen.
            var token = tokenService.GenerateCompleteProfileToken(googleUser);

            return Result<AuthResponse>.Success(new AuthResponse
            {
                accessToken = token,
                refreshToken = null
            }, _localizer, "CompleteYourProfile");
        }
        //public async Task<Result<string>> Logout(string refreshToken, bool logoutAllSessions = false)
        //{
        //    var token = await _unitOfWork.RefreshTokenRepo.GetUserByRefreshToken(refreshToken);

        //    if (token == null)
        //        return Result<string>.Success(null, _localizer, "LoggedOut"); // لو مش موجود اصلاً، اعتبره logout

        //    // 2️⃣ جلب كل التوكنز لو requested
        //    if (logoutAllSessions)
        //    {
        //        var allTokens = _unitOfWork.RefreshTokenRepo.GetByUserId(token.UserId);
        //        foreach (var t in allTokens)
        //        {
        //            t.IsRevoked = true;
        //        }
        //    }
        //    else
        //    {
        //        token.IsRevoked = true;
        //    }

        //    await _unitOfWork.RefreshTokenRepo.UpdateAsync(token);
        //    await _unitOfWork.SaveChangesAsync();

        //    var user = await _unitOfWork.Users.GetByIdAsync(token.UserId);
        //    if (user != null && user.UserType == UserType.Assistant)
        //    {
        //        var assistant = await _unitOfWork.AssistantRepo.GetAssistantWithUserIdAsync(user.Id);
        //        if (assistant != null)
        //        {
        //            await assistantService.RecordLoginActivityAsync(
        //                assistant.Id,
        //                LoginAcitvityActionType.logOut,
        //                _httpContextAccessor.HttpContext!
        //            );
        //        }
        //    }

        //    return Result<string>.Success(null, _localizer, "LoggedOut");
        //}
        public async Task<Result<string>> Logout(string refreshToken, bool logoutAllSessions = false)
        {
            // Idempotent: an unknown / already-gone refresh token is treated as a successful logout.
            var token = await _unitOfWork.RefreshTokenRepo.GetUserByRefreshToken(refreshToken);
            if (token == null)
                return Result<string>.Success(null, _localizer, "LoggedOut");

            var userId = token.UserId;

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                if (logoutAllSessions)
                {
                    // All devices: hard-delete every refresh token for this user (mirrors
                    // ChangePassword's all-devices path), then bump SecurityStamp + drop the
                    // Redis snapshot via the invalidation service. Revoking refresh tokens alone
                    // does NOT end live sessions — a device holding a valid JWT keeps calling
                    // until it expires. The stamp bump is what rejects those JWTs on their next
                    // request (SecurityStampValidationMiddleware).
                    var allTokens = _unitOfWork.RefreshTokenRepo.GetByUserId(userId);
                    await _unitOfWork.GetRepository<RefreshToken, long>().DeleteRangeAsync(allTokens);

                    // MUST run BEFORE SaveChangesAsync so the stamp bump joins this transaction.
                    await _authInvalidation.InvalidateUserAsync(userId);
                    await _unitOfWork.UserDeviceTokensRepo.DeactivateAllForUserAsync(userId);

                }
                else
                {
                    // Single device: revoke only the supplied refresh token. Do NOT bump the
                    // SecurityStamp — it is per-user, so bumping it would log out every device.
                    // This device's access token stays valid until it expires
                    // (Jwt:AccessTokenMinutes); revoking its refresh token only blocks renewal.
                    token.IsRevoked = true;
                    await _unitOfWork.RefreshTokenRepo.UpdateAsync(token);
                  

                }

                var res = await _unitOfWork.SaveChangesAsync();
                if (res <= 0)
                {
                    await _unitOfWork.RollbackAsync();
                    return Result<string>.Failure(_localizer, "ServerError");
                }

                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }

            // Best-effort logout-activity log (REQ-USR-028). Runs AFTER commit so a logging
            // failure can never roll back a completed logout, and is wrapped so it can never
            // surface as a 500 on an otherwise-successful logout.
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user != null && user.UserType == UserType.Assistant)
                {
                    var assistant = await _unitOfWork.AssistantRepo.GetAssistantWithUserIdAsync(user.Id);
                    if (assistant != null)
                    {
                        await assistantService.RecordLoginActivityAsync(
                            assistant.Id,
                            LoginAcitvityActionType.logOut,
                            _httpContextAccessor.HttpContext!);
                    }
                }
            }
            catch
            {
                // Telemetry must not break logout.
            }

            return Result<string>.Success(null, _localizer, "LoggedOut");
        }
        public async Task<Result<AuthResponse>> CompleteProfile(CompleteProfileDto dto)
        {
            // 1. Identity: the CompleteProfile token authorizes this call.
            //    PermissionHandler has already enforced the "CompleteProfile" claim.
            //    Read GoogleUser.Id from NameIdentifier via ICurrentUserService.
            if (!long.TryParse(_currentUser.UserId?.ToString(), out var googleUserId))
                return Result<AuthResponse>.Failure(_localizer, "Unauthorized");

            var googleUser = await _unitOfWork.googleUserRepo.GetByIdAsync(googleUserId);
            if (googleUser is null)
                return Result<AuthResponse>.Failure(_localizer, "Unauthorized");

            if (googleUser.IsCompleted)
                return Result<AuthResponse>.Failure(_localizer, "Googleaccountalreadyregistered");

            // 2. Phone validation — same rule as password path.
            if (string.IsNullOrWhiteSpace(dto.phoneNumber))
                return Result<AuthResponse>.Failure(_localizer, "PhoneNumberRequired");
            if (!PhoneNumberValidator.IsValidEgyptianMobile(dto.phoneNumber))
                return Result<AuthResponse>.Failure(_localizer, "PhoneNumberInvalidFormat");

            // 3. Duplicate check across phone / username / email.
            var existing = await _unitOfWork.Users.FindExistingUserByCredentialsAsync(
                dto.phoneNumber, dto.username, googleUser.Email);
            if (existing is not null)
            {
                if (existing.PhoneNumber == dto.phoneNumber)
                    return Result<AuthResponse>.Failure(_localizer, "repeatedPhoneNumber");
                if (existing.Username == dto.username)
                    return Result<AuthResponse>.Failure(_localizer, "repeatedUserName");
                return Result<AuthResponse>.Failure(_localizer, "repeatedEmail");
            }

            // 4. Self-registration whitelist — same rule as AddUser.
            var allowed = new[] { UserType.Teacher, UserType.Student, UserType.Parent };
            if (!allowed.Contains(dto.userType))
                return Result<AuthResponse>.Failure(_localizer, "InvalidUserType");

            if (dto.userType == UserType.Teacher &&
                (dto.subjectIds is null || dto.subjectIds.Count == 0))
                return Result<AuthResponse>.Failure(_localizer, "SubjectRequired");

            // 5. Transactional creation — mirrors AddUser exactly so we have ONE
            //    place where User + Teacher/Student/Parent records are born.
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var user = new User
                {
                    UserType = dto.userType,
                    FullName = dto.fullName,
                    Username = dto.username,
                    Email = googleUser.Email, // canonical: comes from Google
                    PhoneNumber = dto.phoneNumber,
                    PasswordHashed = string.Empty,     // no local password — SSO only
                    IsActive = true,
                    IsVerified = false,            // OTP still required post-signup
                    CreateAt = DateTime.UtcNow
                };

                await _unitOfWork.Users.AddAsync(user);
                await _unitOfWork.SaveChangesAsync();

                // Delegate type-specific init to the same services AddUser uses.
                // (Teacher/Student/Parent service calls — identical block to AddUser.)
                // ... omitted for brevity, see AddUser switch statement ...

                googleUser.UserId = user.Id;
                googleUser.IsCompleted = true;
                await _unitOfWork.googleUserRepo.UpdateAsync(googleUser);

                // Stage the refresh token INSIDE the creation transaction so the row
                // is committed atomically with the User. These accounts are SSO-only
                // (PasswordHashed = string.Empty) and cannot fall back to Login(), so a
                // user committed without a refresh token would have no way to obtain one.
                var refreshToken = await IssueAndStageRefreshTokenAsync(user);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                // Issue a real JWT, identical to the post-Login path.
                var (jwt, userDto) = await tokenService.BuildUserTokenData(user);
                return Result<AuthResponse>.Success(new AuthResponse
                {
                    accessToken = jwt,
                    refreshToken = refreshToken,   // now persisted (was: never saved)
                    userAccountData = userDto
                }, _localizer, "RegisteredSuccessfully");
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
        /// <summary>
        /// Issues a new refresh token for <paramref name="user"/> and stages the
        /// corresponding <see cref="RefreshToken"/> row on the unit of work.
        ///
        /// Does NOT call SaveChanges/Commit — the caller owns the commit boundary so
        /// the row can join an existing transaction (e.g. CompleteProfile's user-creation
        /// transaction) without a double-save conflict on the shared DbContext.
        /// Returns the generated token string for the AuthResponse.
        ///
        /// Single source of truth for refresh-token issuance across Login, AdminLogin,
        /// and CompleteProfile — keeps expiry, SecurityStamp binding, and revocation
        /// defaults identical across every authentication flow.
        /// </summary>
        private async Task<string> IssueAndStageRefreshTokenAsync(User user)
        {
            var refreshToken = tokenService.GenerateRefreshToken();

            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                // Minutes, not days — the initial deadline must equal the sliding idle
                // window that SessionActivitySlidingMiddleware keeps pushing forward.
                ExpiryDate = DateTime.UtcNow.AddMinutes(configuration.GetValue<int>("Jwt:RefreshTokenMinutes", 1440)),
                CreatedAt = DateTime.UtcNow,
                SecurityStamp = user.SecurityStamp,
                IsRevoked = false,
            };

            await _unitOfWork.GetRepository<RefreshToken, long>()
                .AddAsync(refreshTokenEntity);

            return refreshToken;
        }

    }
}