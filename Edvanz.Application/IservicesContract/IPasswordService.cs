using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.ServiceContract
{
    public interface IPasswordService
    {
        public string HashPassword(string password);
        public bool VerifyPassword(string hashedPassword, string providedPassword);
    }
}
