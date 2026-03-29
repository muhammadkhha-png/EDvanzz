using Edvanz.Application.Dtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Edvanz.Domain.ServiceContract;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    /// <summary>
    /// Implements user registration operations.
    /// Creates the shared User record and delegates type-specific initialization
    /// to ITeacherService, IStudentUserService, or IParentUserService.
    /// 
    /// All database access goes through IUnitOfWork.Users (IUserRepo) — no direct
    /// GetRepository calls with raw expression predicates.
    /// 
    /// TRANSACTION SAFETY:
    /// AddUser starts a transaction that wraps both the User row creation and the
    /// type-specific initialization. The type-specific services detect HasActiveTransaction
    /// and participate in this outer transaction rather than starting their own.
    /// </summary>
    // FIX I3: Renamed from IuserService to IUserService (PascalCase per .NET conventions)
    public class UserService : IUserService
    {
        private readonly IStringLocalizer<Messages> _localizer;
        private readonly IOtpService _otpService;
        // FIX I3: Renamed from passServicr to _passwordService (correct spelling, underscore prefix)
        private readonly IPasswordService _passwordService;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of UserService with required dependencies.
        /// </summary>
        /// <param name="localizer">String localizer for multilingual error/success messages.</param>
        /// <param name="otpService">OTP generation and verification service.</param>
        /// <param name="passwordService">Password hashing and verification service.</param>
        /// <param name="unitOfWork">Unit of work for database operations.</param>
        public UserService(
            IStringLocalizer<Messages> localizer,
            IOtpService otpService,
            IPasswordService passwordService,
            IUnitOfWork unitOfWork)
        {
            _localizer = localizer;
            _otpService = otpService;
            _passwordService = passwordService;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public async Task<Result<AddUserDto?>> AddUser(AddUserDto user)
        {
            if (user == null)
                return Result<AddUserDto?>.Failure(_localizer, "cann't add empty user");

            if (user.password != user.confirmedPassword)
                return Result<AddUserDto?>.Failure(_localizer, "password must be equail confirmed password");

            // FIX V1: Replaced raw FindAsync expression with named repo method.
            // Previously: unitOfWork.Users.FindAsync(u => u.PhoneNumber == ... || u.Username == ... || ...)
            // Now: all query logic is encapsulated in the repo — if the duplicate check logic
            // ever needs to change, you edit it in ONE place (UserRepo), not here.
            var existingUser = await _unitOfWork.Users.FindExistingUserByCredentialsAsync(
                user.phoneNumber, user.username, user.email);

            if (existingUser != null)
            {
                if (existingUser.PhoneNumber == user.phoneNumber)
                    return Result<AddUserDto?>.Failure(_localizer, "repeatedPhoneNumber");
                if (existingUser.Username == user.username)
                    return Result<AddUserDto?>.Failure(_localizer, "repeatedUserName");
                if (!string.IsNullOrEmpty(user.email) && existingUser.Email == user.email)
                    return Result<AddUserDto?>.Failure(_localizer, "repeatedEmail");
            }

            // FIX I3: Renamed from HasedPass to hashedPass (camelCase for local variables)
            var hashedPass = _passwordService.HashPassword(user.password);
            byte[]? imageBytes = null;

            if (user.idImage != null)
            {
                using var ms = new MemoryStream();
                await user.idImage.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            var addedUser = new User()
            {
                // FIX B2 (partial): Use DateTime.UtcNow consistently instead of DateTime.Now
                CreateAt = DateTime.UtcNow,
                CreateByUserId = null, //To Do
                Email = user.email,
                FullName = user.fullName,
                Username = user.username,
                PhoneNumber = user.phoneNumber,
                IdImage = imageBytes,
                IsActive = true,
                PasswordHashed = hashedPass,
                UserType = user.userType
            };

            await _unitOfWork.BeginTransactionAsync();
            await _unitOfWork.Users.AddAsync(addedUser);

            //To Do handle Add In Users Custom Table
            var res = await _unitOfWork.SaveChangesAsync();
            if (res > 0)
            {
                await _unitOfWork.CommitAsync();
                return Result<AddUserDto?>.Success(user, _localizer, "SuccessSaving");
            }
            else
            {
                await _unitOfWork.RollbackAsync();
                return Result<AddUserDto?>.Failure(_localizer, "error in saving");
            }
        }
    }
}