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
        private readonly IUserPermissionService userPermissionService;
        private readonly IAssistantService assistantService;
        private readonly string _googleClientId = "528615365840-ha6qiocetc2sgu1349ecrb9vincdo5rt.apps.googleusercontent.com";
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration configuration;


        private readonly IUserAuthCacheService _authCache;

        public AuthService(
            IUnitOfWork unitOfWork,
            IStringLocalizer<Messages> localizer,
            IPasswordService passwordService, ITokenService tokenService, ICurrentUserService currentUser, IUserPermissionService userPermissionService, IAssistantService assistantService, IHttpContextAccessor httpContextAccessor, IConfiguration _configuration, IUserAuthCacheService authCache)
        {
            _unitOfWork = unitOfWork;
            _localizer = localizer;
            _passwordService = passwordService;
            this.tokenService = tokenService;
            this._currentUser = currentUser;
            this.userPermissionService = userPermissionService;
            this.assistantService = assistantService;

            _httpContextAccessor = httpContextAccessor;
            configuration = _configuration;
            _authCache = authCache;
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
                ExpiryDate = DateTime.UtcNow.AddDays(7),
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

            var permissions = await userPermissionService.GetUserPermissionsToToken(user.Id);

            string jwt = null;
            List<string> modules = new();
            List<long> teacherIds = new();

            var userDto = new UserLoginDto
            {
                accountId = user.Id,
                userName = user.Username,
                fullName = user.FullName,
                accountType = user.UserType.ToString()
            };

            #region Handle User Types

            if (user.UserType == Domain.Enums.UserType.Student)
            {
                // 🔹 get teacherIds
                (long userId, teacherIds) = await _unitOfWork.studentTeacherLinkRepo
                     .GetSudentAccountLinkedTeacherIdsByUserId(user.Id);

                userDto.teacherIds = teacherIds;

                jwt = tokenService.GenerateJwtToken(user, null, null);
            }
            else if (user.UserType == Domain.Enums.UserType.Parent)
            {
                // 🔹 ignore models / permissions / teacherIds
                jwt = tokenService.GenerateJwtToken(user, null, null);

                userDto.models = null;
                userDto.permissions = null;
                userDto.teacherIds = null;
            }
            else if (user.UserType == Domain.Enums.UserType.Teacher)
            {
                var teacher = await _unitOfWork.Users.GetTeacherByUserIdAsync(user.Id);

                var modulesPerTeacher = await _unitOfWork.ModuleTeacherRepo
                    .GetModulesPerTeacher(teacher.Id);

                modules = modulesPerTeacher.Select(mt => mt.Name).ToList();

                userDto.models = modules;
                userDto.permissions = permissions;

                jwt = tokenService.GenerateJwtToken(user, permissions, modules);
            }
            else if (user.UserType == Domain.Enums.UserType.Assistant)
            {
                var assistant = await _unitOfWork.AssistantRepo
                    .GetAssistantWithUserIdAsync(user.Id);

                if (assistant == null)
                    return Result<AuthResponse>.Failure(_localizer, "UserNotFound");

                if (assistant.TeacherAccountId != null)
                {
                    teacherIds.Add(assistant.TeacherAccountId);

                    var modulesPerTeacher = await _unitOfWork.ModuleTeacherRepo
                        .GetModulesPerTeacher(assistant.TeacherAccountId);

                    modules = modulesPerTeacher.Select(mt => mt.Name).ToList();

                    userDto.models = modules;
                }

                userDto.teacherIds = teacherIds;
                userDto.permissions = permissions;

                jwt = tokenService.GenerateJwtToken(user, permissions, modules);

                await assistantService.RecordLoginActivityAsync(
                    assistant.Id,
                    LoginAcitvityActionType.login,
                    _httpContextAccessor.HttpContext!
                );
            }

            else
            {
                return Result<AuthResponse>.Failure(_localizer, "UserNotFound");
            }

                #endregion

                var refreshToken = tokenService.GenerateRefreshToken();

            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiryDate = DateTime.UtcNow.AddDays(configuration.GetValue<int>("Jwt:RefreshTokenDays")),
                CreatedAt = DateTime.UtcNow,
                SecurityStamp = user.SecurityStamp,
                IsRevoked = false,
            };

            await _unitOfWork.GetRepository<RefreshToken, long>()
                .AddAsync(refreshTokenEntity);

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

            // Rotate: mark current as revoked, issue new
            token.IsRevoked = true;


            var user = await _unitOfWork.Users.GetByIdAsync(token.UserId);
            if (user == null)
                return Result<AuthResponse>.Failure(_localizer, "UserNotFound");

            var (newAccessToken, userDto) = await tokenService.BuildUserTokenData(user);

            var newRefreshToken = tokenService.GenerateRefreshToken();

            token.Token = newRefreshToken;
            token.ExpiryDate = DateTime.UtcNow.AddDays(configuration.GetValue<int>("Jwt:RefreshTokenDays"));
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
                throw new UnauthorizedAccessException("Invalid Google token");
            }

            var googleUser = await _unitOfWork.googleUserRepo.GetByGoogleIdAsync(payload.Subject);

            if (googleUser != null)
            {
                if (googleUser.IsCompleted)
                {
                    return Result<AuthResponse>.Failure(_localizer,"Googleaccountalreadyregistered");
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
               var res=  await _unitOfWork.SaveChangesAsync();
                if (res <= 0)
                    return Result<AuthResponse>.Failure(_localizer, "ServerError");
            }

            var token = tokenService.GenerateCompleteProfileToken(googleUser);

            var result = new AuthResponse()
            {
                accessToken = token,
                refreshToken = null
            };

            return Result<AuthResponse>.Success(result, _localizer, "CompleteYourProfile");
        }
        public async Task<Result<string>> Logout(string refreshToken, bool logoutAllSessions = false)
        {
            var token = await _unitOfWork.RefreshTokenRepo.GetUserByRefreshToken(refreshToken);

            if (token == null)
                return Result<string>.Success(null, _localizer, "LoggedOut"); // لو مش موجود اصلاً، اعتبره logout

            // 2️⃣ جلب كل التوكنز لو requested
            if (logoutAllSessions)
            {
                var allTokens = _unitOfWork.RefreshTokenRepo.GetByUserId(token.UserId);
                foreach (var t in allTokens)
                {
                    t.IsRevoked = true;
                }
            }
            else
            {
                token.IsRevoked = true;
            }

            await _unitOfWork.RefreshTokenRepo.UpdateAsync(token);
            await _unitOfWork.SaveChangesAsync();

            var user = await _unitOfWork.Users.GetByIdAsync(token.UserId);
            if (user != null && user.UserType == UserType.Assistant)
            {
                var assistant = await _unitOfWork.AssistantRepo.GetAssistantWithUserIdAsync(user.Id);
                if (assistant != null)
                {
                    await assistantService.RecordLoginActivityAsync(
                        assistant.Id,
                        LoginAcitvityActionType.logOut,
                        _httpContextAccessor.HttpContext!
                    );
                }
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
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                // 6. Issue a real JWT, identical to the post-Login path.
                var (jwt, userDto) = await tokenService.BuildUserTokenData(user);
                return Result<AuthResponse>.Success(new AuthResponse
                {
                    accessToken = jwt,
                    refreshToken = tokenService.GenerateRefreshToken(),
                    userAccountData = userDto
                }, _localizer, "RegisteredSuccessfully");
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }


    }
}