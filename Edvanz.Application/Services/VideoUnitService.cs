using System.Net;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.VideoContentManagement;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
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

        // Block (containment): a video must live inside a unit, and its scope must
        // stay within its units' coverage. Refuse the delete if any member video
        // would be left with no unit at all, or targeting sessions no OTHER unit of
        // theirs covers. The response names the offending video(s).
        var deleteGuard = await EnsureUnitChangeKeepsVideosCoveredAsync(
            teacherId, unitId,
            Array.Empty<(VideoScopeType, long?, long?)>(),
            unitIsBeingDeleted: true,
            VideoConstants.Messages.UnitDeleteWouldOrphanVideos);
        if (!deleteGuard.IsSuccess)
            return Result<bool>.Failure(deleteGuard);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Guard above guarantees no member video is orphaned; detaching the
            // remaining join rows here keeps videos that survive (because they are
            // also in another unit) consistent.
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
                _localizer, VideoConstants.Messages.VideoUnitNotFound,
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

    // ══════════════════════════════════════════════════════════════════════════
    // UNIT SCOPE CRUD (append / replace / delete a unit's session/group targets)
    // ══════════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<AppendUnitScopesResponse>> AppendUnitScopesAsync(
        long teacherId, long actingUserId, long unitId, AssignUnitScopesRequest request)
    {
        if (request.Scopes is null || request.Scopes.Count == 0)
            return Result<AppendUnitScopesResponse>.Failure(
                _localizer, VideoConstants.Messages.ScopeCannotBeEmpty, HttpStatusCode.BadRequest);

        var unit = await _unitOfWork.VideoUnitsRepo.GetUnitWithScopesAsync(unitId, teacherId);
        if (unit is null)
            return Result<AppendUnitScopesResponse>.Failure(
                _localizer, VideoConstants.Messages.VideoUnitNotFound, HttpStatusCode.NotFound);

        var shapeError = ValidateScopeShape(request.Scopes);
        if (shapeError is not null)
            return Result<AppendUnitScopesResponse>.Failure(
                _localizer, shapeError, HttpStatusCode.BadRequest);

        var ownershipError = await ValidateScopeOwnershipAsync(teacherId, request.Scopes);
        if (ownershipError is not null)
            return Result<AppendUnitScopesResponse>.Failure(
                _localizer, ownershipError, HttpStatusCode.BadRequest);

        // Append only GROWS coverage, so it can never uncover a member video —
        // no block guard needed. Dedupe against existing rows and within the batch.
        int skipped = 0;
        var rowsToAdd = new List<VideoUnitScope>();
        var utcNow = DateTime.UtcNow;

        foreach (var input in request.Scopes)
        {
            bool duplicate =
                unit.Scopes.Any(s => s.ScopeType == input.ScopeType
                    && s.SessionId == input.SessionId && s.SessionGroupId == input.SessionGroupId)
             || rowsToAdd.Any(s => s.ScopeType == input.ScopeType
                    && s.SessionId == input.SessionId && s.SessionGroupId == input.SessionGroupId);

            if (duplicate) { skipped++; continue; }

            rowsToAdd.Add(new VideoUnitScope
            {
                VideoUnitId = unitId,
                TeacherId = teacherId,
                ScopeType = input.ScopeType,
                SessionId = input.SessionId,
                SessionGroupId = input.SessionGroupId,
                AssignedByUserId = actingUserId,
                AssignedAt = utcNow,
            });
        }

        if (rowsToAdd.Count > 0)
        {
            await _unitOfWork.VideoUnitsRepo.AddUnitScopesAsync(rowsToAdd);
            await _unitOfWork.SaveChangesAsync();
        }

        return Result<AppendUnitScopesResponse>.Success(new AppendUnitScopesResponse
        {
            ScopesAdded = rowsToAdd.Count,
            ScopesSkipped = skipped,
        }, _localizer, VideoConstants.Messages.ScopesAssigned);
    }

    /// <inheritdoc />
    public async Task<Result<ReplaceUnitScopesResponse>> ReplaceUnitScopesAsync(
        long teacherId, long actingUserId, long unitId, AssignUnitScopesRequest request)
    {
        if (request.Scopes is null || request.Scopes.Count == 0)
            return Result<ReplaceUnitScopesResponse>.Failure(
                _localizer, VideoConstants.Messages.ScopeCannotBeEmpty, HttpStatusCode.BadRequest);

        var unit = await _unitOfWork.VideoUnitsRepo.GetUnitByIdAndTeacherAsync(unitId, teacherId);
        if (unit is null)
            return Result<ReplaceUnitScopesResponse>.Failure(
                _localizer, VideoConstants.Messages.VideoUnitNotFound, HttpStatusCode.NotFound);

        var shapeError = ValidateScopeShape(request.Scopes);
        if (shapeError is not null)
            return Result<ReplaceUnitScopesResponse>.Failure(
                _localizer, shapeError, HttpStatusCode.BadRequest);

        var ownershipError = await ValidateScopeOwnershipAsync(teacherId, request.Scopes);
        if (ownershipError is not null)
            return Result<ReplaceUnitScopesResponse>.Failure(
                _localizer, ownershipError, HttpStatusCode.BadRequest);

        // Replace may SHRINK coverage — block (409) if the new set would leave any
        // member video targeting sessions the unit no longer covers.
        var newTargets = request.Scopes
            .Select(s => (s.ScopeType, s.SessionId, s.SessionGroupId))
            .ToList();
        var guard = await EnsureUnitChangeKeepsVideosCoveredAsync(
            teacherId, unitId, newTargets, unitIsBeingDeleted: false,
            VideoConstants.Messages.UnitScopeChangeUncoversVideos);
        if (!guard.IsSuccess)
            return Result<ReplaceUnitScopesResponse>.Failure(guard);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            int oldCount = await _unitOfWork.VideoUnitsRepo.CountScopesForUnitAsync(unitId);
            await _unitOfWork.VideoUnitsRepo.DeleteAllScopesForUnitAsync(unitId);

            var utcNow = DateTime.UtcNow;
            var newRows = request.Scopes
                .GroupBy(s => (s.ScopeType, s.SessionId, s.SessionGroupId))
                .Select(g => g.First())
                .Select(input => new VideoUnitScope
                {
                    VideoUnitId = unitId,
                    TeacherId = teacherId,
                    ScopeType = input.ScopeType,
                    SessionId = input.SessionId,
                    SessionGroupId = input.SessionGroupId,
                    AssignedByUserId = actingUserId,
                    AssignedAt = utcNow,
                })
                .ToList();

            await _unitOfWork.VideoUnitsRepo.AddUnitScopesAsync(newRows);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<ReplaceUnitScopesResponse>.Success(new ReplaceUnitScopesResponse
            {
                ScopesAdded = newRows.Count,
                ScopesRemoved = oldCount,
            }, _localizer, VideoConstants.Messages.ScopesReplaced);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> RemoveUnitScopeAsync(long teacherId, long unitId, long scopeId)
    {
        var unit = await _unitOfWork.VideoUnitsRepo.GetUnitWithScopesAsync(unitId, teacherId);
        if (unit is null)
            return Result<bool>.Failure(
                _localizer, VideoConstants.Messages.VideoUnitNotFound, HttpStatusCode.NotFound);

        var target = unit.Scopes.FirstOrDefault(s => s.Id == scopeId);
        if (target is null)
            return Result<bool>.Failure(
                _localizer, VideoConstants.Messages.ScopeNotFound, HttpStatusCode.NotFound);

        // Removing a scope SHRINKS coverage — block if the remaining set would leave
        // a member video uncovered. The response names the offending video(s).
        var remainingTargets = unit.Scopes
            .Where(s => s.Id != scopeId)
            .Select(s => (s.ScopeType, s.SessionId, s.SessionGroupId))
            .ToList();
        var guard = await EnsureUnitChangeKeepsVideosCoveredAsync(
            teacherId, unitId, remainingTargets, unitIsBeingDeleted: false,
            VideoConstants.Messages.UnitScopeChangeUncoversVideos);
        if (!guard.IsSuccess)
            return Result<bool>.Failure(guard);

        bool removed = await _unitOfWork.VideoUnitsRepo
            .DeleteUnitScopeByIdAndTeacherAsync(scopeId, teacherId);
        if (!removed)
            return Result<bool>.Failure(
                _localizer, VideoConstants.Messages.ScopeNotFound, HttpStatusCode.NotFound);

        return Result<bool>.Success(true, _localizer, VideoConstants.Messages.ScopeRemoved);
    }

    /// <inheritdoc />
    public async Task<Result<AllowedScopeTargetsDto>> GetAllowedScopeTargetsAsync(
        long teacherId, IReadOnlyCollection<long>? unitIds, long? videoId)
    {
        List<long> resolvedUnitIds;

        if (videoId is long vid)
        {
            var video = await _unitOfWork.VideoAssetsRepo.GetVideoByIdAndTeacherAsync(vid, teacherId);
            if (video is null)
                return Result<AllowedScopeTargetsDto>.Failure(
                    _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

            resolvedUnitIds = await _unitOfWork.VideoAssetsRepo.GetLinkedUnitIdsAsync(vid);
        }
        else
        {
            resolvedUnitIds = unitIds?.Distinct().ToList() ?? new List<long>();
            foreach (var uid in resolvedUnitIds)
            {
                var unit = await _unitOfWork.VideoUnitsRepo.GetUnitByIdAndTeacherAsync(uid, teacherId);
                if (unit is null)
                    return Result<AllowedScopeTargetsDto>.Failure(
                        _localizer, VideoConstants.Messages.VideoUnitNotFound, HttpStatusCode.NotFound);
            }
        }

        var dto = new AllowedScopeTargetsDto();
        if (resolvedUnitIds.Count == 0)
            return Result<AllowedScopeTargetsDto>.Success(dto, _localizer, "Success", HttpStatusCode.OK);

        var unitScopes = await _unitOfWork.VideoUnitsRepo.GetScopeRowsForUnitsAsync(resolvedUnitIds);

        var directSessionIds = unitScopes
            .Where(s => s.ScopeType == VideoScopeType.Session && s.SessionId.HasValue)
            .Select(s => s.SessionId!.Value)
            .ToHashSet();

        var groupIds = unitScopes
            .Where(s => s.ScopeType == VideoScopeType.SessionGroup && s.SessionGroupId.HasValue)
            .Select(s => s.SessionGroupId!.Value)
            .Distinct()
            .ToList();

        // Expand the units' group scopes into their member sessions (each row carries
        // the session name), so the picker can offer an individual session within a
        // covered group — that is a valid (subset) video target.
        IReadOnlyList<GroupSessionRow> groupSessions = groupIds.Count > 0
            ? await _unitOfWork.SessionsRepo.GetSessionsByGroupIdsAsync(teacherId, groupIds)
            : new List<GroupSessionRow>();

        var allSessionIds = new HashSet<long>(directSessionIds);
        var sessionNameById = new Dictionary<long, string>();
        foreach (var gs in groupSessions)
        {
            allSessionIds.Add(gs.Id);
            sessionNameById[gs.Id] = gs.SessionName;
        }

        var unnamedSessionIds = directSessionIds.Where(id => !sessionNameById.ContainsKey(id)).ToList();
        if (unnamedSessionIds.Count > 0)
        {
            var names = await _unitOfWork.SessionsRepo.GetSessionNamesByIdsAsync(teacherId, unnamedSessionIds);
            foreach (var kv in names)
                sessionNameById[kv.Key] = kv.Value;
        }

        dto.Sessions = allSessionIds
            .Select(id => new AllowedSessionTargetDto
            {
                SessionId = id,
                SessionName = sessionNameById.TryGetValue(id, out var n) ? n : $"#{id}",
            })
            .OrderBy(s => s.SessionName)
            .ToList();

        if (groupIds.Count > 0)
        {
            var groupNames = await _unitOfWork.SessionsRepo.GetGroupNamesByIdsAsync(teacherId, groupIds);
            dto.Groups = groupIds
                .Select(id => new AllowedGroupTargetDto
                {
                    SessionGroupId = id,
                    GroupName = groupNames.TryGetValue(id, out var n) ? n : $"#{id}",
                })
                .OrderBy(g => g.GroupName)
                .ToList();
        }

        return Result<AllowedScopeTargetsDto>.Success(dto, _localizer, "Success", HttpStatusCode.OK);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CONTAINMENT INTERNALS (shape/ownership + resolved-audience block guard)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ensures a unit-scope change (shrink or delete) leaves every member video's
    /// scope still covered. <paramref name="newUnitTargets"/> is the unit's scope
    /// AFTER the change (ignored when <paramref name="unitIsBeingDeleted"/> is true).
    /// Returns a successful result when the invariant holds, or a 409 naming the
    /// offending videos via <paramref name="errorKey"/>.
    /// </summary>
    private async Task<Result<bool>> EnsureUnitChangeKeepsVideosCoveredAsync(
        long teacherId, long unitId,
        IReadOnlyCollection<(VideoScopeType ScopeType, long? SessionId, long? SessionGroupId)> newUnitTargets,
        bool unitIsBeingDeleted, string errorKey)
    {
        var videoIds = await _unitOfWork.VideoUnitsRepo.GetVideoIdsInUnitAsync(unitId);
        if (videoIds.Count == 0)
            return Result<bool>.Success(true, HttpStatusCode.OK);

        var offendingTitles = new List<string>();

        foreach (var videoId in videoIds)
        {
            var video = await _unitOfWork.VideoAssetsRepo.GetVideoWithScopesAsync(videoId, teacherId);
            if (video is null)
                continue;

            // The video's OTHER units (this one excluded) still contribute coverage.
            var otherUnitIds = (await _unitOfWork.VideoAssetsRepo.GetLinkedUnitIdsAsync(videoId))
                .Where(u => u != unitId)
                .ToList();

            // Deleting this unit while the video has no other unit would leave it
            // without any unit at all — a video must live inside a unit.
            if (unitIsBeingDeleted && otherUnitIds.Count == 0)
            {
                offendingTitles.Add(video.Title);
                continue;
            }

            var videoSessions = await ResolveScopeSessionIdsAsync(
                teacherId, video.Scopes.Select(s => (s.ScopeType, s.SessionId, s.SessionGroupId)));

            // A scope-less video reaches nobody — trivially covered.
            if (videoSessions.Count == 0)
                continue;

            var allowedTargets = new List<(VideoScopeType, long?, long?)>();
            if (otherUnitIds.Count > 0)
            {
                var otherRows = await _unitOfWork.VideoUnitsRepo.GetScopeRowsForUnitsAsync(otherUnitIds);
                allowedTargets.AddRange(otherRows.Select(s => (s.ScopeType, s.SessionId, s.SessionGroupId)));
            }
            if (!unitIsBeingDeleted)
                allowedTargets.AddRange(newUnitTargets);

            var allowedSessions = await ResolveScopeSessionIdsAsync(teacherId, allowedTargets);
            if (!videoSessions.IsSubsetOf(allowedSessions))
                offendingTitles.Add(video.Title);
        }

        if (offendingTitles.Count == 0)
            return Result<bool>.Success(true, HttpStatusCode.OK);

        string label = string.Join(", ", offendingTitles.Distinct());
        return Result<bool>.Failure(
            _localizer, errorKey, new object?[] { label }, HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Resolves scope targets to the concrete session ids they reach (a group
    /// expands to its member sessions) — the "resolved audience" used by the
    /// containment guard. Mirrors <c>VideoService.ResolveScopeSessionIdsAsync</c>.
    /// </summary>
    private async Task<HashSet<long>> ResolveScopeSessionIdsAsync(
        long teacherId,
        IEnumerable<(VideoScopeType ScopeType, long? SessionId, long? SessionGroupId)> targets)
    {
        var sessionIds = new HashSet<long>();
        var groupIds = new List<long>();

        foreach (var t in targets)
        {
            if (t.ScopeType == VideoScopeType.Session && t.SessionId.HasValue)
                sessionIds.Add(t.SessionId.Value);
            else if (t.ScopeType == VideoScopeType.SessionGroup && t.SessionGroupId.HasValue)
                groupIds.Add(t.SessionGroupId.Value);
        }

        if (groupIds.Count > 0)
        {
            var rows = await _unitOfWork.SessionsRepo.GetSessionsByGroupIdsAsync(teacherId, groupIds);
            foreach (var r in rows)
                sessionIds.Add(r.Id);
        }

        return sessionIds;
    }

    /// <summary>
    /// Validates each scope row's shape (exactly one target, matching its type).
    /// Mirrors <c>VideoService.ValidateScopeShape</c>. Returns a message key on
    /// failure, null on success.
    /// </summary>
    private static string? ValidateScopeShape(IEnumerable<VideoScopeInputDto> scopes)
    {
        foreach (var s in scopes)
        {
            int populated =
                (s.SessionId.HasValue ? 1 : 0)
              + (s.SessionGroupId.HasValue ? 1 : 0);

            if (populated != 1)
                return VideoConstants.Messages.ScopeShapeInvalid;

            bool typeMatches = s.ScopeType switch
            {
                VideoScopeType.Session => s.SessionId.HasValue,
                VideoScopeType.SessionGroup => s.SessionGroupId.HasValue,
                _ => false,
            };

            if (!typeMatches)
                return VideoConstants.Messages.ScopeShapeInvalid;
        }
        return null;
    }

    /// <summary>
    /// Validates each scope target belongs to the calling teacher, reusing the
    /// video module's ownership check (target ownership is not video/unit-specific).
    /// Returns a message key on failure, null on success.
    /// </summary>
    private async Task<string?> ValidateScopeOwnershipAsync(
        long teacherId, IEnumerable<VideoScopeInputDto> scopes)
    {
        foreach (var s in scopes)
        {
            long targetId = s.ScopeType switch
            {
                VideoScopeType.Session => s.SessionId!.Value,
                VideoScopeType.SessionGroup => s.SessionGroupId!.Value,
                _ => 0,
            };

            bool owned = await _unitOfWork.VideoAssetsRepo
                .IsScopeTargetOwnedByTeacherAsync(teacherId, s.ScopeType, targetId);

            if (!owned)
                return VideoConstants.Messages.ScopeTargetNotFoundOrForeign;
        }
        return null;
    }
}
