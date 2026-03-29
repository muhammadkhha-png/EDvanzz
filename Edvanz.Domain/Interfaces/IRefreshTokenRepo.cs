using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    public interface IRefreshTokenRepo : IGenericRepo<RefreshToken,long>
    {
        List<RefreshToken> GetByUserId(long userId);
        Task<RefreshToken?> GetUserByRefreshToken(string refreshToken);

    }
}
