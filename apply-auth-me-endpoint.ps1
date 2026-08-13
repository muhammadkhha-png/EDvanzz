<#
.SYNOPSIS
    Auth /me endpoint  -  GET /api/auth/me, same response shape as /api/auth/login.
    Anchor-based find/replace, not a git patch  -  safe against CRLF checkout mismatches.

.DESCRIPTION
    Implements a JWT-only "who am I" endpoint that reuses the existing Login pipeline
    instead of duplicating it:

      1. IAuthService.cs        -  new GetCurrentUserAsync(long userId) contract method.
      2. AuthService.cs         -  implementation. Resolves the user from the DB, then
         calls TokenService.BuildUserTokenData(user)  -  the SAME method Login, AdminLogin,
         and Refresh already call  -  to build accessToken + userAccountData. Does NOT call
         IssueAndStageRefreshTokenAsync: unlike Login, this is a read-only check an app may
         call on every foreground/splash, and minting+persisting a new RefreshToken row on
         every call would grow that table unboundedly. refreshToken is always null in the
         response (AuthResponse already supports this  -  see SigUpByGoogle's
         incomplete-profile branch).
      3. AuthController.cs      -  injects ICurrentUserService (already DI-registered) and
         adds [Authorize][HttpGet("me")] GetMe(). Enforcement does NOT rely on [Authorize]
         actually firing  -  this controller has class-level [AllowAnonymous], which per the
         existing change-password/delete-account doc notes makes [Authorize] unreliable
         here. Instead this mirrors NotificationsController/ChatController/UploadController:
         checks _currentUser.UserId is null and returns the shared UserNotResolved() 401
         helper directly. A missing, invalid, or expired Bearer token all leave
         HttpContext.User anonymous under [AllowAnonymous], so this one check correctly
         covers all three required 401 cases.
      4/5. Messages.en.resx / Messages.ar.resx  -  new CurrentUserRetrievedSuccess key
         (EN + Egyptian Arabic). NOT reusing the existing "successlogin" key used by
         Login/AdminLogin/Refresh  -  that key does not actually exist in either resx file
         today (IStringLocalizer silently falls back to returning the literal string
         "successlogin"), and this endpoint deserves its own accurate message regardless.

    Two things flagged during implementation, NOT fixed here (out of scope  -  surgical
    patch only, touch only what's needed):
      - ChangePassword/DeleteAccount's XML docs promise 401, but their service-layer
        Result<T>.Failure(_localizer, "UserNotFound") calls omit the HttpStatusCode
        argument, which defaults to 400. Worth a follow-up if you want it fixed.
      - "successlogin" (used by Login/AdminLogin/Refresh) is not a real resx key in
        either Messages.en.resx or Messages.ar.resx today.

.NOTES
    Run from the repo root (muhammadkhha-png/EDvanzz, master_integration branch).
    Idempotent: if an anchor is already gone (i.e. already applied), that step is skipped
    with a warning, not a failure. Verified end-to-end against a fresh pristine clone of
    master_integration before delivery: all 7 edits apply cleanly on a first run, all 7
    correctly SKIP on a second run (no double-insertion), and the resulting files are
    byte-identical to the reviewed implementation.

    Saved with a UTF-8 BOM on purpose: two of the files this script edits (the resx files)
    contain non-ASCII Arabic text, and this script's own comments contain em-dashes.
    Windows PowerShell 5.1 does not reliably assume UTF-8 for a BOM-less script or a
    BOM-less Get-Content read  -  without the BOM here, and -Encoding UTF8 below, those
    bytes get silently reinterpreted under the console's codepage and the script (or the
    files it writes) end up corrupted. If you ever re-save this file, keep it UTF-8 with BOM.
#>

$ErrorActionPreference = 'Stop'

function Apply-Edit {
    param(
        [string]$Path,
        [string]$Old,
        [string]$New,
        [string]$Description
    )

    if (-not (Test-Path $Path)) {
        throw "File not found: $Path"
    }

    $content = Get-Content -Path $Path -Raw -Encoding UTF8

    # CRLF-safety: this repo's .gitattributes sets "* text=auto", which normalizes to
    # LF inside git but converts to CRLF on checkout under Git for Windows' default
    # core.autocrlf=true - almost certainly what a Desktop\...\EDvanzz checkout has on
    # disk. The Old/New here-strings below are LF-only. Matching (and building the
    # replacement) is therefore done against an LF-normalized view of both, and the
    # file's OWN original line-ending convention is restored before writing - never
    # mixing CRLF and LF within one file, and matching correctly whether the checkout
    # is CRLF (typical Windows) or LF (e.g. under WSL).
    $usesCrlf = $content.Contains("`r`n")
    $normalizedContent = $content -replace "`r`n", "`n"
    $normalizedOld = $Old -replace "`r`n", "`n"
    $normalizedNew = $New -replace "`r`n", "`n"

    $count = ([regex]::Matches($normalizedContent, [regex]::Escape($normalizedOld))).Count

    if ($count -eq 0) {
        Write-Warning "SKIP  [$Description]  -  anchor not found in $Path (already applied, or file has diverged  -  check manually)."
        return
    }
    if ($count -gt 1) {
        throw "ABORT [$Description]  -  anchor found $count times in $Path, expected exactly 1. Refusing to guess which one."
    }

    $updatedNormalized = $normalizedContent.Replace($normalizedOld, $normalizedNew)
    $updated = if ($usesCrlf) { $updatedNormalized -replace "`n", "`r`n" } else { $updatedNormalized }

    # OneDrive (and sometimes an AV scanner) transiently locks files under a synced Desktop
    # path right after a write. Retry with backoff instead of failing the whole run on a
    # lock that clears itself within a second or two.
    $maxAttempts = 6
    $delayMs = 400
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            Set-Content -Path $Path -Value $updated -NoNewline -Encoding UTF8
            break
        }
        catch [System.IO.IOException] {
            if ($attempt -eq $maxAttempts) {
                throw "ABORT [$Description]  -  $Path stayed locked after $maxAttempts attempts. Close it in Visual Studio (and let OneDrive finish syncing) and re-run  -  edits already applied are skipped automatically."
            }
            Write-Warning "RETRY [$Description]  -  $Path is locked (attempt $attempt/$maxAttempts), waiting..."
            Start-Sleep -Milliseconds $delayMs
            $delayMs = $delayMs * 2
        }
    }

    Write-Host "OK    [$Description]  -  applied to $Path"
}

$iAuthServicePath = Join-Path $PSScriptRoot 'Edvanz.Application\IservicesContract\IAuthService.cs'
$authServicePath  = Join-Path $PSScriptRoot 'Edvanz.Application\Services\AuthService.cs'
$authControllerPath = Join-Path $PSScriptRoot 'Edvanz.API\Controllers\AuthController.cs'
$messagesEnPath   = Join-Path $PSScriptRoot 'Edvanz.Domain\Messages.en.resx'
$messagesArPath   = Join-Path $PSScriptRoot 'Edvanz.Domain\Messages.ar.resx'

# =====================================================================================
# Edit 1 of 7  -  IAuthService: add the GetCurrentUserAsync contract method.
# =====================================================================================

$edit1Old = @'
        Task<Result<string>> ForceChangePasswordAsync(ForceChangePasswordDto req);

    }
'@

$edit1New = @'
        Task<Result<string>> ForceChangePasswordAsync(ForceChangePasswordDto req);

        /// <summary>
        /// Resolves the authenticated caller from their JWT and returns the same
        /// <see cref="AuthResponse"/> shape as <see cref="Login"/> - a fresh access token
        /// plus the current account profile. No credentials are accepted; <paramref name="userId"/>
        /// must already be resolved from the validated token's claims by the caller.
        /// </summary>
        /// <param name="userId">The caller's user id, resolved from the JWT <c>NameIdentifier</c> claim.</param>
        /// <returns>
        /// Success with a rebuilt <see cref="AuthResponse"/> (userAccountData reflects the
        /// account's CURRENT state - role, modules, permissions - not what was true when the
        /// token was originally issued). <c>refreshToken</c> is always <c>null</c>: unlike
        /// <see cref="Login"/>, this does not mint or persist a new refresh token. Failure
        /// (401) with "UserNotFound" or "AccountInactive" if the account behind the token no
        /// longer exists or has been deactivated since the token was issued.
        /// </returns>
        Task<Result<AuthResponse>> GetCurrentUserAsync(long userId);

    }
'@

Apply-Edit -Path $iAuthServicePath -Old $edit1Old -New $edit1New `
    -Description '1/7 IAuthService: add GetCurrentUserAsync contract'

# =====================================================================================
# Edit 2 of 7  -  AuthService: add the System.Net using (for HttpStatusCode.Unauthorized).
# =====================================================================================

$edit2Old = @'
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Text;
'@

$edit2New = @'
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
'@

Apply-Edit -Path $authServicePath -Old $edit2Old -New $edit2New `
    -Description '2/7 AuthService: add System.Net using'

# =====================================================================================
# Edit 3 of 7  -  AuthService: implement GetCurrentUserAsync.
# =====================================================================================
# Anchor deliberately includes Login()'s own closing block (ending in the literal
# "successlogin" message key) rather than just the ForceChangePasswordAsync signature
# alone - that message key is unique to Login/AdminLogin and is NOT reproduced by the
# newly-inserted GetCurrentUserAsync (which uses "CurrentUserRetrievedSuccess"), so this
# anchor correctly disappears after the edit applies and won't re-fire on a second run.

$edit3Old = @'
            }, _localizer, "successlogin");
        }

        /// <inheritdoc />
        public async Task<Result<string>> ForceChangePasswordAsync(ForceChangePasswordDto req)
'@

$edit3New = @'
            }, _localizer, "successlogin");
        }

        /// <inheritdoc />
        public async Task<Result<AuthResponse>> GetCurrentUserAsync(long userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return Result<AuthResponse>.Failure(_localizer, "UserNotFound", HttpStatusCode.Unauthorized);

            // Same active-status gate as Login - a token minted before the account was
            // deactivated must not be able to re-derive a usable session through /me.
            if (user.IsActive != true)
                return Result<AuthResponse>.Failure(_localizer, "AccountInactive", HttpStatusCode.Unauthorized);

            // Single source of truth for accessToken + userDto - the exact same path
            // Login, AdminLogin, and Refresh use. Any future change to what the login
            // payload contains (new module, new permission shape, etc.) is picked up here
            // automatically with zero duplication.
            var (jwt, userDto) = await tokenService.BuildUserTokenData(user);
            if (string.IsNullOrEmpty(jwt))
                return Result<AuthResponse>.Failure(_localizer, "ServerError");

            // Deliberately NOT calling IssueAndStageRefreshTokenAsync here. Unlike Login,
            // /me is a read-only "who am I" check a client may call on every app foreground -
            // persisting a brand-new RefreshToken row on every call would grow that table
            // unboundedly with rows that never get used to refresh anything. refreshToken
            // stays null, which AuthResponse already supports (see SigUpByGoogle's
            // incomplete-profile branch).
            return Result<AuthResponse>.Success(new AuthResponse
            {
                accessToken = jwt,
                refreshToken = null,
                userAccountData = userDto
            }, _localizer, "CurrentUserRetrievedSuccess");
        }

        /// <inheritdoc />
        public async Task<Result<string>> ForceChangePasswordAsync(ForceChangePasswordDto req)
'@

Apply-Edit -Path $authServicePath -Old $edit3Old -New $edit3New `
    -Description '3/7 AuthService: implement GetCurrentUserAsync'

# =====================================================================================
# Edit 4 of 7  -  AuthController: inject ICurrentUserService.
# =====================================================================================

$edit4Old = @'
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
'@

$edit4New = @'
        private readonly IUserService _userService;
        private readonly IOtpService _otpService;
        private readonly IAuthService authService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;

        /// <summary>Initializes the controller with its service dependencies.</summary>
        public AuthController(
            IUserService userService,
            IOtpService otpService,
            IAuthService _authService,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser)
        {
            _userService = userService;
            _otpService = otpService;
            authService = _authService;
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }
'@

Apply-Edit -Path $authControllerPath -Old $edit4Old -New $edit4New `
    -Description '4/7 AuthController: inject ICurrentUserService'

# =====================================================================================
# Edit 5 of 7  -  AuthController: add GET api/auth/me.
# =====================================================================================
# Anchor includes Login()'s own signature + body ("authService.Login(req)") rather than
# the generic "return ToResponse(result); }" pattern alone - that generic pattern is ALSO
# how the newly-inserted GetMe() ends, so a generic anchor would still match (and
# re-fire) after this edit already applied. Anchoring on Login-specific text keeps this
# edit non-reapplicable once done.

$edit5Old = @'
        public async Task<IActionResult> Login(LoginDto req)
        {
            var result = await authService.Login(req);
            return ToResponse(result);
        }

        /// <summary>Authenticates a SuperAdmin user.</summary>
'@

$edit5New = @'
        public async Task<IActionResult> Login(LoginDto req)
        {
            var result = await authService.Login(req);
            return ToResponse(result);
        }

        /// <summary>Returns the current session's profile, in the exact shape <c>login</c> returns.</summary>
        /// <remarks>
        /// Authenticates purely via the Bearer access token already on the request - no
        /// username/password or request body. Internally re-resolves the account from the
        /// database and rebuilds the response through the same <c>TokenService.BuildUserTokenData</c>
        /// path <c>login</c>/<c>admin-login</c>/<c>refresh</c> use, so <c>userAccountData</c>
        /// (role, modules, permissions, linked teacher ids) always reflects the account's
        /// CURRENT state rather than whatever was true when the token was originally issued.
        ///
        /// <para><c>refreshToken</c> is always <c>null</c> in this response. Unlike <c>login</c>,
        /// this does not mint or persist a new refresh token - a read-only identity check an
        /// app may call on every foreground/splash must not grow the RefreshTokens table on
        /// every call. <c>accessToken</c> is still a freshly-signed, fully usable token.</para>
        ///
        /// <para><b>Auth note:</b> as with <c>change-password</c> and <c>delete-account</c>, the
        /// intended gate is authenticated-only; send the Bearer token regardless of the
        /// class-level <c>[AllowAnonymous]</c>. Enforcement here does not rely on that
        /// attribute at all - <see cref="ApiBaseController.UserNotResolved"/> is checked
        /// directly against the resolved claims, which is what actually returns 401 for a
        /// missing, invalid, or expired token, or one without a usable identity claim.</para>
        /// </remarks>
        /// <response code="200">Authenticated; the current profile is returned in the login response shape.</response>
        /// <response code="401">Token missing, invalid, expired, lacking a usable identity claim, or the account behind it no longer exists or is inactive.</response>
        /// <response code="429">Rate limit exceeded.</response>
        [Authorize]
        [HttpGet("me")]
        [EnableRateLimiting("auth")]
        [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Auth.AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> GetMe()
        {
            var userId = _currentUser.UserId;
            if (userId is null)
                return UserNotResolved();

            var result = await authService.GetCurrentUserAsync(userId.Value);
            return ToResponse(result);
        }

        /// <summary>Authenticates a SuperAdmin user.</summary>
'@

Apply-Edit -Path $authControllerPath -Old $edit5Old -New $edit5New `
    -Description '5/7 AuthController: add GET api/auth/me'

# =====================================================================================
# Edit 6 of 7  -  Messages.en.resx: CurrentUserRetrievedSuccess.
# =====================================================================================

$edit6Old = @'
  <data name="UserNotFound" xml:space="preserve">
    <value>We couldn't find this user account. Please check and try again</value>
  </data>
  <data name="TeacherInitializedSuccess" xml:space="preserve">
'@

$edit6New = @'
  <data name="UserNotFound" xml:space="preserve">
    <value>We couldn't find this user account. Please check and try again</value>
  </data>
  <data name="CurrentUserRetrievedSuccess" xml:space="preserve">
    <value>Your account information was retrieved successfully</value>
  </data>
  <data name="TeacherInitializedSuccess" xml:space="preserve">
'@

Apply-Edit -Path $messagesEnPath -Old $edit6Old -New $edit6New `
    -Description '6/7 Messages.en.resx: add CurrentUserRetrievedSuccess'

# =====================================================================================
# Edit 7 of 7  -  Messages.ar.resx: CurrentUserRetrievedSuccess (Egyptian Arabic).
# =====================================================================================

$edit7Old = @'
  <data name="UserNotFound" xml:space="preserve">
    <value>مش لاقيين الحساب ده. من فضلك راجع البيانات وحاول تاني</value>
  </data>
  <data name="TeacherInitializedSuccess" xml:space="preserve">
'@

$edit7New = @'
  <data name="UserNotFound" xml:space="preserve">
    <value>مش لاقيين الحساب ده. من فضلك راجع البيانات وحاول تاني</value>
  </data>
  <data name="CurrentUserRetrievedSuccess" xml:space="preserve">
    <value>تم استرجاع بيانات حسابك بنجاح</value>
  </data>
  <data name="TeacherInitializedSuccess" xml:space="preserve">
'@

Apply-Edit -Path $messagesArPath -Old $edit7Old -New $edit7New `
    -Description '7/7 Messages.ar.resx: add CurrentUserRetrievedSuccess'

Write-Host ''
Write-Host 'Done. Next steps:'
Write-Host '  1. dotnet build  -  build it locally to confirm it compiles before trusting it.'
Write-Host '  2. Hit GET /api/auth/me with a valid Bearer token in Swagger/Postman and confirm the'
Write-Host '     response shape matches /api/auth/login (accessToken populated, refreshToken null,'
Write-Host '     userAccountData populated per user type).'
Write-Host '  3. Confirm 401 with no Authorization header, and with a deliberately malformed/expired token.'
Write-Host '  4. Flagged but NOT fixed here: ChangePassword/DeleteAccount return 400 (not the 401 their'
Write-Host '     docs promise) because their Result<T>.Failure(...) calls omit HttpStatusCode.Unauthorized.'
Write-Host '  5. Flagged but NOT fixed here: "successlogin" (Login/AdminLogin/Refresh success key) is not'
Write-Host '     an actual key in either Messages.en.resx or Messages.ar.resx today.'
