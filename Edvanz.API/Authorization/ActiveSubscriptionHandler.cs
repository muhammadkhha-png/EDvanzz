using Edvanz.API.Attributes;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Helpers;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Edvanz.API.Authorization;

/// <summary>
/// Policy handler (§8.2) that enforces ActiveSubscriptionRequirement for every
/// authenticated request unless the endpoint is whitelisted via [AllowExpiredSubscription].
///
/// FAST PATH (NFR-SUB-007):
///   1. Skip when the user is anonymous or the role is SuperAdmin.
///   2. Skip when the endpoint carries [AllowExpiredSubscription].
///   3. Read the cached projection from Redis (Phase 05 — ISubscriptionCacheService).
///      Cache miss → fetch from DB and write-through.
///   4. Derive status via SubscriptionStatusCalculator (status is NEVER stored — D-08).
///   5. Allow Active or ExpiringSoon. Fail (403) on Expired.
///
/// EC-19 (Redis down):
///   Cache.GetAsync swallows IDistributedCache exceptions and returns null. The
///   handler then falls through to the DB read and continues — degrading
///   performance but not correctness.
///
/// ASSISTANT FLOW (BR-SUB-002):
///   Assistants share their tutor's subscription. The handler resolves the tutor
///   via ICurrentUserService.GetAssistantDataAsync().Tutor.Id when Role = Assistant,
///   otherwise resolves the teacher row from the user id directly.
/// </summary>
public class ActiveSubscriptionHandler : AuthorizationHandler<ActiveSubscriptionRequirement>
{
    private const string SuperAdminRole = "SuperAdmin";
    private const string AssistantRole = "Assistant";
    private const string TeacherRole = "Teacher";

    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubscriptionCacheService _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ActiveSubscriptionHandler> _logger;

    public ActiveSubscriptionHandler(
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ISubscriptionCacheService cache,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ActiveSubscriptionHandler> logger)
    {
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ActiveSubscriptionRequirement requirement)
    {
        // ── 1. Anonymous / SuperAdmin pass-through ──
        if (_currentUser.UserId is null)
        {
            // No authenticated user → another upstream policy (RequireAuthenticatedUser)
            // handles the rejection. Do NOT mark this requirement satisfied.
            return;
        }

        if (string.Equals(_currentUser.Role, SuperAdminRole, StringComparison.Ordinal))
        {
            context.Succeed(requirement);
            return;
        }

        // ── 2. Endpoint whitelist ──
        if (HasAllowExpiredSubscriptionAttribute())
        {
            context.Succeed(requirement);
            return;
        }

        // ── 3. Resolve teacherId ──
        long? teacherId = await ResolveTeacherIdAsync();
        if (teacherId is null)
        {
            // No teacher record for this user (shouldn't happen for Teacher/Assistant
            // roles after a successful login, but fail closed to be safe).
            _logger.LogWarning(
                "ActiveSubscriptionHandler: could not resolve teacherId for user {UserId} role {Role}",
                _currentUser.UserId, _currentUser.Role);
            return;
        }

        // ── 4. Cache-then-DB, derive status ──
        var projection = await _cache.GetAsync(teacherId.Value);
        if (projection is null)
        {
            projection = await _unitOfWork.Users.GetCurrentSubscriptionStatusAsync(teacherId.Value);
            if (projection is not null)
            {
                await _cache.SetAsync(teacherId.Value, projection);
            }
        }

        // ── 5. Status check ──
        var subForStatus = projection is null
            ? null
            : new TeacherSubscription
            {
                StartDate = projection.StartDate,
                EndDate = projection.EndDate,
                IsCurrent = true
            };

        var status = SubscriptionStatusCalculator.Derive(subForStatus, DateTime.UtcNow);

        if (status == SubscriptionStatus.Active || status == SubscriptionStatus.ExpiringSoon)
        {
            context.Succeed(requirement);
            return;
        }

        // Expired (or no subscription row at all). Fall through without Succeed().
        _logger.LogInformation(
            "ActiveSubscriptionHandler: blocked teacher {TeacherId} (status {Status})",
            teacherId.Value, status);
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    /// <summary>
    /// Reads the [AllowExpiredSubscription] attribute from the matched endpoint's
    /// metadata. Endpoint routing populates this BEFORE authorization runs, so we
    /// can rely on it here.
    /// </summary>
    private bool HasAllowExpiredSubscriptionAttribute()
    {
        var endpoint = _httpContextAccessor.HttpContext?.GetEndpoint();
        return endpoint?.Metadata.GetMetadata<AllowExpiredSubscriptionAttribute>() is not null;
    }

    /// <summary>
    /// Maps the authenticated user to a teacherId:
    ///   - Teacher role: lookup teacher by user id.
    ///   - Assistant role: load assistant, return the owning tutor's id (BR-SUB-002).
    /// Returns null when no mapping exists.
    /// </summary>
    private async Task<long?> ResolveTeacherIdAsync()
    {
        long userId = _currentUser.UserId!.Value;

        if (string.Equals(_currentUser.Role, AssistantRole, StringComparison.Ordinal))
        {
            var assistant = await _currentUser.GetAssistantDataAsync();
            return assistant?.TeacherAccountId;
        }

        if (string.Equals(_currentUser.Role, TeacherRole, StringComparison.Ordinal))
        {
            var teacher = await _unitOfWork.Users.GetTeacherByUserIdAsync(userId);
            return teacher?.Id;
        }

        return null;
    }
}