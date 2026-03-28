using Edvanz.Application.Dtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Services;
using Edvanz.Domain.ServiceContract;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Edvanz.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ApiBaseController
    {
        private readonly IuserService userService;
        private readonly IOtpService otpService;

        public AuthController(IuserService _userService,IOtpService otpService)
        {
            userService = _userService;
            this.otpService = otpService;
        }
        [HttpPost("sign-up")]
        public async Task<IActionResult> Signup([FromForm] AddUserDto req)
        {
            var result = await userService.AddUser(req);

            return ToResponse(result);
        }
        [HttpPost("generatet-otp")]
        public async Task<IActionResult> GenertateOtp([FromQuery]string phoneNumber)
        {


            var result = await otpService.AskForOtp(phoneNumber);
            return ToResponse(result);
        }
        [HttpPost("verifiy-otp")]
        public async Task<IActionResult> VerifiyOtp( OtpVerificationDto req)
        {


            var result = await otpService.VerifyOtp(req);
            return ToResponse(result);
        }


    }
}
