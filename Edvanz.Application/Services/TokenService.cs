using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Auth;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Edvanz.Application.Services
{
    public class TokenService : ITokenService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStringLocalizer<Messages> _localizer;
        private readonly IUserPermissionService userPermissionService;
        private readonly IConfiguration configuration;
        private readonly string apiKey;
        public TokenService( IUnitOfWork _unitOfWork,IStringLocalizer<Messages> _localizer,IUserPermissionService userPermissionService, IConfiguration configuration)
        {
            this._unitOfWork = _unitOfWork;
            this._localizer = _localizer;
            this.userPermissionService = userPermissionService;
            this.configuration = configuration;
            this.apiKey = configuration["Jwt:Key"];
        }

        public string GenerateJwtToken(User user, List<string>? permissions,List<string>? modules)
        {
            var claims = new List<Claim>();

            if (user.Id != 0)
                claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));

            if (!string.IsNullOrWhiteSpace(user.Username))
                claims.Add(new Claim(ClaimTypes.Name, user.Username));

            if (user.UserType != null)
                claims.Add(new Claim(ClaimTypes.Role, user.UserType.ToString()));

            if (modules != null && modules.Any())
            {
                claims.AddRange(
                    modules
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .Select(m => new Claim("module", m))
                );
            }
            if (permissions != null && permissions.Any())
            {
                claims.AddRange(
                    permissions
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => new Claim("Permission", p))
                );
            }
           
          

            var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(apiKey)), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                 issuer: configuration["Jwt:Issuer"],
    audience: configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(configuration.GetValue<int>("Jwt:AccessTokenMinutes")),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        public string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

            return Convert.ToBase64String(randomBytes);
        }
        public string GenerateCompleteProfileToken(GoogleUser googleUser)
        {
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, googleUser.Id.ToString()),
        new Claim(ClaimTypes.Email, googleUser.Email ?? ""),
       
        new Claim("Permission", "CompleteProfile") 
    };

           

            var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(apiKey)), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(10), 
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        public async Task<(string jwt, UserLoginDto userDto)> BuildUserTokenData(User user)
        {
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
                (_, teacherIds) = await _unitOfWork.studentTeacherLinkRepo
                    .GetSudentAccountLinkedTeacherIdsByUserId(user.Id);

                userDto.teacherIds = teacherIds;

                jwt = GenerateJwtToken(user, null, null);
            }
            else if (user.UserType == Domain.Enums.UserType.Parent)
            {
                jwt = GenerateJwtToken(user, null, null);

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

                jwt = GenerateJwtToken(user, permissions, modules);
            }
            else if (user.UserType == Domain.Enums.UserType.Assistant)
            {
                var assistant = await _unitOfWork.AssistantRepo
                    .GetAssistantWithUserIdAsync(user.Id);

                if (assistant == null)
                    throw new Exception("Assistant not found");

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

                jwt = GenerateJwtToken(user, permissions, modules);
            }
            else if (user.UserType == Domain.Enums.UserType.SuperAdmin)
            {
                // SuperAdmin has no teacher and no per-tutor module list — they administer
                // the platform itself. Permissions are still resolved (a SuperAdmin can still
                // be enrolled in granular permissions if your model supports it) but no
                // module claims and no teacherIds are emitted on the token.
                userDto.models = null;
                userDto.teacherIds = null;
                userDto.permissions = null;

                jwt = GenerateJwtToken(user, permissions, null);
            }

            #endregion

            return (jwt, userDto);
        }

    }
}
