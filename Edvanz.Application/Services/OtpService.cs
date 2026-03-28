using Edvanz.Application.Dtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Edvanz.Domain.ServiceContract;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    public class OtpService : IOtpService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IStringLocalizer<Messages> localizer;
        private readonly IPasswordService passService;

        public OtpService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer,IPasswordService passService)
        {
            this.unitOfWork = unitOfWork;
            this.localizer = localizer;
            this.passService = passService;
        }
        public async Task<Result<string>> AskForOtp(string phoneNumber)
        {
           var user=await unitOfWork.Users.GetByPhoneAsync(phoneNumber);
            if (user == null)
                return Result<string>.Failure(localizer, "NoUserFoundForThisPhone");
            var otp = GenerateOtp();
            var hashedOtp=passService.HashPassword(otp);
            user.OtpCode = hashedOtp;
            user.OtpExpiry=DateTime.Now.AddMinutes(5);
            await unitOfWork.Users.UpdateAsync(user);
            var res = await unitOfWork.SaveChangesAsync();
            if (res > 0)
                return Result<string>.Success(otp, localizer, "GeneratedOtpSucc");

            return Result<string>.Failure(localizer, "internalServerError",System.Net.HttpStatusCode.InternalServerError);

        }

        public string GenerateOtp()
        {
            return new Random().Next(100000, 999999).ToString();
        }
        
    }
}
