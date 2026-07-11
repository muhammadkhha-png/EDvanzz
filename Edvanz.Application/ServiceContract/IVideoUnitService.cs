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

}
