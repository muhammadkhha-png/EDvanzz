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
    }
}
