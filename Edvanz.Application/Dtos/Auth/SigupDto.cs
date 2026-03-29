using Edvanz.Application.Dtos.UserDto;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.Auth
{
    public class SigupDto: AddUserDto
    {
        #region teacher
        public List<long>? subjectIds { get; set; } = new();
        public int? studentCapacity { get; set; }
        public string? customSubject { get; set; }

        #endregion

        #region shared
        public string? languagePreference { get; set; }

        #endregion
    }
}
