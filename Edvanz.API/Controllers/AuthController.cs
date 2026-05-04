using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Auth;
using Edvanz.Application.Dtos.UserDto;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Edvanz.API.Controllers
{
    /// <summary>
    /// API endpoints for authentication and user registration.
    /// 
    /// Handles: user sign-up, OTP generation, and OTP verification.
    /// 
    /// All responses follow a unified JSON shape:
    ///   Success: { "success": true,  "message": "...", "data": { ... } }
    ///   Failure: { "success": false, "message": "..." }
    /// 
    /// Messages are returned in Arabic or English based on the Accept-Language header.
    /// Set "Accept-Language: ar" for Arabic, "Accept-Language: en" for English.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class AuthController : ApiBaseController
    {
        // FIX I1/I3: Updated from IuserService to IUserService (PascalCase, correct namespace)
        private readonly IUserService _userService;
        private readonly IOtpService _otpService;
        private readonly IAuthService authService;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of AuthController with required service dependencies.
        /// </summary>
        /// <param name="userService">User registration service.</param>
        /// <param name="otpService">OTP generation and verification service.</param>
        public AuthController(IUserService userService, IOtpService otpService,IAuthService _authService ,IUnitOfWork unitOfWork)
        {
            _userService = userService;
            _otpService = otpService;
            authService = _authService;
            this._unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Registers a new user account.
        /// Validates uniqueness of phone number, username, and email.
        /// Creates the User record in the database.
        /// </summary>
        /// <param name="req">Registration data including user type, credentials, and optional ID image.</param>
        /// <returns>Result containing the registration data on success.</returns>
        [HttpPost("sign-up")]
        public async Task<IActionResult> Signup([FromForm] SigupDto req)
        {
            var result = await _userService.AddUser(req);
            return ToResponse(result);
        }

        /// <summary>
        /// Generates a new OTP for the given phone number and stores it (hashed) on the user record.
        /// The plain-text OTP is returned in the response for delivery to the user (via SMS in production).
        /// </summary>
        /// <param name="phoneNumber">The phone number to generate an OTP for.</param>
        /// <returns>Result containing the plain-text OTP code on success.</returns>
        [HttpPost("generate-otp")]
        public async Task<IActionResult> GenerateOtp([FromQuery] string phoneNumber)
        {
            var result = await _otpService.AskForOtp(phoneNumber);
            return ToResponse(result);
        }

        /// <summary>
        /// Verifies an OTP code for a given phone number.
        /// On success, marks the user account as verified.
        /// </summary>
        /// <param name="req">Verification data containing phone number and OTP code.</param>
        /// <returns>Result indicating success or failure of verification.</returns>
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp(OtpVerificationDto req)
        {
            var result = await _otpService.VerifyOtp(req);
            return ToResponse(result);
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto req)
        {
            var result = await authService.Login(req);
            return ToResponse(result);
        }
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto req)
        {
            var result = await authService.ChangePassword(req);
            return ToResponse(result);
        }
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken(RefeshDto req)
        {
            var result = await authService.Refresh(req.token);
            return ToResponse(result);
        }
        [HttpPost("sigup-by-google")]
        public async Task<IActionResult> GoogleSignUp(RefeshDto req)
        {
            var result = await authService.SigUpByGoogle(req.token);
            return ToResponse(result);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefeshDto req)
        {
            var result = await authService.Logout(req.token);
            return ToResponse(result);
        }


        // ════════════════════════════════════════════════════════════════
        // ADD THIS ACTION METHOD to Edvanz.API/Controllers/AuthController.cs
        // alongside the existing Login() action. No other changes to the controller.
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Authenticates a SuperAdmin user.
        ///
        /// This endpoint accepts the same credentials shape as the generic /login endpoint
        /// but rejects every non-SuperAdmin account. It exists as a separate surface so:
        ///   - admin auth can be rate-limited / monitored / firewalled independently of
        ///     end-user login,
        ///   - 2FA or IP allow-listing can be added later without touching tutor login,
        ///   - tutor login cannot accidentally issue an admin token.
        ///
        /// SECURITY:
        ///   The service verifies the password BEFORE checking the SuperAdmin role and
        ///   returns the same generic "InvalidCredentials" message for both wrong-password
        ///   and not-an-admin cases. Clients cannot use this endpoint to enumerate which
        ///   usernames belong to which user type.
        /// </summary>
        /// <param name="req">Username and password.</param>
        /// <returns>
        /// 200 with an <c>AuthResponse</c> on successful admin login. 400 with
        /// "InvalidCredentials" on any failure path.
        /// </returns>
      
        [HttpPost("admin-login")]
        public async Task<IActionResult> AdminLogin([FromBody] LoginDto req)
        {
            var result = await authService.AdminLoginAsync(req);
            return ToResponse(result);
        }

    }
}