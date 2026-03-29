using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    public interface IgoogleUserRepo: IGenericRepo<GoogleUser, long>
    {
        public Task<GoogleUser?> GetByGoogleIdAsync(string googleId);
    }
}
