using Edvanz.Application.Dtos;
using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.ServiceContract
{
    public interface IuserService
    {
        public Task<Result<AddUserDto?>> AddUser(AddUserDto user);
    }
}
