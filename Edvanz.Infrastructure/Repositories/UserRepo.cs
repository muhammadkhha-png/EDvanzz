using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Infrastructure.Repositories
{
    public class UserRepo : GenericRepo<User, long>, IUserRepo
    {
        public UserRepo(EdvanzDbContext context) : base(context)
        {
        }

        public async Task<User?> GetByPhoneAsync(string phone)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == phone);
        }
        public async Task<User?> GetByUserName(string userName)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == userName);
        }
        public async Task<User?> GetByEmail(string email)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Email  == email);
        }

    }
}
