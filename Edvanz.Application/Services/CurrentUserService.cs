using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private Center? _center;
        private bool _centerLoaded = false;

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

        /// <inheritdoc />
        public long? ActingTeacherId
        {
            get
            {
                var raw = _httpContextAccessor.HttpContext?
                    .Request.Headers[CenterConstants.ActingTeacherHeader].FirstOrDefault();
                return long.TryParse(raw, out var id) ? id : null;
            }
        }

        /// <inheritdoc />
        public async Task<Center?> GetCenterDataAsync()
        {
            if (_centerLoaded)
                return _center;

            _centerLoaded = true;

            if (UserId == null)
                return null;

            if (Role == UserRoles.Center)
                _center = await unitOfWork.Centers.GetCenterByUserIdAsync(UserId.Value);
            else if (Role == UserRoles.CenterAssistant)
                _center = (await unitOfWork.Centers.GetCenterAssistantByUserIdAsync(UserId.Value))?.Center;

            return _center;
        }

        /// <inheritdoc />
        public async Task<long?> ResolveActingTeacherIdAsync()
        {
            if (Role != UserRoles.Center && Role != UserRoles.CenterAssistant)
                return null;

            var actingTeacherId = ActingTeacherId;
            if (actingTeacherId == null)
                return null;

            var center = await GetCenterDataAsync();
            if (center == null)
                return null;

            // Fail-closed: the header value is trusted ONLY after confirming the teacher belongs to
            // this caller's center AND is not deactivated (the acting-as IDOR guard — §3.3 / BUG-12).
            // A deactivated teacher is unmanageable via acting-as until the center reactivates it.
            if (!await unitOfWork.Centers.IsActiveTeacherInCenterAsync(center.Id, actingTeacherId.Value))
                return null;

            return actingTeacherId;
        }
    }
}
