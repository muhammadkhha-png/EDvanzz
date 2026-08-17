using Edvanz.Domain.Constants;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace Edvanz.Application.Security
{
    /// <summary>
    /// Authorization handler for <see cref="PermissionRequirement"/> — the
    /// per-endpoint module/permission gate used by every <c>[ModulePermission]</c>
    /// attribute in the API.
    ///
    /// LIVE-DATA SOURCE (REQ-USR-013 / REQ-USR-027 / BR-ADM-010):
    ///   This handler resolves authorization against the per-request
    ///   <see cref="UserAuthSnapshot"/> deposited on <c>HttpContext.Items</c> by
    ///   <c>SecurityStampValidationMiddleware</c> — NOT against JWT claims.
    ///   The snapshot is freshly loaded from Redis (or DB on cache miss) on
    ///   every request, so any permission change saved by a tutor is visible
    ///   on the assistant's very next call.
    ///
    /// ARCHITECTURE NOTE:
    ///   The snapshot is read directly from <c>HttpContext.Items</c> using the
    ///   key defined in <see cref="AuthConstants.AuthSnapshotItemKey"/>. We do
    ///   NOT depend on any API-layer helper class — Onion Architecture forbids
    ///   Application → API references. The key constant and snapshot type both
    ///   live in the Domain layer, which both the API middleware and this
    ///   Application-layer handler can reference.
    ///
    /// DECISION MATRIX (preserved from v1 to keep call-site behavior identical):
    ///   - RoleOnly requirement present → succeed iff the user is in that role.
    ///   - SuperAdmin / Student / Parent → bypass (their access is governed
    ///     elsewhere, not by module/permission claims).
    ///   - CompleteProfile flow → only succeeds for the dedicated CompleteProfile
    ///     endpoint (legacy path; preserved for the Google-signup completion UI).
    ///   - Teacher → module check only.
    ///   - Assistant → module check AND permission check.
    ///
    /// DEFENSIVE FALLBACK:
    ///   If <c>HttpContext.Items</c> contains no snapshot (middleware
    ///   misconfigured or anonymous-flagged endpoint that somehow reaches a
    ///   policy), the request fails closed — the safer of the two failure modes.
    /// </summary>
    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        // Role constants — kept in this file (not promoted to a global) because
        // they are tightly coupled to the decision matrix above and used nowhere
        // else. Matches the v1 implementation's literal-string convention.
        private const string SuperAdminRole = "SuperAdmin";
        private const string StudentRole = "Student";
        private const string ParentRole = "Parent";
        private const string TeacherRole = "Teacher";
        private const string AssistantRole = "Assistant";
        private const string CenterRole = "Center";
        private const string CenterAssistantRole = "CenterAssistant";
        private const string CompleteProfilePermission = "CompleteProfile";
        /// <summary>
        /// Marker written to <see cref="AuthorizationFailureReason"/> when the caller's tutor
        /// scope does not have the required module enabled. Read by
        /// <see cref="Edvanz.API.Authorization.SubscriptionAuthorizationResultHandler"/> to return
        /// a clear "module not assigned" envelope instead of a bare 403.
        /// </summary>
        public const string ModuleNotAssignedReason = "module_not_assigned";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public PermissionHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc />
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var principal = context.User;

            // ── 1. RoleOnly fast path ───────────────────────────────────────
            // Endpoints decorated with [ModulePermission(roles: "X", roleOnly: true)]
            // bypass the module/permission system entirely — only the role matters.
            if (!string.IsNullOrEmpty(requirement.RoleOnly))
            {
                if (principal.IsInRole(requirement.RoleOnly))
                    context.Succeed(requirement);
                else
                    context.Fail();

                return Task.CompletedTask;
            }

            // ── 2. SuperAdmin / Student / Parent / Center tier bypass ───────
            // Same v1 behavior: these roles are governed by separate authorization
            // surfaces (e.g., SuperAdmin sees everything; Student/Parent endpoints
            // are typically scoped by user id, not by module/permission grants).
            //
            // Center / CenterAssistant are ROLE-SUFFICIENT in v1: a center operates its
            // own teachers by "acting as" one per request (X-Acting-Teacher-Id, validated
            // fail-closed against center membership by the tenant resolvers), so DATA
            // isolation is already enforced at resolution time — the center's snapshot has
            // no single teacher/module scope to gate on. Per-center-assistant granular
            // permissions are a P5 refinement; until then both center roles pass this gate.
            if (principal.IsInRole(SuperAdminRole)
                || principal.IsInRole(StudentRole)
                || principal.IsInRole(ParentRole)
                || principal.IsInRole(CenterRole)
                || principal.IsInRole(CenterAssistantRole))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // ── 3. Resolve the live snapshot from HttpContext.Items ─────────
            // Deposited by SecurityStampValidationMiddleware for every
            // authenticated request. Null indicates either:
            //   - An anonymous request that nonetheless triggered this handler
            //     (misconfigured endpoint — fail closed).
            //   - The middleware is not registered in the pipeline (deployment
            //     error — fail closed).
            UserAuthSnapshot? snapshot = ResolveSnapshot();

            if (snapshot is null)
            {
                context.Fail();
                return Task.CompletedTask;
            }

            // ── 4. CompleteProfile flow ─────────────────────────────────────
            // The Google sign-up completion path issues a token with only the
            // "CompleteProfile" permission. It is preserved here because the
            // CompleteProfile token is generated by GenerateCompleteProfileToken
            // (not GenerateJwtToken), which still emits the legacy Permission
            // claim. We read it from claims, not from the snapshot — the user
            // is mid-signup and has no DB row yet to snapshot.
            //
            // NOTE: This path is INDEPENDENT of the live snapshot system; it
            // exists only because GenerateCompleteProfileToken issues a special-
            // purpose JWT that doesn't go through the SecurityStamp pipeline.
            bool isCompleteProfileToken = principal.HasClaim(
                c => c.Type == "Permission"
                  && string.Equals(c.Value, CompleteProfilePermission,
                                   StringComparison.OrdinalIgnoreCase));

            if (isCompleteProfileToken)
            {
                if (string.Equals(requirement.Permission, CompleteProfilePermission,
                                  StringComparison.OrdinalIgnoreCase))
                {
                    context.Succeed(requirement);
                }
                else
                {
                    context.Fail();
                }
                return Task.CompletedTask;
            }

            // ── 5. Module gate (Teacher + Assistant) ────────────────────────
            // Snapshot.Modules is OrdinalIgnoreCase, so the HashSet.Contains
            // already handles casing variations from the [ModulePermission]
            // attribute.
            if (!string.IsNullOrEmpty(requirement.Module))
            {
                if (!snapshot.Modules.Contains(requirement.Module))
                {
                    context.Fail(new AuthorizationFailureReason(this, ModuleNotAssignedReason));
                    return Task.CompletedTask;
                }
            }

            // ── 6. Teacher → module is sufficient ───────────────────────────
            if (principal.IsInRole(TeacherRole))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // ── 7. Assistant → require granular permission ──────────────────
            // The attribute may carry a null/empty permission (module-only
            // requirement). For Assistants we fail closed on that — an empty
            // permission requirement against an Assistant means the endpoint
            // forgot to specify one, which is a coding bug we shouldn't silently
            // allow past.
            if (string.IsNullOrEmpty(requirement.Permission))
            {
                context.Fail();
                return Task.CompletedTask;
            }

            // Permissions in the snapshot are "{ModuleName}.{PermissionName}"
            // (matches IUserPermissionService.GetUserPermissionsToToken). The
            // attribute supplies just the permission name, so we form the
            // qualified key here.
            string qualifiedPermission = $"{requirement.Module}.{requirement.Permission}";

            if (snapshot.Permissions.Contains(qualifiedPermission))
                context.Succeed(requirement);
            else
                context.Fail();

            return Task.CompletedTask;
        }

        // ════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════

        /// <summary>
        /// Pulls the snapshot the middleware deposited on this request's
        /// <c>HttpContext.Items</c>. Returns null when the middleware did not
        /// run for this request — handled as fail-closed by the caller.
        /// </summary>
        private UserAuthSnapshot? ResolveSnapshot()
        {
            HttpContext? httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
                return null;

            return httpContext.Items.TryGetValue(AuthConstants.AuthSnapshotItemKey, out var raw)
                ? raw as UserAuthSnapshot
                : null;
        }
    }
}