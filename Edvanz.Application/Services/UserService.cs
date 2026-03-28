using Edvanz.Application.Dtos;
using Edvanz.Application.IservicesContract;
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
   
    public class UserService :IuserService
    {
        private readonly IStringLocalizer<Messages> localizer;
        private readonly IOtpService otpService;
        private readonly IPasswordService passServicr;
        private readonly IUnitOfWork unitOfWork;

        public UserService(IStringLocalizer<Messages> localizer,IOtpService otpService,IPasswordService _passServicr,IUnitOfWork _unitOfWork)
        {
            this.localizer = localizer;
            this.otpService = otpService;
            passServicr = _passServicr;
            unitOfWork = _unitOfWork;
        }

        public async Task<Result<AddUserDto?>> AddUser(AddUserDto user)
        {
            if (user == null)
                return Result<AddUserDto?>.Failure(localizer, "cann't add empty user");
         
            if(user.password != user.confirmedPassword)
                return Result<AddUserDto?>.Failure(localizer, "password must be equail confirmed password");
           
            var existingUser = await unitOfWork.Users
    .FindAsync(u => u.PhoneNumber == user.phoneNumber
                   || u.Username == user.username
                   || (!string.IsNullOrEmpty(user.email) && u.Email == user.email));

            if (existingUser != null)
            {
                if (existingUser.PhoneNumber == user.phoneNumber)
                    return Result<AddUserDto?>.Failure(localizer, "repeatedPhoneNumber");
                if (existingUser.Username == user.username)
                    return Result<AddUserDto?>.Failure(localizer, "repeatedUserName");
                if (!string.IsNullOrEmpty(user.email) && existingUser.Email == user.email)
                    return Result<AddUserDto?>.Failure(localizer, "repeatedEmail");
            }
          
            var HasedPass = passServicr.HashPassword(user.password);
            byte[]? imageBytes = null;

            if (user.idImage != null)
            {
                using var ms = new MemoryStream();
                await user.idImage.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }
            var AddedUser = new User()
            {
                CreateAt = DateTime.Now,
                CreateByUserId = null, //To Do
                Email = user.email,
                FullName = user.fullName,
                Username=user.username,
                PhoneNumber = user.phoneNumber,
                IdImage = imageBytes,
                IsActive = true,
                PasswordHashed = HasedPass,
                UserType=user.userType
                
            };
            await unitOfWork.BeginTransactionAsync();
            await unitOfWork.Users.AddAsync(AddedUser);

            //To Do handle Add In Users Custom Table 
            var res = await unitOfWork.SaveChangesAsync();
            if (res > 0)
            {
                await unitOfWork.CommitAsync();
                return Result<AddUserDto?>.Success(user, localizer,"SuccessSaving");

            }
            else
            {
               await unitOfWork.RollbackAsync();
                return Result<AddUserDto?>.Failure(localizer, "error in saving");
            }

        }
    }
}
