using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Subscription;

namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Super-admin per-tutor module activation service (§2.9 / REQ-ADM-034 through
/// REQ-ADM-038 / BR-ADM-010).
///
/// Owns every write to <c>TutorModuleAccess</c> from the admin surface. Each
/// operation runs the same three-step pipeline inside one transaction:
///
///   1. Diff the request against current state to detect whether anything
///      actually changed (idempotent no-op support — REQ-ADM-035 implies the
///      same module state must be re-applicable without error).
///   2. If yes: write the grant/revoke row(s), record one audit-trail entry per
///      action (REQ-ADM-055), invalidate the cached snapshot for the tutor AND
///      every assistant under that tutor via
///      <c>IUserAuthInvalidationService.InvalidateTutorAndAssistantsAsync</c>
///      so the change is visible on the next request (REQ-ADM-035).
///   3. Commit. On rollback the stamp bumps roll back too — the snapshot cache
///      becomes harmlessly stale until the next request rebuilds.
/// </summary>
public interface ITutorModuleAccessService
{
    /// <summary>
    /// Grants a single module to a tutor (REQ-ADM-034). Idempotent — granting
    /// an already-granted module returns success without performing writes.
    /// </summary>
    Task<Result<string>> GrantAsync(ModuleGrantRequest request, long adminUserId);

    /// <summary>
    /// Revokes a single module from a tutor (REQ-ADM-034 / BR-ADM-010).
    /// Idempotent. Existing data is preserved (REQ-ADM-050) — only the
    /// <c>TutorModuleAccess</c> row is removed.
    /// </summary>
    Task<Result<string>> RevokeAsync(ModuleRevokeRequest request, long adminUserId);

    /// <summary>
    /// Replaces the tutor's entire module access set in one transaction
    /// (REQ-ADM-037 / REQ-ADM-051). Computes the adds/removes diff against
    /// the current state, applies both, writes one audit row per actual change,
    /// and invalidates the cache once for the tutor + assistants fan-out.
    /// </summary>
    Task<Result<string>> BulkReplaceAsync(TutorModulesReplaceRequest request, long adminUserId);
}