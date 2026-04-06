using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface ICurrentUserService
    {
       public long? UserId { get; }
        public string? Username { get; }
        public string? Role { get; }
        public List<string> Permissions { get; }
        public Task<Assistant?> GetAssistantDataAsync();
    }
}
