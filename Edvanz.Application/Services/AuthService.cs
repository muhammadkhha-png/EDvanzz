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
        private readonly ITokenService tokenService;
        private readonly ICurrentUserService _currentUser;
        private readonly IUserPermissionService userPermissionService;
        private readonly IAssistantService assistantService;
        private readonly IModuleTeacherRepo moduleTeacherRepo;
        private readonly string _googleClientId = "528615365840-ha6qiocetc2sgu1349ecrb9vincdo5rt.apps.googleusercontent.com";
        private readonly IHttpContextAccessor _httpContextAccessor;


        /// <summary>
        /// Initializes a new instance of AuthService with required dependencies.
        /// </summary>
        /// <param name="unitOfWork">Unit of work for database operations.</param>
        /// <param name="localizer">String localizer for multilingual messages.</param>
        /// <param name="passwordService">Password hashing and verification service.</param>
        public AuthService(
            IUnitOfWork unitOfWork,
            IStringLocalizer<Messages> localizer,
            IPasswordService passwordService,ITokenService tokenService,ICurrentUserService currentUser,IUserPermissionService userPermissionService,IAssistantService assistantService,IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _localizer = localizer;
            _passwordService = passwordService;
            this.tokenService = tokenService;
            this._currentUser = currentUser;
            this.userPermissionService = userPermissionService;
            this.assistantService = assistantService;
            this.moduleTeacherRepo = moduleTeacherRepo;
            _httpContextAccessor = httpContextAccessor;
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

        public async Task<Result<AuthResponse>> Login(LoginDto req)
        {
            var user = await _unitOfWork.Users.GetByUserName(req.userName);
            if (user == null)
                return Result<AuthResponse>.Failure(_localizer, "UserNotFound");
            var IsPassMatched = _passwordService.VerifyPassword(user.PasswordHashed, req.password);
            if (!IsPassMatched)
                return Result<AuthResponse>.Failure(_localizer, "PasswordError");
            var permissions = await userPermissionService.GetUserPermissionsToToken(user.Id);
            string jwt = null;
            List<string> modules = new List<string>();
            if (user.UserType == Domain.Enums.UserType.Student || user.UserType == Domain.Enums.UserType.Parent)
                jwt = tokenService.GenerateJwtToken(user, permissions, null);

           else if (user.UserType == Domain.Enums.UserType.Teacher )
            {

                var teacher = await _unitOfWork.Users.GetTeacherByUserIdAsync(user.Id);
                var modulesPerTeacher=await _unitOfWork.ModuleTeacherRepo.GetModulesPerTeacher(teacher.Id);
                    modules = modulesPerTeacher.Select(mt => mt.Name).ToList();
                jwt = tokenService.GenerateJwtToken(user, permissions, modules);
            }
            else if (user.UserType == Domain.Enums.UserType.Assistant)
            {
                var assistant = await _unitOfWork.AssistantRepo.GetAssistantWithUserIdAsync(user.Id);
                if (assistant == null)
                    return Result<AuthResponse>.Failure(_localizer, "UserNotFound");

                
                if (assistant.TeacherAccountId == null)
                {
                    jwt = tokenService.GenerateJwtToken(user, permissions, null);
                }
                else
                {
                    var modulesPerTeacher = await _unitOfWork.ModuleTeacherRepo
                        .GetModulesPerTeacher(assistant.TeacherAccountId);
                    modules = modulesPerTeacher.Select(mt => mt.Name).ToList();
                    jwt = tokenService.GenerateJwtToken(user, permissions, modules);
                }
                await assistantService.RecordLoginActivityAsync(assistant.Id, LoginAcitvityActionType.login, _httpContextAccessor.HttpContext!);


            }
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

            await _unitOfWork.GetRepository<RefreshToken, long>().AddAsync(refreshTokenEntity);
          
            var res = await _unitOfWork.SaveChangesAsync();
            if (res <= 0)
                return Result<AuthResponse>.Failure(_localizer, "ServerError");

            return Result<AuthResponse>.Success(new AuthResponse
            {
                accessToken = jwt,
                refreshToken = refreshToken
            }, _localizer, "successlogin");
        }
        public async Task<Result<string>> ChangePassword(ChangePasswordDto req)
        {
            if (  _currentUser.UserId == null)
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

            var entities = _unitOfWork.RefreshTokenRepo.GetByUserId(userId);
            await _unitOfWork.GetRepository<RefreshToken, long>().DeleteRangeAsync(entities);

            var res = await _unitOfWork.SaveChangesAsync();
            if(res <= 0)
            {
                await _unitOfWork.RollbackAsync();
                return Result<string>.Failure(_localizer, "ServerError");

            }
            await _unitOfWork.CommitAsync();
            return Result<string>.Success(null,_localizer, "PasswordChangedSuccessfully");
        }

        public async Task<Result<AuthResponse>> Refresh(string refreshToken)
        {
            var token =await _unitOfWork.RefreshTokenRepo.GetUserByRefreshToken(refreshToken);
            if (token == null )
                return Result<AuthResponse>.Failure(_localizer, "notFoundToken");
            if (token.user == null || token.ExpiryDate <= DateTime.UtcNow)
                return Result<AuthResponse>.Failure(_localizer, "Unauthorized");
            var permissions = await userPermissionService.GetUserPermissionsToToken(token.UserId);


            var newAccessToken = tokenService.GenerateJwtToken(token.user, permissions,null);
            var newRefreshToken = tokenService.GenerateRefreshToken();

            token.Token = newRefreshToken;
            token.ExpiryDate = DateTime.UtcNow.AddDays(7);
            await _unitOfWork.RefreshTokenRepo.UpdateAsync(token);
          var res=  await _unitOfWork.SaveChangesAsync();
            if (res <= 0)
                return Result<AuthResponse>.Failure(_localizer, "ServerError");

            var result = new AuthResponse
            {
                accessToken = newAccessToken,
                refreshToken = newRefreshToken
            };
            return Result<AuthResponse>.Success(result, _localizer, "TokenRefreshedSuccessfully");
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

    }
}