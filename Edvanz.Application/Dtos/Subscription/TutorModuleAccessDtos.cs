namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Input DTO for POST /api/admin/tutor-modules/grant (REQ-ADM-034 / BR-ADM-010).
/// Grants a single module to a tutor account. Idempotent.
/// </summary>
public class ModuleGrantRequest
{
    /// <summary>The Teacher.Id receiving the module grant.</summary>
    public long TeacherId { get; set; }

    /// <summary>The Module.Id to grant. Must reference an existing row in the Models table.</summary>
    public long ModuleId { get; set; }
}

/// <summary>
/// Input DTO for POST /api/admin/tutor-modules/revoke (REQ-ADM-034 / BR-ADM-010).
/// Revokes a single module from a tutor account. Idempotent.
/// REQ-ADM-050: data created while the module was active is preserved.
/// </summary>
public class ModuleRevokeRequest
{
    /// <summary>The Teacher.Id losing the module grant.</summary>
    public long TeacherId { get; set; }

    /// <summary>The Module.Id to revoke.</summary>
    public long ModuleId { get; set; }
}

/// <summary>
/// Input DTO for PUT /api/admin/tutor-modules/replace (REQ-ADM-037 / REQ-ADM-051).
/// Replaces the tutor's entire module access set with the supplied list.
/// Backs the "toggle module group with one tap" UX and the "apply feature
/// configuration template" flow.
///
/// Diff semantics:
///   - Modules in the request not currently granted → granted.
///   - Modules currently granted not in the request → revoked.
///   - Modules in both → left untouched (no audit row, no cache invalidation
///     for unchanged state).
/// </summary>
public class TutorModulesReplaceRequest
{
    /// <summary>The Teacher.Id whose module set is being replaced.</summary>
    public long TeacherId { get; set; }

    /// <summary>
    /// The desired full set of <c>Module.Id</c>s for this tutor after the call
    /// returns. Empty list is valid and revokes every module (effectively suspends
    /// all features without deactivating the account itself — distinct from
    /// REQ-ADM-013 subscription deactivation).
    /// </summary>
    public List<long> ModuleIds { get; set; } = new();
}