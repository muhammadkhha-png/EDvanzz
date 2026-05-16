using System.Collections.Generic;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Per-user authorization snapshot consumed by the live-permission system
/// (REQ-USR-013 / REQ-USR-027 / BR-ADM-010).
///
/// Cached in Redis under the key <c>auth:user:{userId}</c> and refreshed
/// on every change to the underlying user state (permissions, templates,
/// modules, active status, security stamp). The snapshot is the single source
/// of truth for authorization decisions at request time — the JWT carries only
/// stable identity and a SecurityStamp marker.
///
/// IMMUTABILITY:
///   Treated as immutable once constructed. Any change to underlying state
///   results in cache invalidation followed by a fresh DB read on the next
///   request — never an in-place mutation of an existing snapshot.
///
/// SERIALIZATION:
///   Persisted as JSON via System.Text.Json (camelCase) — same convention as
///   <see cref="CurrentSubscriptionStatusProjection"/>.
/// </summary>
public sealed class UserAuthSnapshot
{
    /// <summary>
    /// The user's primary key (matches <c>User.Id</c> and the JWT
    /// <c>NameIdentifier</c> claim).
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// User role as stored on <c>User.UserType</c> — "Teacher", "Assistant",
    /// "Student", "Parent", "SuperAdmin". Mirrored from the JWT role claim
    /// for fast in-handler checks without re-parsing the principal.
    /// </summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Owning tutor account id used for subscription / data-scope resolution.
    ///   - Teacher: their own <c>Teacher.Id</c>.
    ///   - Assistant: <c>Assistant.TeacherAccountId</c> (BR-SUB-002 / BR-USR-005).
    ///   - SuperAdmin / Student / Parent: null.
    /// </summary>
    public long? TeacherScopeId { get; init; }

    /// <summary>
    /// True when the underlying <c>User.IsActive</c> flag is set. False after a
    /// tutor deactivates an assistant (REQ-USR-008) or a super-admin
    /// deactivates a tutor — the next request is rejected with 401.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Current value of <c>User.SecurityStamp</c>. Compared against the
    /// <c>SecurityStamp</c> claim on the incoming JWT by
    /// <c>SecurityStampValidationMiddleware</c>; a mismatch invalidates the
    /// token (REQ-USR-013).
    /// </summary>
    public string SecurityStamp { get; init; } = string.Empty;

    /// <summary>
    /// Modules the user's tutor scope currently has access to (BR-ADM-010).
    /// Empty for SuperAdmin / Student / Parent.
    /// Module names match <c>Module.Name</c> exactly — case-sensitive lookups
    /// are performed against this set.
    /// </summary>
    public HashSet<string> Modules { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Granular permissions assigned to this user in
    /// <c>"{ModuleName}.{PermissionName}"</c> format (matches the existing
    /// <c>UserPermissionService.GetUserPermissionsToToken</c> output).
    /// Populated only for Assistants — Teachers infer permission via Modules.
    /// </summary>
    public HashSet<string> Permissions { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}