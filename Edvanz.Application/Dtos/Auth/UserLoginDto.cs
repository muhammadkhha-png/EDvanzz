using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.Auth
{
    public class UserLoginDto
    {
        public long accountId { get; set; }
        public string? userName { get; set; }
        public string? fullName { get; set; }
        public string accountType { get; set; }
        public List<string>? models { get; set; }
        public List<string>? permissions { get; set; }
        public List<long> teacherIds { get; set; }
    }
}
