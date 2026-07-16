using System.Net;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.VideoContentManagement;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements <c>VideoUnit</c> CRUD and paged listing (Track C / G-UNIT).
/// Kept separate from <see cref="VideoService"/> — same rationale as the
/// interface split (distinct sub-aggregate, own lifecycle).
/// </summary>
public sealed class VideoUnitService : IVideoUnitService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;
    private readonly IFileAccessService _fileAccess;

    public VideoUnitService(
        IUnitOfWork unitOfWork, IStringLocalizer<Domain.Resources.Messages> localizer,
        IFileAccessService fileAccess)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _fileAccess = fileAccess;
    }

    public async Task<Result<CreateVideoUnitResponse>> CreateUnitAsync(
     long teacherId, long actingUserId, VideoUnitRequest request)
    {
        string title = request.Title?.Trim() ?? string.Empty;

        var unit = new VideoUnit
        {
            TeacherId = teacherId,
            Title = title,
            Description = request.Description?.Trim(),
            CreatedByUserId = actingUserId,
            CreateAt = DateTime.UtcNow,
        };

        // 1. Save the unit first (so we have its Id).
        await _unitOfWork.VideoUnitsRepo.AddUnitAsync(unit);
        await _unitOfWork.SaveChangesAsync();

        // 2. If scopes are provided, convert and persist them.
        if (request.Scopes is { Count: > 0 })
        {
            var unitScopes = request.Scopes
                .Select(s => new VideoUnitScope
                {
                    VideoUnitId = unit.Id,
                    TeacherId = teacherId,
                    ScopeType = s.ScopeType,
                    SessionId = s.SessionId,
                    SessionGroupId = s.SessionGroupId,
                    AssignedByUserId = actingUserId,
                    AssignedAt = DateTime.UtcNow,
                })
                .ToList();

            await _unitOfWork.VideoUnitsRepo.AddUnitScopesAsync(unitScopes);
            await _unitOfWork.SaveChangesAsync();
        }

        return Result<CreateVideoUnitResponse>.Success(
            new CreateVideoUnitResponse { VideoUnitId = unit.Id },
            _localizer,
            VideoConstants.Messages.VideoUnitCreated,
            HttpStatusCode.Created);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> UpdateUnitAsync(
        long teacherId, long unitId, VideoUnitRequest request)
    {
        var unit = await _unitOfWork.VideoUnitsRepo.GetUnitByIdAndTeacherAsync(unitId, teacherId);
        if (unit is null)
            return Result<bool>.Failure(
                _localizer, VideoConstants.Messages.VideoUnitNotFound, HttpStatusCode.NotFound);

        unit.Title = request.Title?.Trim() ?? unit.Title;
        unit.Description = request.Description?.Trim();

        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, VideoConstants.Messages.VideoUnitUpdated);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteUnitAsync(long teacherId, long unitId)
    {
        var unit = await _unitOfWork.VideoUnitsRepo.GetUnitByIdAndTeacherAsync(unitId, teacherId);
        if (unit is null)
            return Result<bool>.Failure(
                _localizer, VideoConstants.Messages.VideoUnitNotFound, HttpStatusCode.NotFound);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Confirmed decision: deleting a unit never orphans or cascades
            // its videos — they become loose (UnitId = null) immediately.
            await _unitOfWork.VideoUnitsRepo.DetachVideosFromUnitAsync(unitId);
            await _unitOfWork.VideoUnitsRepo.SoftDeleteUnitAsync(unit, DateTime.UtcNow);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<bool>.Success(true, _localizer, VideoConstants.Messages.VideoUnitDeleted);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<TeacherVideoUnitListItemDto>>>>
        GetTeacherUnitsAsync(long teacherId, TeacherVideoUnitListRequest request)
    {
        var (rows, totalCount) = await _unitOfWork.VideoUnitsRepo
            .GetTeacherUnitsPagedAsync(teacherId, request.Search, request.Page, request.PageSize);

        var items = rows.Select(r => new TeacherVideoUnitListItemDto
        {
            Id = r.Id,
            Title = r.Title,
            Description = r.Description,
            VideoCount = r.VideoCount,
            SeenStudentCount = r.SeenStudentCount,
            UnseenStudentCount = r.UnseenStudentCount,
            CreatedAt = r.CreatedAt,
        }).ToList();

        var response = new PaginatedResponse<List<TeacherVideoUnitListItemDto>>
        {
            data = items,
            page = request.Page,
            pageSize = request.PageSize,
            totalCount = totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
        };

        return Result<PaginatedResponse<List<TeacherVideoUnitListItemDto>>>.Success(response, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<TeacherVideoListItemDto>>>>
        GetVideosInUnitAsync(long teacherId, long unitId, TeacherVideoListRequest request)
    {
        var unit = await _unitOfWork.VideoUnitsRepo.GetUnitByIdAndTeacherAsync(unitId, teacherId);
        if (unit is null)
            return Result<PaginatedResponse<List<TeacherVideoListItemDto>>>.Failure(
                _localizer, VideoConstants.Messages.VideoUnitNotFound, HttpStatusCode.NotFound);

        var (rows, totalCount) = await _unitOfWork.VideoUnitsRepo
            .GetVideosInUnitPagedAsync(unitId, teacherId, request.Search, request.Page, request.PageSize);

        // Batch-resolve the page's cover-photo file ids to opaque PublicId + gated URL in one
        // query — identical to VideoService.GetTeacherVideosAsync so both video lists match.
        var photoIds = rows.Where(r => r.VideoPhotoFileId is not null)
            .Select(r => r.VideoPhotoFileId!.Value).Distinct().ToList();
        var photosById = photoIds.Count == 0
            ? new Dictionary<long, FileObject>()
            : (await _unitOfWork.FileObjectsRepo.GetByIdsAsync(photoIds)).ToDictionary(f => f.Id);

        var items = rows.Select(r => new TeacherVideoListItemDto
        {
            Id = r.Id,
            Title = r.Title,
            Description = r.Description,
            SourceUrl = r.SourceUrl,
            SourceType = r.SourceType,
            DurationSeconds = r.DurationSeconds,
            StudentsInScope = r.StudentsInScope,
            TotalOpens = r.TotalOpens,
            SeenStudentCount = r.SeenStudentCount,
            UnseenStudentCount = r.UnseenStudentCount,
            Status = r.Status,
            PublishDate = r.PublishDate,
            VideoPhotoFileId = r.VideoPhotoFileId is long pid && photosById.TryGetValue(pid, out var photo)
                ? photo.PublicId
                : null,
            VideoPhotoUrl = r.VideoPhotoFileId is long pid2 && photosById.TryGetValue(pid2, out var photo2)
                ? _fileAccess.BuildGatedUrl(photo2.PublicId)
                : null,
            QuestionsNumber = r.QuestionsNumber,
            AttachmentsNumber = r.AttachmentsNumber,
            CreatedAt = r.CreatedAt,
        }).ToList();

        var response = new PaginatedResponse<List<TeacherVideoListItemDto>>
        {
            data = items,
            page = request.Page,
            pageSize = request.PageSize,
            totalCount = totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
        };

        return Result<PaginatedResponse<List<TeacherVideoListItemDto>>>.Success(response, _localizer);
    }

    public async Task<Result<VideoUnitResponse>> GetUnitWithScopesAsync(long unitId, long teacherId)
    {
        var unit = await _unitOfWork.VideoUnitsRepo.GetUnitWithScopesAsync(unitId, teacherId);
        if (unit is null)
            return Result<VideoUnitResponse>.Failure(
                _localizer[VideoConstants.Messages.VideoUnitNotFound],
                HttpStatusCode.NotFound);

        var response = new VideoUnitResponse
        {
            Id = unit.Id,
            Title = unit.Title,
            Description = unit.Description,
            Scopes = unit.Scopes.Select(s => new VideoScopeInputDto
            {
                ScopeType = s.ScopeType,
                SessionId = s.SessionId,
                SessionGroupId = s.SessionGroupId,
            }).ToList(),
        };

        return Result<VideoUnitResponse>.Success(response, _localizer, "Success", HttpStatusCode.OK);
    }
}
