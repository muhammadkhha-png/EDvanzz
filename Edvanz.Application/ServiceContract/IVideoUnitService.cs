using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.VideoContentManagement;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Defines the contract for <c>VideoUnit</c> operations (Track C / G-UNIT).
/// Kept as its own service rather than folded into <see cref="IVideoService"/>
/// — units are a distinct sub-aggregate with their own CRUD/list shape.
/// Every method returns <see cref="Result{T}"/>; never throws for
/// business-rule violations.
/// </summary>
public interface IVideoUnitService
{
    /// <summary>Creates a new unit for the teacher. S1/S4 "create unit".</summary>
    Task<Result<CreateVideoUnitResponse>> CreateUnitAsync(
        long teacherId, long actingUserId, VideoUnitRequest request);

    /// <summary>Renames/updates a unit's description. S4 edit.</summary>
    Task<Result<bool>> UpdateUnitAsync(
        long teacherId, long unitId, VideoUnitRequest request);

    /// <summary>
    /// Soft-deletes a unit. Its videos are NOT deleted or orphaned — they
    /// become loose (<c>UnitId = null</c>) in the same transaction.
    /// </summary>
    Task<Result<bool>> DeleteUnitAsync(long teacherId, long unitId);

    /// <summary>Paged list of the teacher's units with rolled-up aggregates. Backs S2.</summary>
    Task<Result<PaginatedResponse<List<TeacherVideoUnitListItemDto>>>>
        GetTeacherUnitsAsync(long teacherId, TeacherVideoUnitListRequest request);

    /// <summary>Paged list of videos inside a specific unit. Backs the S6 drill-down.</summary>
    Task<Result<PaginatedResponse<List<TeacherVideoListItemDto>>>>
        GetVideosInUnitAsync(long teacherId, long unitId, TeacherVideoListRequest request);
    public Task<Result<VideoUnitResponse>> GetUnitWithScopesAsync(long unitId, long teacherId);

    /// <summary>
    /// Appends session/group targets to a unit's scope (idempotent on duplicates).
    /// Append only grows coverage, so it never uncovers a member video.
    /// </summary>
    Task<Result<AppendUnitScopesResponse>> AppendUnitScopesAsync(
        long teacherId, long actingUserId, long unitId, AssignUnitScopesRequest request);

    /// <summary>
    /// Replaces a unit's entire scope with the given set (the request is the exact
    /// desired scope, so omitted entries are removed; an empty list clears the scope).
    /// Because this can SHRINK coverage, it is rejected (409) if the new set would
    /// leave any member video targeting sessions the unit no longer covers — the
    /// response names those videos.
    /// </summary>
    Task<Result<ReplaceUnitScopesResponse>> ReplaceUnitScopesAsync(
        long teacherId, long actingUserId, long unitId, AssignUnitScopesRequest request);

    /// <summary>
    /// Returns the sessions/groups a video may be scoped to, derived from the target
    /// scope of the given units (groups expanded, with names) — powers the create
    /// screen's scope picker. (The edit screen gets the same data folded into the
    /// video detail via <c>GET /api/videos/{id}</c>.)
    /// </summary>
    Task<Result<AllowedScopeTargetsDto>> GetAllowedScopeTargetsAsync(
        long teacherId, IReadOnlyCollection<long> unitIds);

}
