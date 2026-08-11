<#
.SYNOPSIS
    Notification System Production Readiness  -  Risk #1: FCM token removal on logout.
    Anchor-based find/replace, not a git patch  -  safe against CRLF checkout mismatches.

.DESCRIPTION
    Fixes: "Ex-users keep receiving pushes with sensitive content (chat previews,
    payment/subscription info) on devices they logged out of."

    Adds two things, and nothing else:

      1. DELETE /api/notifications/fcm-token  -  explicit, client-initiated removal of
         ONE device token, scoped to the calling user (JWT). This is the correct path
         for the single-device logout case: the client already knows its own FCM
         token value and can call this right before/at logout. No new field was added
         to LogoutDto for this - the wire contract of POST /api/auth/logout is
         untouched.

      2. AuthService.Logout  -  when logoutAllSessions = true, also deactivates EVERY
         device token for that user (the userId alone is enough to identify the full
         set, so no new client contract is needed for this path). Wired into the same
         transaction as the existing refresh-token/SecurityStamp logic, immediately
         after the existing InvalidateUserAsync call.

    NOT changed: no existing method signature, business rule, or return shape was
    touched. RegisterFcmTokenAsync, the reminder/renewal/rejection/capacity push jobs,
    refresh-token revocation, and SecurityStamp handling are untouched.

    New repo methods added (both ExecuteUpdateAsync - no entity load, no SaveChanges
    required, matching the existing DeactivateTokenAsync pattern exactly):
      - IUserDeviceTokenRepo.DeactivateByUserAndTokenAsync(userId, fcmToken)
      - IUserDeviceTokenRepo.DeactivateAllForUserAsync(userId)

.NOTES
    Run from the repo root (muhammadkhha-png/EDvanzz, feature/notifictions).
    Idempotent: if an anchor is already gone (already applied), that step is skipped
    with a warning, not a failure. Every anchor in this script was verified against a
    fresh clone of the branch before being written - not reconstructed from memory.

    Saved with a UTF-8 BOM on purpose: this file, and the files it edits, contain
    non-ASCII characters (em-dashes, Arabic text) in content this script writes.
    Windows PowerShell 5.1 does not reliably assume UTF-8 for a BOM-less script or a
    BOM-less Get-Content read - without the BOM here, and -Encoding UTF8 below, those
    bytes get silently reinterpreted under the console's codepage and the files this
    script writes end up corrupted. If you ever re-save this file, keep it UTF-8 with BOM.

    After running: `dotnet build` locally to confirm it compiles before trusting it.
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

    $count = ([regex]::Matches($content, [regex]::Escape($Old))).Count

    if ($count -eq 0) {
        Write-Warning "SKIP  [$Description]  -  anchor not found in $Path (already applied, or file has diverged  -  check manually)."
        return
    }
    if ($count -gt 1) {
        throw "ABORT [$Description]  -  anchor found $count times in $Path, expected exactly 1. Refusing to guess which one."
    }

    $updated = $content.Replace($Old, $New)

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

function New-FileIfMissing {
    param(
        [string]$Path,
        [string]$Content,
        [string]$Description
    )

    if (Test-Path $Path) {
        Write-Warning "SKIP  [$Description]  -  $Path already exists (already applied, or file was created independently  -  check manually)."
        return
    }

    $dir = Split-Path -Path $Path -Parent
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $maxAttempts = 6
    $delayMs = 400
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            Set-Content -Path $Path -Value $Content -NoNewline -Encoding UTF8
            break
        }
        catch [System.IO.IOException] {
            if ($attempt -eq $maxAttempts) {
                throw "ABORT [$Description]  -  $Path stayed locked after $maxAttempts attempts."
            }
            Write-Warning "RETRY [$Description]  -  $Path is locked (attempt $attempt/$maxAttempts), waiting..."
            Start-Sleep -Milliseconds $delayMs
            $delayMs = $delayMs * 2
        }
    }

    Write-Host "OK    [$Description]  -  created $Path"
}

$iUserDeviceTokenRepoPath      = Join-Path $PSScriptRoot 'Edvanz.Domain\Interfaces\IUserDeviceTokenRepo.cs'
$userDeviceTokenRepoPath       = Join-Path $PSScriptRoot 'Edvanz.Infrastructure\Repositories\UserDeviceTokenRepo.cs'
$subscriptionConstantsPath     = Join-Path $PSScriptRoot 'Edvanz.Domain\Constants\SubscriptionConstants.cs'
$messagesEnResxPath            = Join-Path $PSScriptRoot 'Edvanz.Domain\Messages.en.resx'
$messagesArResxPath            = Join-Path $PSScriptRoot 'Edvanz.Domain\Messages.ar.resx'
$unregisterDtoPath             = Join-Path $PSScriptRoot 'Edvanz.Application\Dtos\Subscription\UnregisterFcmTokenRequest.cs'
$iNotificationHistoryServicePath = Join-Path $PSScriptRoot 'Edvanz.Application\ServiceContract\INotificationHistoryService.cs'
$notificationHistoryServicePath  = Join-Path $PSScriptRoot 'Edvanz.Application\Services\NotificationHistoryService.cs'
$notificationsControllerPath   = Join-Path $PSScriptRoot 'Edvanz.API\Controllers\NotificationsController.cs'
$authServicePath                = Join-Path $PSScriptRoot 'Edvanz.Application\Services\AuthService.cs'

# =====================================================================================
# Edit 1 of 10  -  IUserDeviceTokenRepo: declare the two new repo methods.
# =====================================================================================

$edit1Old = @'
    /// <summary>
    /// Flips IsActive=false on a single token row by Id. Called when the push
    /// sender receives "registration-token-not-registered" from Firebase (EC-11).
    /// Uses ExecuteUpdateAsync — no entity load.
    /// </summary>
    Task DeactivateTokenAsync(long tokenId);
}
'@

$edit1New = @'
    /// <summary>
    /// Flips IsActive=false on a single token row by Id. Called when the push
    /// sender receives "registration-token-not-registered" from Firebase (EC-11).
    /// Uses ExecuteUpdateAsync — no entity load.
    /// </summary>
    Task DeactivateTokenAsync(long tokenId);

    /// <summary>
    /// Flips IsActive=false on a single token row identified by (UserId, FcmToken),
    /// scoped to the owning user so a caller can only ever deactivate their own
    /// device token — never another user's row (IDOR guard). Called by
    /// DELETE /api/notifications/fcm-token — the client-initiated unregister, typically
    /// invoked right before/at logout.
    /// Idempotent: a token that doesn't exist, or is already inactive, still
    /// completes with 0 rows affected — never an error.
    /// Uses ExecuteUpdateAsync — no entity load, no SaveChanges required.
    /// </summary>
    Task DeactivateByUserAndTokenAsync(long userId, string fcmToken);

    /// <summary>
    /// Flips IsActive=false on every device-token row belonging to a user. Called by
    /// AuthService.Logout when logoutAllSessions is true, so every device stops
    /// receiving push once every session is revoked.
    /// Idempotent: a user with no tokens still completes with 0 rows affected.
    /// Uses ExecuteUpdateAsync — no entity load, no SaveChanges required.
    /// </summary>
    Task DeactivateAllForUserAsync(long userId);
}
'@

Apply-Edit -Path $iUserDeviceTokenRepoPath -Old $edit1Old -New $edit1New `
    -Description '1/10 IUserDeviceTokenRepo: declare DeactivateByUserAndTokenAsync + DeactivateAllForUserAsync'

# =====================================================================================
# Edit 2 of 10  -  UserDeviceTokenRepo: implement the two new repo methods.
# =====================================================================================

$edit2Old = @'
    /// <inheritdoc />
    public async Task DeactivateTokenAsync(long tokenId)
    {
        // Single SQL UPDATE — no entity materialized.
        await _context.Set<UserDeviceToken>()
            .Where(t => t.Id == tokenId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.IsActive, false));
    }
}
'@

$edit2New = @'
    /// <inheritdoc />
    public async Task DeactivateTokenAsync(long tokenId)
    {
        // Single SQL UPDATE — no entity materialized.
        await _context.Set<UserDeviceToken>()
            .Where(t => t.Id == tokenId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.IsActive, false));
    }

    /// <inheritdoc />
    public async Task DeactivateByUserAndTokenAsync(long userId, string fcmToken)
    {
        // Single SQL UPDATE, scoped to (UserId, FcmToken) — matches the unique index
        // IX_UserDeviceTokens_UserId_FcmToken, so this can never touch another user's row.
        await _context.Set<UserDeviceToken>()
            .Where(t => t.UserId == userId && t.FcmToken == fcmToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.IsActive, false));
    }

    /// <inheritdoc />
    public async Task DeactivateAllForUserAsync(long userId)
    {
        // Single SQL UPDATE — no entity materialized. Used by AuthService.Logout's
        // all-devices path so every device stops receiving push at once.
        await _context.Set<UserDeviceToken>()
            .Where(t => t.UserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.IsActive, false));
    }
}
'@

Apply-Edit -Path $userDeviceTokenRepoPath -Old $edit2Old -New $edit2New `
    -Description '2/10 UserDeviceTokenRepo: implement DeactivateByUserAndTokenAsync + DeactivateAllForUserAsync'

# =====================================================================================
# Edit 3 of 10  -  SubscriptionConstants.Messages: new localization key.
# =====================================================================================

$edit3Old = @'
        public const string FcmTokenRegistered = "FcmTokenRegistered";
        public const string FcmTokenRequired = "FcmTokenRequired";
'@

$edit3New = @'
        public const string FcmTokenRegistered = "FcmTokenRegistered";
        public const string FcmTokenRequired = "FcmTokenRequired";
        public const string FcmTokenUnregistered = "FcmTokenUnregistered";
'@

Apply-Edit -Path $subscriptionConstantsPath -Old $edit3Old -New $edit3New `
    -Description '3/10 SubscriptionConstants.Messages: add FcmTokenUnregistered key'

# =====================================================================================
# Edit 4 of 10  -  Messages.en.resx: English localized text for the new key.
# =====================================================================================

$edit4Old = @'
  <data name="FcmTokenRequired" xml:space="preserve">
    <value>Device token is required</value>
  </data>
'@

$edit4New = @'
  <data name="FcmTokenRequired" xml:space="preserve">
    <value>Device token is required</value>
  </data>
  <data name="FcmTokenUnregistered" xml:space="preserve">
    <value>Device unregistered from push notifications</value>
  </data>
'@

Apply-Edit -Path $messagesEnResxPath -Old $edit4Old -New $edit4New `
    -Description '4/10 Messages.en.resx: add FcmTokenUnregistered entry'

# =====================================================================================
# Edit 5 of 10  -  Messages.ar.resx: Egyptian Arabic localized text for the new key.
# =====================================================================================

$edit5Old = @'
  <data name="FcmTokenRequired" xml:space="preserve">
    <value>توكن الجهاز مطلوب</value>
  </data>
'@

$edit5New = @'
  <data name="FcmTokenRequired" xml:space="preserve">
    <value>توكن الجهاز مطلوب</value>
  </data>
  <data name="FcmTokenUnregistered" xml:space="preserve">
    <value>تم إلغاء تسجيل الجهاز من الإشعارات الفورية</value>
  </data>
'@

Apply-Edit -Path $messagesArResxPath -Old $edit5Old -New $edit5New `
    -Description '5/10 Messages.ar.resx: add FcmTokenUnregistered entry'

# =====================================================================================
# Edit 6 of 10  -  New file: UnregisterFcmTokenRequest.cs (companion to RegisterFcmTokenRequest).
# =====================================================================================

$unregisterDtoContent = @'
﻿namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for DELETE /api/notifications/fcm-token (companion to
/// RegisterFcmTokenRequest / FR-SUB-054). Removes/deactivates a single device
/// token — called by the client on logout, or whenever it knows a token is no
/// longer valid for this session.
/// </summary>
public class UnregisterFcmTokenRequest
{
    /// <summary>
    /// The Firebase-generated FCM token to deactivate for the calling user.
    /// </summary>
    public string Token { get; set; } = null!;
}
'@

New-FileIfMissing -Path $unregisterDtoPath -Content $unregisterDtoContent `
    -Description '6/10 New file: UnregisterFcmTokenRequest.cs'

# =====================================================================================
# Edit 7 of 10  -  INotificationHistoryService: declare UnregisterFcmTokenAsync.
# =====================================================================================

$edit7Old = @'
    /// <summary>
    /// Upserts the user's FCM device token (FR-SUB-054).
    /// Idempotent on (UserId, FcmToken) — refreshes LastSeenAt and IsActive
    /// when the token already exists.
    /// </summary>
    Task<Result<bool>> RegisterFcmTokenAsync(
        long userId, RegisterFcmTokenRequest request);
}
'@

$edit7New = @'
    /// <summary>
    /// Upserts the user's FCM device token (FR-SUB-054).
    /// Idempotent on (UserId, FcmToken) — refreshes LastSeenAt and IsActive
    /// when the token already exists.
    /// </summary>
    Task<Result<bool>> RegisterFcmTokenAsync(
        long userId, RegisterFcmTokenRequest request);

    /// <summary>
    /// Deactivates a single FCM device token for the calling user (companion to
    /// RegisterFcmTokenAsync). Called on logout, or whenever the client knows a
    /// token is no longer valid for this session.
    /// Idempotent — a token that doesn't exist, or is already inactive, still
    /// returns success.
    /// </summary>
    Task<Result<bool>> UnregisterFcmTokenAsync(
        long userId, UnregisterFcmTokenRequest request);
}
'@

Apply-Edit -Path $iNotificationHistoryServicePath -Old $edit7Old -New $edit7New `
    -Description '7/10 INotificationHistoryService: declare UnregisterFcmTokenAsync'

# =====================================================================================
# Edit 8 of 10  -  NotificationHistoryService: implement UnregisterFcmTokenAsync.
# =====================================================================================

$edit8Old = @'
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, SubscriptionConstants.Messages.FcmTokenRegistered);
    }
}
'@

$edit8New = @'
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, SubscriptionConstants.Messages.FcmTokenRegistered);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> UnregisterFcmTokenAsync(
        long userId, UnregisterFcmTokenRequest request)
    {
        // ── Validation ──
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result<bool>.Failure(
                _localizer, SubscriptionConstants.Messages.FcmTokenRequired);
        }

        string token = request.Token.Trim();

        // Scoped to (UserId, FcmToken) — never touches another user's row, and is
        // idempotent: a token that doesn't exist, or is already inactive, still
        // completes as a success (mirrors AuthService.Logout's own "unknown token is
        // a successful logout" convention).
        await _unitOfWork.UserDeviceTokensRepo.DeactivateByUserAndTokenAsync(userId, token);

        return Result<bool>.Success(true, _localizer, SubscriptionConstants.Messages.FcmTokenUnregistered);
    }
}
'@

Apply-Edit -Path $notificationHistoryServicePath -Old $edit8Old -New $edit8New `
    -Description '8/10 NotificationHistoryService: implement UnregisterFcmTokenAsync'

# =====================================================================================
# Edit 9 of 10  -  NotificationsController: DELETE /api/notifications/fcm-token endpoint.
# =====================================================================================

$edit9Old = @'
        var result = await _notificationService.RegisterFcmTokenAsync(userId.Value, request);
        return ToResponse(result);
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════
'@

$edit9New = @'
        var result = await _notificationService.RegisterFcmTokenAsync(userId.Value, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 6: UNREGISTER FCM TOKEN (companion to FR-SUB-054)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Deactivates a single FCM device token for the calling user. Client calls
    //   this on logout (or whenever it knows a token is no longer valid for this
    //   session) so the ex-session stops receiving push notifications.
    //
    // TABLES WRITTEN: UserDeviceTokens
    //
    // SAMPLE: DELETE /api/notifications/fcm-token
    //   { "token": "fGhJ…long-fcm-token" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("fcm-token")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnregisterFcmToken([FromBody] UnregisterFcmTokenRequest request)
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return UserNotResolved();

        var result = await _notificationService.UnregisterFcmTokenAsync(userId.Value, request);
        return ToResponse(result);
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════
'@

Apply-Edit -Path $notificationsControllerPath -Old $edit9Old -New $edit9New `
    -Description '9/10 NotificationsController: add DELETE /api/notifications/fcm-token'

# =====================================================================================
# Edit 10 of 10  -  AuthService.Logout: deactivate every device token on all-devices logout.
# =====================================================================================

$edit10Old = @'
                    // MUST run BEFORE SaveChangesAsync so the stamp bump joins this transaction.
                    await _authInvalidation.InvalidateUserAsync(userId);
                }
'@

$edit10New = @'
                    // MUST run BEFORE SaveChangesAsync so the stamp bump joins this transaction.
                    await _authInvalidation.InvalidateUserAsync(userId);

                    // Stop push delivery to every device now that every session is revoked —
                    // companion to the client-initiated DELETE /api/notifications/fcm-token for
                    // the single-device path (that one needs the specific token value, which
                    // only the client has; this path needs only the userId, so no new field was
                    // added to LogoutDto for it). Idempotent: a user with no registered tokens
                    // still completes with 0 rows affected.
                    await _unitOfWork.UserDeviceTokensRepo.DeactivateAllForUserAsync(userId);
                }
'@

Apply-Edit -Path $authServicePath -Old $edit10Old -New $edit10New `
    -Description '10/10 AuthService.Logout: deactivate all device tokens on logoutAllSessions'

Write-Host ''
Write-Host 'Done. Next steps:'
Write-Host '  1. dotnet build  -  build it locally to confirm it compiles before trusting it.'
Write-Host '  2. Coordinate DELETE /api/notifications/fcm-token with the Flutter team - they should call it with the device''s current FCM token right before/at logout.'
Write-Host '  3. Single-device logout still cannot deactivate a token server-side without the client calling the new DELETE endpoint first - LogoutDto was deliberately left untouched. Flag this in your API docs so mobile doesn''t skip it.'
