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

        /// <summary>
        /// Slides the idle deadline (<see cref="RefreshToken.ExpiryDate"/>) forward to
        /// <paramref name="newExpiryUtc"/> for the user's still-alive sessions (not revoked,
        /// not already past <paramref name="nowUtc"/>). Set-based single UPDATE — never revives
        /// an already-idled-out session. Returns the number of rows affected.
        /// </summary>
        Task<int> SlideActiveExpiryAsync(long userId, DateTime newExpiryUtc, DateTime nowUtc);
    }
}
