using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Infrastructure.Repositories
{
    public class RefreshTokenRepo : GenericRepo<RefreshToken, long>, IRefreshTokenRepo
    {
        public RefreshTokenRepo(EdvanzDbContext context) : base(context)
        {
        }

        public async Task<RefreshToken?> GetUserByRefreshToken(string refreshToken)
        {
            return await _context.RefreshTokens.Include(u=>u.user).FirstOrDefaultAsync(u => u.Token == refreshToken);
        }

        public  List<RefreshToken> GetByUserId(long userId)
        {
         return _context.RefreshTokens.Where(r=>r.UserId == userId).ToList();
        }

        public async Task<int> SlideActiveExpiryAsync(long userId, DateTime newExpiryUtc, DateTime nowUtc)
        {
            // Slide only still-alive sessions forward; the ExpiryDate > nowUtc guard guarantees
            // we never revive a session that already idled out. Set-based UPDATE (no tracking).
            return await _context.RefreshTokens
                .Where(r => r.UserId == userId && !r.IsRevoked && r.ExpiryDate > nowUtc)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiryDate, newExpiryUtc));
        }
    }
}
