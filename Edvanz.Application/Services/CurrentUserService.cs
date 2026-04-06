using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
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
        private readonly IUnitOfWork unitOfWork;
        private Assistant? _assistant; 
        private bool _teacherIdLoaded = false;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor,IUnitOfWork unitOfWork)
        {
            _httpContextAccessor = httpContextAccessor;
            this.unitOfWork = unitOfWork;
        }

        public long? UserId
        {
            get
            {
                var claim = _httpContextAccessor.HttpContext?.User
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value ;

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

        public async Task<Assistant?> GetAssistantDataAsync()
        {
            if (_teacherIdLoaded)
                return _assistant;

            _teacherIdLoaded = true;

            if (Role != "Assistant" || UserId == null)
                return null;

            return await unitOfWork.AssistantRepo.GetAssistantWithUserIdAsync(UserId.Value);
             
        }
    }
}
