using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface ITokenService
    {
        public string GenerateJwtToken(User user, List<string>? permissions, List<string>? modules);
        public string GenerateRefreshToken();
       public string GenerateCompleteProfileToken(GoogleUser googleUser);
       
    }
}
