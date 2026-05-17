using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Edvanz.Application.Services;

/// <summary>
/// Default implementation of <see cref="IUserAuthInvalidationService"/>.
///
/// Implementation strategy:
///   - The SecurityStamp bump is performed by loading the User row, assigning
///     a fresh Guid string, and marking it Modified via the repository's
///     UpdateAsync. The change participates in the caller's existing
///     transaction — we never call SaveChangesAsync or commit here.
///   - The cache invalidation is fire-and-forget within the request — the
///     RedisUserAuthCacheService.InvalidateAsync swallows its own exceptions
///     and logs them, so a Redis hiccup doesn't break the caller's transaction.
///   - Order matters: bump stamp FIRST (so it's queued in the EF change tracker
///     before the caller's SaveChangesAsync), then invalidate the cache. If
///     invalidation fails, the stamp bump still ensures the next request is
///     rejected — the TTL is the safety net.
/// </summary>
public class UserAuthInvalidationService : IUserAuthInvalidationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserAuthCacheService _cache;
    private readonly ILogger<UserAuthInvalidationService> _logger;

    public UserAuthInvalidationService(
        IUnitOfWork unitOfWork,
        IUserAuthCacheService cache,
        ILogger<UserAuthInvalidationService> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InvalidateUserAsync(long userId)
    {
        await BumpStampAsync(userId);
        await _cache.InvalidateAsync(userId);
    }

    /// <inheritdoc />
    public async Task InvalidateUsersAsync(IReadOnlyCollection<long> userIds)
    {
        if (userIds is null || userIds.Count == 0)
            return;

        // Sequential bumps — the user count per template edit is small (typically
        // single-digit assistants per template), so a serial loop avoids the
        // EF Core change-tracker contention that parallel awaits would create
        // on a single DbContext instance.
        foreach (long userId in userIds)
        {
            await BumpStampAsync(userId);
        }

        // Cache invalidations can fire in parallel — they hit Redis directly
        // without touching the DbContext.
        await Task.WhenAll(userIds.Select(id => _cache.InvalidateAsync(id)));
    }

    /// <inheritdoc />
    public async Task InvalidateTutorAndAssistantsAsync(long teacherId)
    {
        // ── 1. Resolve the tutor's User row ──
        // The Teacher.UserId is the canonical link from the teacher account
        // to the User used for authentication.
        var teacher = await _unitOfWork.Users.GetTeacherByIdAsync(teacherId);
        if (teacher is null)
        {
            _logger.LogWarning(
                "InvalidateTutorAndAssistants: teacher {TeacherId} not found — nothing to invalidate",
                teacherId);
            return;
        }

        // ── 2. Resolve every assistant's UserId under this tutor ──
        // GetUserIdsByTeacherAccountIdAsync was added in Phase 3 specifically
        // for this fan-out. Includes deactivated assistants (they may still
        // hold a token we want to invalidate). Excludes soft-deleted rows.
        var assistantUserIds = await _unitOfWork.AssistantRepo
            .GetUserIdsByTeacherAccountIdAsync(teacherId);

        // ── 3. Build the combined invalidation set ──
        var allUserIds = new List<long>(assistantUserIds.Count + 1) { teacher.UserId };
        allUserIds.AddRange(assistantUserIds);

        // ── 4. Delegate to the bulk path ──
        // Same transaction semantics — must be called BEFORE the caller's
        // SaveChangesAsync.
        await InvalidateUsersAsync(allUserIds);
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    /// <summary>
    /// Bumps the SecurityStamp on the given user's row. The change is queued
    /// in the EF Core change tracker; it persists when the caller calls
    /// SaveChangesAsync as part of the surrounding transaction.
    /// </summary>
    private async Task BumpStampAsync(long userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user is null)
        {
            // Defensive: caller asked us to invalidate a user that doesn't
            // exist. Log and continue — the cache invalidation below will
            // still remove any orphan key.
            _logger.LogWarning(
                "BumpStamp: user {UserId} not found — skipping stamp bump",
                userId);
            return;
        }

        user.SecurityStamp = Guid.NewGuid().ToString();
        await _unitOfWork.Users.UpdateAsync(user);
    }
}