using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.VideoContentManagement;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Defines the contract for Video Content Management Module (Module 14)
/// operations. Every method returns <see cref="Result{T}"/>; the
/// implementation never throws for business-rule violations — it returns
/// <c>Result.Failure(...)</c> with a localized message key.
///
/// MAPPING TO ENDPOINTS (Spec §3 contract table):
/// <list type="bullet">
///   <item>1 — POST /api/videos                        → <see cref="CreateVideoAsync"/></item>
///   <item>2 — POST /api/videos/{id}/scopes            → <see cref="AppendScopesAsync"/></item>
///   <item>3 — PUT  /api/videos/{id}/scopes            → <see cref="ReplaceScopesAsync"/></item>
///   <item>4 — DELETE /api/videos/{id}/scopes/{scopeId} → <see cref="RemoveScopeAsync"/></item>
///   <item>5 — DELETE /api/videos/{id}                 → <see cref="DeleteVideoAsync"/></item>
///   <item>6 — GET  /api/videos/teacher                → <see cref="GetTeacherVideosAsync"/></item>
///   <item>7 — GET  /api/videos/student                → <see cref="GetStudentVideosAsync"/></item>
///   <item>7b — GET /api/videos/parent                 → <see cref="GetParentVideosAsync"/></item>
///   <item>8 — POST /api/videos/{id}/start             → <see cref="StartWatchAsync"/></item>
///   <item>9 — POST /api/videos/{id}/stop              → <see cref="StopWatchAsync"/></item>
///   <item>10 — GET /api/videos/{id}/analytics         → <see cref="GetAnalyticsAsync"/></item>
/// </list>
///
/// TENANT SCOPE: every method that takes a teacherId / actingUserId trusts
/// those values to come from the JWT, never from the request body or route.
/// The controller resolves them from claims.
///
/// MODULE-ACTIVE GATE (BR-ADM-010):
/// Teacher and assistant endpoints rely on the existing
/// <c>[ModulePermission("Videos", ...)]</c> filter — same convention as
/// every other module — which checks the <c>module</c> JWT claim.
/// Student and parent endpoints have no such claim and call
/// <c>IModuleTeacherRepo.IsModuleActiveAsync</c> at runtime inside the
/// service before returning data.
/// </summary>
public interface IVideoService
{
    // ══════════════════════════════════════════════════════════════════════
    // TEACHER + ASSISTANT WRITE FLOWS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a new video for the teacher. The newly-created row has zero
    /// scope rows — Flutter immediately follows up with a
    /// <see cref="AppendScopesAsync"/> call.
    ///
    /// REQ-VCM-FR-01. Story A. Errors:
    /// <see cref="Domain.Constants.VideoConstants.Messages.InvalidUrl"/>,
    /// <see cref="Domain.Constants.VideoConstants.Messages.UnsupportedSource"/>.
    /// </summary>
    /// <param name="teacherId">Tenant scope from JWT.</param>
    /// <param name="actingUserId">User who clicked Create — Teacher or
    /// Assistant. Stored on <c>VideoAsset.CreatedByUserId</c>.</param>
    /// <param name="request">Create payload.</param>
    Task<Result<CreateVideoResponse>> CreateVideoAsync(
        long teacherId, long actingUserId, CreateVideoRequest request);

    /// <summary>
    /// Appends scope rows to an existing video. Idempotent on duplicates —
    /// scopes that match an existing row are skipped via the unique index
    /// and reported in <see cref="AppendScopesResponse.ScopesSkipped"/>.
    ///
    /// Story A step 7. Errors:
    /// <see cref="Domain.Constants.VideoConstants.Messages.VideoNotFound"/>,
    /// <see cref="Domain.Constants.VideoConstants.Messages.NotVideoOwner"/>,
    /// <see cref="Domain.Constants.VideoConstants.Messages.ScopeShapeInvalid"/>,
    /// <see cref="Domain.Constants.VideoConstants.Messages.ScopeTargetNotFoundOrForeign"/>.
    /// </summary>
    Task<Result<AppendScopesResponse>> AppendScopesAsync(
        long teacherId, long actingUserId, long videoAssetId, AssignScopesRequest request);

    /// <summary>
    /// Replaces ALL scope rows for a video. Wrapped in a single transaction:
    /// DELETE old → validate new → INSERT new. <c>VideoAnalytics</c> rows
    /// are deliberately untouched so a previously-targeted student's watch
    /// history survives reassignment (Story D).
    ///
    /// Errors as <see cref="AppendScopesAsync"/> plus
    /// <see cref="Domain.Constants.VideoConstants.Messages.ScopeCannotBeEmpty"/>.
    /// </summary>
    Task<Result<ReplaceScopesResponse>> ReplaceScopesAsync(
        long teacherId, long actingUserId, long videoAssetId, AssignScopesRequest request);

    /// <summary>
    /// Removes a single scope row. Refuses with
    /// <see cref="Domain.Constants.VideoConstants.Messages.LastScopeCannotBeRemoved"/>
    /// when this would leave the video with zero scope rows — the teacher
    /// should hard-delete the video instead.
    ///
    /// Spec endpoint #4 (Q3 decision: keep both PUT-replace and
    /// DELETE-single).
    /// </summary>
    Task<Result<bool>> RemoveScopeAsync(
        long teacherId, long videoAssetId, long scopeId);

    /// <summary>
    /// Hard-deletes a video. Inside a single transaction:
    /// <list type="number">
    ///   <item>Build the audit snapshot (asset + scopes + analytics +
    ///         aggregates).</item>
    ///   <item>INSERT the <see cref="Domain.Entities.VideoAssetAudit"/> row.</item>
    ///   <item>DELETE the <see cref="Domain.Entities.VideoAsset"/> row —
    ///         cascade FKs remove scopes, analytics, watch events.</item>
    ///   <item>COMMIT.</item>
    /// </list>
    ///
    /// REQ-VCM-BR-03. Story E.
    /// </summary>
    Task<Result<bool>> DeleteVideoAsync(
        long teacherId, long actingUserId, long videoAssetId);

    // ══════════════════════════════════════════════════════════════════════
    // TEACHER READ FLOWS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Paged list of the teacher's own videos. Story B teacher endpoint.
    /// </summary>
    Task<Result<PaginatedResponse<List<TeacherVideoListItemDto>>>>
        GetTeacherVideosAsync(long teacherId, TeacherVideoListRequest request);

    /// <summary>
    /// Per-video analytics report. Story F. Returns one row per student in
    /// the resolved scope, including students who have never opened the
    /// video. Aggregates appear at the top level of the response.
    /// </summary>
    Task<Result<VideoAnalyticsResponse>> GetAnalyticsAsync(
        long teacherId, long videoAssetId, VideoAnalyticsRequest request);

    // ══════════════════════════════════════════════════════════════════════
    // STUDENT READ + WATCH FLOWS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Paged list of videos visible to the calling student. Story B student
    /// endpoint. Includes the student's own watch state via the analytics
    /// LEFT JOIN.
    ///
    /// The service runs the runtime module-active gate
    /// (<c>IModuleTeacherRepo.IsModuleActiveAsync</c>) per BR-ADM-010 — the
    /// student has no <c>module</c> JWT claim that the
    /// <c>[ModulePermission]</c> filter could check.
    /// </summary>
    /// <param name="teacherId">The teacher whose videos the student is
    /// listing. Resolved by the controller from the
    /// <c>StudentTeacherLink</c> for this student.</param>
    /// <param name="teacherStudentId">The student's <c>TeacherStudent.Id</c>
    /// under that teacher, resolved the same way.</param>
    Task<Result<PaginatedResponse<List<StudentVideoListItemDto>>>>
        GetStudentVideosAsync(
            long teacherId, long teacherStudentId, StudentVideoListRequest request);

    /// <summary>
    /// Records the student's Open event and returns the embed URL + resume
    /// position. Story B step 5–9. Validates the student is in scope
    /// (BR-VCM-01) and the module is active.
    ///
    /// Errors:
    /// <see cref="Domain.Constants.VideoConstants.Messages.VideoNotFound"/>,
    /// <see cref="Domain.Constants.VideoConstants.Messages.VideoNotInScope"/>,
    /// <see cref="Domain.Constants.VideoConstants.Messages.ModuleDeactivated"/>.
    /// </summary>
    Task<Result<StartWatchResponse>> StartWatchAsync(
        long teacherId, long teacherStudentId, long videoAssetId, StartWatchRequest request);

    /// <summary>
    /// Records the student's Stop event with server-validated, clamped
    /// delta. Story C. Atomically increments
    /// <c>VideoAnalytics.TotalWatchSeconds</c> via
    /// <c>ExecuteUpdateAsync</c>.
    ///
    /// Errors:
    /// <see cref="Domain.Constants.VideoConstants.Messages.NoActiveSession"/>,
    /// <see cref="Domain.Constants.VideoConstants.Messages.VideoNotInScope"/>,
    /// <see cref="Domain.Constants.VideoConstants.Messages.ModuleDeactivated"/>.
    /// </summary>
    Task<Result<StopWatchResponse>> StopWatchAsync(
        long teacherId, long teacherStudentId, long videoAssetId, StopWatchRequest request);

    // ══════════════════════════════════════════════════════════════════════
    // PARENT READ FLOW (Q7(a))
    // ══════════════════════════════════════════════════════════════════════

   
    #region INTEGRATION HOOKS (called by admin teacher hard-purge flow)

    /// <summary>
    /// Hard-deletes every video owned by the given teacher, writes a
    /// <see cref="Domain.Entities.VideoAssetAudit"/> snapshot for each one, and
    /// cascades through to scopes, analytics, and watch events. Returns the
    /// count of videos purged.
    ///
    /// CALLED BY: the admin "permanent-purge teacher" flow that owns the full
    /// account-deletion transaction (e.g., a future <c>IAdminTeacherService</c>
    /// or extension on <c>IUserService</c>). This method is NOT exposed as an
    /// HTTP endpoint; it is an integration hook.
    ///
    /// TRANSACTION HANDLING: follows the project pattern (see
    /// <c>StudentUserService.InitializeStudentUserAsync</c>): inspects
    /// <c>IUnitOfWork.HasActiveTransaction</c>. When the caller already owns a
    /// transaction (the admin purge flow always will), participates in it.
    /// When called standalone (e.g., manual ops cleanup), opens its own.
    ///
    /// Audit rows survive the cascade by design — only when the teacher account
    /// row itself is deleted (with cascade configured to remove audits) does
    /// audit history go.
    ///
    /// Phase 2.2 decision context: <c>Teachers → VideoAssets = NO_ACTION</c> at
    /// the DB level, so the cascade chain does not auto-fire on teacher delete.
    /// This service-layer method substitutes for that DB cascade.
    /// </summary>
    /// <param name="teacherId">The teacher being permanently purged.</param>
    /// <param name="actingAdminUserId">The admin user performing the purge.
    /// Stored on every emitted <see cref="Domain.Entities.VideoAssetAudit"/>
    /// row's <c>DeletedByUserId</c>.</param>
    /// <returns>Result with the number of videos purged.</returns>
    Task<Result<int>> PurgeAllVideosForTeacherAsync(long teacherId, long actingAdminUserId);

    #endregion
}
