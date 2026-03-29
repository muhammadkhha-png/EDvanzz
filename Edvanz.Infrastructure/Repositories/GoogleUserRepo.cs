using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Infrastructure.Repositories
{
    public class GoogleUserRepo : GenericRepo<GoogleUser, long>, IgoogleUserRepo
    {
        public GoogleUserRepo(EdvanzDbContext context) : base(context)
        {
        }

        public async Task<GoogleUser?> GetByGoogleIdAsync(string googleId)
        {
            return await _context.GoogleUsers
                .FirstOrDefaultAsync(u => u.GoogleId == googleId);
        }
    }
}
