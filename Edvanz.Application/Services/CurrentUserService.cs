using Edvanz.Application.IservicesContract;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace Edvanz.Application.Services
{
    public class CurrentUserService:ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public long? UserId
        {
            get
            {
                var claim = _httpContextAccessor.HttpContext?.User
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                return long.TryParse(claim, out var id) ? id : null;
            }
        }

        public string? Username =>
            _httpContextAccessor.HttpContext?.User
                .FindFirst(ClaimTypes.Name)?.Value;

        public string? Role =>
            _httpContextAccessor.HttpContext?.User
                .FindFirst(ClaimTypes.Role)?.Value;

        public List<string> Permissions =>
            _httpContextAccessor.HttpContext?.User.Claims
                .Where(c => c.Type == "Permission")
                .Select(c => c.Value)
                .ToList() ?? new List<string>();
    }
}
