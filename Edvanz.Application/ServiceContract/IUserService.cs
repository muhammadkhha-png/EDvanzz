using Edvanz.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

// FIX I1: Moved from Edvanz.Domain.ServiceContract to Edvanz.Application.ServiceContract.
// The interface references AddUserDto which lives in the Application layer,
// so it cannot live in the Domain layer (Onion dependency rule violation).
// FIX I3: Renamed from IuserService to IUserService (PascalCase per .NET conventions).
namespace Edvanz.Application.ServiceContract
{
    /// <summary>
    /// Defines the contract for User module operations.
    /// Handles user registration (creating the shared User record).
    /// Type-specific initialization (Teacher/Student/Parent) is delegated to
    /// ITeacherService, IStudentUserService, and IParentUserService respectively.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Registers a new user account.
        /// Validates uniqueness of phone number, username, and email.
        /// Creates the User record and delegates type-specific initialization
        /// to the appropriate service based on UserType.
        /// </summary>
        /// <param name="user">Registration data including credentials and user type.</param>
        /// <returns>Result containing the registration data on success.</returns>
        Task<Result<AddUserDto?>> AddUser(AddUserDto user);
    }
}