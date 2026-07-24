using Edvanz.API.Attributes;
using Edvanz.Application.Dtos.Auth;
using Edvanz.Application.Dtos.UserDto;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Edvanz.API.Controllers
{
    /// <summary>
    /// Authentication and self-registration endpoints.
    /// </summary>
    /// <remarks>
    /// Covers sign-up, OTP generation/verification, username/password login, SuperAdmin
    /// login, Google SSO sign-up + profile completion, token refresh, password change,
    /// and logout.
    ///
    /// <para><b>Response envelope.</b> Every endpoint returns the unified shape:
    /// success — <c>{ "success": true, "message": "...", "data": { ... } }</c>;
    /// failure — <c>{ "success": false, "message": "..." }</c>.</para>
    ///
    /// <para><b>Localization.</b> The <c>message</c> field is localized by the
    /// <c>Accept-Language</c> header — <c>en</c> (default) or <c>ar</c>.</para>
    ///
    /// <para><b>Rate limiting.</b> Most endpoints sit behind the "auth" fixed-window
    /// limiter (10 requests / IP / minute); exceeding it returns <c>429</c>.</para>
    ///
    /// <para><b>Seeded test accounts</b> (default password <c>Edvanz@2026</c>):
    /// <c>teacher1</c> (Ahmed Mostafa), <c>teacher2</c> (Mariam Hassan),
    /// <c>student1</c> (Youssef Tarek), <c>parent1</c> (Hany Saleh),
    /// <c>superadmin</c> (admin-login only).</para>
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class AuthController : ApiBaseController
    {
        private readonly IUserService _userService;
        private readonly IOtpService _otpService;
        private readonly IAuthService authService;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>Initializes the controller with its service dependencies.</summary>
        public AuthController(
            IUserService userService,
            IOtpService otpService,
            IAuthService _authService,
            IUnitOfWork unitOfWork)
        {
            _userService = userService;
            _otpService = otpService;
            authService = _authService;
            _unitOfWork = unitOfWork;
        }

        /// <summary>Registers a new self-service user account (Teacher, Student, or Parent).</summary>
        /// <remarks>
        /// Sent as <c>multipart/form-data</c> because it accepts an optional ID image.
        /// Validates uniqueness of username, phone, and email, and enforces the password
        /// policy (min 8 chars; upper, lower, digit, and special character). The account
        /// is created unverified — the caller must then verify via OTP (<c>generate-otp</c>
        /// → <c>verify-otp</c>). For Teacher sign-ups, <c>subjectIds</c> is required.
        /// </remarks>
        /// <param name="req">Registration form: identity, credentials, optional ID image, and (teachers) subjects.</param>
        /// <response code="200">Account created; proceed to OTP verification.</response>
        /// <response code="400">Validation failed or username/phone/email already in use.</response>
        /// <response code="429">Rate limit exceeded.</response>
        [HttpPost("sign-up")]
        [EnableRateLimiting("auth")]
        [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Signup([FromForm] SigupDto req)
        {
            var result = await _userService.AddUser(req);
            return ToResponse(result);
        }

        /// <summary>Generates a one-time password (OTP) for a phone number.</summary>
        /// <remarks>
        /// Stores the OTP hashed on the user record. In the current build the plain-text
        /// OTP is returned in <c>data</c> for development convenience; in production it is
        /// delivered by SMS and must not be returned in the response.
        /// </remarks>
        /// <param name="phoneNumber">Egyptian mobile number, e.g. <c>01000000001</c> (teacher1).</param>
        /// <response code="200">OTP generated (returned in <c>data</c> in dev builds).</response>
        /// <response code="400">No account found for the phone number.</response>
        /// <response code="429">Rate limit exceeded.</response>
        [HttpPost("generate-otp")]
        [EnableRateLimiting("auth")]
        [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> GenerateOtp([FromQuery] string phoneNumber)
        {
            var result = await _otpService.AskForOtp(phoneNumber);
            return ToResponse(result);
        }

        /// <summary>Verifies an OTP and marks the account as verified.</summary>
        /// <param name="req">Phone number and the OTP code received.</param>
        /// <response code="200">OTP correct; account verified.</response>
        /// <response code="400">OTP invalid or expired.</response>
        /// <response code="429">Rate limit exceeded.</response>
        [HttpPost("verify-otp")]
        [EnableRateLimiting("auth")]
        [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> VerifyOtp(OtpVerificationDto req)
        {
            var result = await _otpService.VerifyOtp(req);
            return ToResponse(result);
        }

        /// <summary>Authenticates a Teacher, Assistant, Student, or Parent by username and password.</summary>
        /// <remarks>
        /// On success returns an access token (JWT), a rotatable refresh token, and the
        /// account profile (id, username, full name, account type, accessible modules,
        /// granted permissions, and linked teacher ids). Inactive/suspended accounts are
        /// rejected. SuperAdmin accounts cannot log in here — use <c>admin-login</c>.
        /// </remarks>
        /// <param name="req">Username and password.</param>
        /// <response code="200">Authenticated; tokens and profile returned.</response>
        /// <response code="400">Invalid credentials or inactive account.</response>
        /// <response code="429">Rate limit exceeded.</response>
        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Auth.AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Login(LoginDto req)
        {
            var result = await authService.Login(req);
            return ToResponse(result);
        }

        /// <summary>Authenticates a SuperAdmin user.</summary>
        /// <remarks>
        /// Separate surface from <c>login</c> so admin auth can be monitored, rate-limited,
        /// and firewalled independently. The password is verified <i>before</i> the
        /// SuperAdmin role is checked, and both wrong-password and not-an-admin cases
        /// return the same generic failure message — the endpoint cannot be used to
        /// enumerate which usernames are admins.
        /// </remarks>
        /// <param name="req">Username and password of the SuperAdmin account (e.g. <c>superadmin</c>).</param>
        /// <response code="200">Authenticated as SuperAdmin; tokens and profile returned.</response>
        /// <response code="400">Invalid credentials, or the account is not a SuperAdmin.</response>
        /// <response code="429">Rate limit exceeded.</response>
        [HttpPost("admin-login")]
        [EnableRateLimiting("auth")]
        [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Auth.AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> AdminLogin([FromBody] LoginDto req)
        {
            var result = await authService.AdminLoginAsync(req);
            return ToResponse(result);
        }

        /// <summary>Exchanges a valid refresh token for a new access token (and rotated refresh token).</summary>
        /// <remarks>
        /// Refresh tokens are revocable: changing the password or logging out (all devices)
        /// invalidates them via a SecurityStamp bump. A reused, expired, or revoked token is
        /// rejected.
        /// </remarks>
        /// <param name="req">The current refresh token.</param>
        /// <response code="200">New tokens issued.</response>
        /// <response code="400">Refresh token invalid, expired, or revoked.</response>
        [HttpPost("refresh")]
        [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Auth.AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RefreshToken(RefreshDto req)
        {
            var result = await authService.Refresh(req.token);
            return ToResponse(result);
        }

        /// <summary>Changes the authenticated user's password.</summary>
        /// <remarks>
        /// Requires a valid access token (Bearer). The acting user is taken from the token —
        /// never from the body. Verifies the current password and that the new password and
        /// its confirmation match. When <c>logOutFromAllDevices</c> is true, all of the
        /// user's refresh tokens are revoked.
        ///
        /// <para><b>Auth note:</b> the intended gate is authenticated-only. See the module
        /// review note regarding class-level <c>[AllowAnonymous]</c> precedence — send the
        /// Bearer token regardless.</para>
        /// </remarks>
        /// <param name="req">Current password, new password, confirmation, and the all-devices flag.</param>
        /// <response code="200">Password changed.</response>
        /// <response code="400">Current password incorrect or new/confirm mismatch.</response>
        /// <response code="401">No valid user could be resolved from the token.</response>
        /// <response code="429">Rate limit exceeded.</response>
        [Authorize]
        [HttpPost("change-password")]
        [EnableRateLimiting("auth")]
        [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto req)
        {
            var result = await authService.ChangePassword(req);
            return ToResponse(result);
        }

        /// <summary>Signs up or logs in via Google SSO using a Google ID token.</summary>
        /// <remarks>
        /// Behavior depends on the Google account state:
        /// <list type="bullet">
        ///   <item>Known, completed account → returns a full <c>AuthResponse</c> (logged in).</item>
        ///   <item>New or incomplete account → returns a short-lived <i>complete-profile</i>
        ///   token in <c>data.accessToken</c> (with <c>refreshToken: null</c>); the client
        ///   must then call <c>complete-profile</c> to finish onboarding.</item>
        /// </list>
        /// </remarks>
        /// <param name="req">The Google ID token obtained by the client SDK.</param>
        /// <response code="200">Logged in, or a complete-profile token issued for onboarding.</response>
        /// <response code="400">Google token invalid.</response>
        /// <response code="429">Rate limit exceeded.</response>
        [HttpPost("sigup-by-google")]
        [EnableRateLimiting("auth")]
        [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Auth.AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> GoogleSignUp(SigUpByGoogle req)
        {
            var result = await authService.SigUpByGoogle(req.clientDeviceToken);
            return ToResponse(result);
        }

        /// <summary>Completes onboarding for a Google SSO user (sets identity, role, and teacher subjects).</summary>
        /// <remarks>
        /// Authorized by the short-lived complete-profile token returned from
        /// <c>sigup-by-google</c> (policy <c>CompleteProfile</c>). Validates the Egyptian
        /// phone format and uniqueness of phone/username/email, and (for teachers) requires
        /// <c>subjectIds</c>. On success creates the User + role record and returns a full
        /// <c>AuthResponse</c>.
        ///
        /// <para><b>Auth note:</b> see the module review note about class-level
        /// <c>[AllowAnonymous]</c> precedence over this policy — send the complete-profile
        /// token in the Authorization header.</para>
        /// </remarks>
        /// <param name="req">Identity, role, language, and (teachers) subjects/capacity.</param>
        /// <response code="200">Profile completed; tokens and profile returned.</response>
        /// <response code="400">Validation failed or username/phone/email already in use.</response>
        /// <response code="401">Missing or invalid complete-profile token.</response>
        [Authorize(Policy = "CompleteProfile")]
        [HttpPost("complete-profile")]
        [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Auth.AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CompleteProfile(CompleteProfileDto req)
        {
            var result = await authService.CompleteProfile(req);
            return ToResponse(result);
        }

        /// <summary>Logs out by revoking the supplied refresh token (optionally all sessions).</summary>
        /// <remarks>
        /// Idempotent: an unknown or already-revoked token still returns success. When
        /// <c>logoutAllSessions</c> is true, every refresh token for the user is revoked and
        /// the SecurityStamp is bumped, ending all active sessions.
        /// </remarks>
        /// <param name="req">The refresh token to revoke, and whether to end all sessions.</param>
        /// <response code="200">Logged out (always, by idempotency).</response>
        [HttpPost("logout")]
        [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Logout([FromBody] LogoutDto req)
        {
            var result = await authService.Logout(req.token, req.logoutAllSessions);
            return ToResponse(result);
        }
        /// <summary>Forces a password reset on any user account. SuperAdmin only.</summary>
        /// <remarks>
        /// No old-password verification — this is the admin-initiated counterpart to
        /// <c>change-password</c>. Every existing refresh token for the target user is
        /// revoked and their SecurityStamp is bumped, so all of their active sessions
        /// are invalidated immediately.
        ///
        /// <para><b>Auth note:</b> gated by <see cref="ModulePermissionAttribute"/>
        /// (roleOnly: SuperAdmin), which reads the role directly off
        /// <c>HttpContext.User</c> — this is deliberate, not <c>[Authorize]</c>, because
        /// this controller's class-level <c>[AllowAnonymous]</c> makes <c>[Authorize]</c>
        /// unreliable here (see the <c>change-password</c> doc note above).</para>
        /// </remarks>
        /// <param name="req">Target user id, new password, and confirmation.</param>
        /// <response code="200">Password force-changed; target's sessions revoked.</response>
        /// <response code="400">Validation failed, target not found, or confirmation mismatch.</response>
        /// <response code="403">Caller is not a SuperAdmin.</response>
        /// <response code="429">Rate limit exceeded.</response>
        [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
        [HttpPost("admin/force-change-password")]
        [EnableRateLimiting("admin-auth")]
        [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ForceChangePassword([FromBody] Edvanz.Application.Dtos.Auth.ForceChangePasswordDto req)
        {
            var result = await authService.ForceChangePasswordAsync(req);
            return ToResponse(result);
        }
    }
}