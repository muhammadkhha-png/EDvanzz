using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    public interface IUserRepo:IGenericRepo<User,long>
    {
        public Task<User> GetByPhoneAsync(string phone);
        public Task<User?> GetByEmail(string email);
        public Task<User?> GetByUserName(string userName);
    }
}
