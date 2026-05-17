namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Orchestrates the two-step "make a user's authorization change take effect
/// immediately" workflow (REQ-USR-013 / REQ-USR-027 / REQ-USR-008 / BR-ADM-010):
///
///   1. Bump <c>User.SecurityStamp</c> — all outstanding access tokens for
///      this user become invalid on their next request (rejected by
///      <c>SecurityStampValidationMiddleware</c>).
///   2. Invalidate the Redis <c>UserAuthSnapshot</c> entry — the next request
///      for this user rebuilds from the database, reflecting the new state.
///
/// CALL SITES (every service that mutates user access state):
///   - <c>AssistantService.UpdateAssistantAsync</c>      → <see cref="InvalidateUserAsync"/>
///   - <c>AssistantService.ToggleStatus</c>              → <see cref="InvalidateUserAsync"/>
///   - <c>AuthService.ChangePassword</c>                 → <see cref="InvalidateUserAsync"/>
///   - <c>PermissionService.UpdateAssistantPermissionsAsync</c> → <see cref="InvalidateUserAsync"/>
///   - <c>ProfileService.UpdateProfileAsync</c>          → <see cref="InvalidateUsersAsync"/> (fan-out)
///   - <c>ProfileService.DeleteProfileAsync</c>          → <see cref="InvalidateUsersAsync"/> (fan-out)
///   - Future super-admin module grant/revoke           → <see cref="InvalidateTutorAndAssistantsAsync"/>
///
/// TRANSACTION DISCIPLINE:
///   These methods MUST be called from inside the caller's existing transaction
///   block, BEFORE <c>SaveChangesAsync</c>. The User.SecurityStamp bump is a
///   normal EF Core update that participates in the transaction — if the
///   transaction rolls back, the stamp bump rolls back too, and the cache
///   invalidation (which already ran) becomes a harmless extra cache miss on
///   the next request. Failure mode is biased toward correctness.
/// </summary>
public interface IUserAuthInvalidationService
{
    /// <summary>
    /// Single-user invalidation: bumps the user's SecurityStamp and drops the
    /// Redis snapshot key. Called by every service that mutates exactly one
    /// user's effective access.
    ///
    /// IMPORTANT: Must be called BEFORE <c>SaveChangesAsync</c> so the stamp
    /// bump is part of the same transaction as the access-state change.
    /// </summary>
    /// <param name="userId">Target user id. No-op if the user does not exist.</param>
    Task InvalidateUserAsync(long userId);

    /// <summary>
    /// Bulk invalidation: bumps SecurityStamp on every listed user and drops
    /// each Redis snapshot key. Called by template-edit/delete (multiple
    /// assistants share a template) and by module-revocation fan-out.
    ///
    /// IMPORTANT: Must be called BEFORE <c>SaveChangesAsync</c> so the stamp
    /// bumps are part of the same transaction.
    /// </summary>
    /// <param name="userIds">Target user ids. Empty list is a safe no-op.</param>
    Task InvalidateUsersAsync(IReadOnlyCollection<long> userIds);

    /// <summary>
    /// Module-revocation fan-out (BR-ADM-010): bumps SecurityStamp and
    /// invalidates the cache for the tutor user AND every assistant under
    /// that tutor. Used by the super-admin module-grant/revoke flow when
    /// <c>TutorModuleAccess</c> is written, since module changes affect
    /// the tutor and all their assistants symmetrically.
    ///
    /// IMPORTANT: Must be called BEFORE <c>SaveChangesAsync</c>.
    /// </summary>
    /// <param name="teacherId">The Teacher.Id whose module access changed.</param>
    Task InvalidateTutorAndAssistantsAsync(long teacherId);
}