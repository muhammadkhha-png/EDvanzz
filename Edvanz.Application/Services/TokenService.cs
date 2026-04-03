using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities;
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
        
        public string GenerateJwtToken(User user, List<string>? permissions,List<string>? modules)
        {
            var claims = new List<Claim>();

            if (user.Id != 0)
                claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));

            if (!string.IsNullOrWhiteSpace(user.Username))
                claims.Add(new Claim(ClaimTypes.Name, user.Username));

            if (user.UserType != null)
                claims.Add(new Claim(ClaimTypes.Role, user.UserType.ToString()));

            if (!string.IsNullOrWhiteSpace(user.SecurityStamp))
                claims.Add(new Claim("SecurityStamp", user.SecurityStamp));
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

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("SUPER_SECRET_KEY_EDVanz_edvanzz_OMRANBELAL")
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(5),
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

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("SUPER_SECRET_KEY_EDVanz_edvanzz_OMRANBELAL")
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(10), 
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
