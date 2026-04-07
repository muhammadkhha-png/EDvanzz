using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.Auth
{
    public class AuthResponse
    {
        public string? refreshToken { get; set; }
        public string accessToken { get; set; }
        public UserLoginDto userAccountData { get; set; }
    }
}
