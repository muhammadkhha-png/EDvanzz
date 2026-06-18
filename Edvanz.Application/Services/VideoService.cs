using System.Net;
using System.Text.Json;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.VideoContentManagement;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements all Video Content Management Module (Module 14) operations.
/// Returns <see cref="Result{T}"/> for every flow; never throws on
/// business-rule violations.
///
/// ARCHITECTURAL NOTES:
/// <list type="bullet">
///   <item>All data access goes through <see cref="IUnitOfWork"/>'s named
///         repo methods — no raw expression predicates here.</item>
///   <item>Scope resolution delegates to <see cref="IVideoScopeResolver"/>.</item>
///   <item>URL parsing delegates to <see cref="IVideoUrlParser"/>.</item>
/// </list>
///
/// TRANSACTION POLICY (mirrors <c>StudentUserService</c>):
/// Methods that write multiple rows inspect
/// <c>IUnitOfWork.HasActiveTransaction</c>. When the caller already owns a
/// transaction (e.g., admin teacher-purge), this service participates. When
/// called standalone (the normal HTTP path), it manages its own.
///
/// RACE HANDLING — first-time open INSERT path:
/// <see cref="StartWatchAsync"/> uses a bounded retry loop around the
/// <c>VideoAnalytics</c> INSERT. Two simultaneous Open events from one
/// student (phone + tablet) race to insert; the unique index
/// <c>UX_VideoAnalytics_Video_Student</c> rejects the second; the service
/// catches <c>DbUpdateException</c> with a SQL Server unique-violation
/// number and retries via the increment branch (the row now exists).
///
/// PARENT ENDPOINT — DEFERRED (Q1(c)):
/// v1 ships without a parent endpoint. The student endpoint plus the
/// existing ParentChild → StudentTeacherLink / ParentChildTeacherLink data
/// model is sufficient to add it later without DB changes.
/// </summary>
public sealed class VideoService : IVideoService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVideoScopeResolver _scopeResolver;
    private readonly IVideoUrlParser _urlParser;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public VideoService(
        IUnitOfWork unitOfWork,
        IVideoScopeResolver scopeResolver,
        IVideoUrlParser urlParser,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _scopeResolver = scopeResolver;
        _urlParser = urlParser;
        _localizer = localizer;
    }

    // ══════════════════════════════════════════════════════════════════════
    // CREATE VIDEO (Story A)
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<CreateVideoResponse>> CreateVideoAsync(
        long teacherId, long actingUserId, CreateVideoRequest request)
    {
        string title = request.Title?.Trim() ?? string.Empty;
        if (title.Length == 0)
            return Result<CreateVideoResponse>.Failure(
                _localizer, VideoConstants.Messages.InvalidUrl, HttpStatusCode.BadRequest);

        // Parse URL — distinguishes "not a URL" from "unsupported provider".
        var parseOutcome = _urlParser.Parse(request.SourceUrl);
        if (!parseOutcome.IsSuccess)
        {
            string key = parseOutcome.Failure switch
            {
                VideoUrlParseFailure.UnsupportedSource => VideoConstants.Messages.UnsupportedSource,
                _ => VideoConstants.Messages.InvalidUrl,
            };
            return Result<CreateVideoResponse>.Failure(
                _localizer, key, HttpStatusCode.BadRequest);
        }

        var video = new VideoAsset
        {
            TeacherId = teacherId,
            Title = title,
            Description = request.Description?.Trim(),
            SourceUrl = request.SourceUrl.Trim(),
            SourceType = parseOutcome.Success!.SourceType,
            ExternalId = parseOutcome.Success.ExternalId,
            DurationSeconds = 0, // Story A: learned on first open.
            CreatedByUserId = actingUserId,
            CreateAt = DateTime.UtcNow,
        };

        await _unitOfWork.VideoAssetsRepo.AddVideoAsync(video);
        await _unitOfWork.SaveChangesAsync();

        return Result<CreateVideoResponse>.Success(
            new CreateVideoResponse { VideoAssetId = video.Id },
            _localizer,
            VideoConstants.Messages.VideoCreated,
            HttpStatusCode.Created);
    }

    // ══════════════════════════════════════════════════════════════════════
    // APPEND SCOPES (Story A step 7, idempotent on duplicates)
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<AppendScopesResponse>> AppendScopesAsync(
        long teacherId, long actingUserId, long videoAssetId, AssignScopesRequest request)
    {
        if (request.Scopes is null || request.Scopes.Count == 0)
            return Result<AppendScopesResponse>.Failure(
                _localizer, VideoConstants.Messages.ScopeCannotBeEmpty, HttpStatusCode.BadRequest);

        var video = await _unitOfWork.VideoAssetsRepo
            .GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
            return Result<AppendScopesResponse>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        var shapeError = ValidateScopeShape(request.Scopes);
        if (shapeError is not null)
            return Result<AppendScopesResponse>.Failure(
                _localizer, shapeError, HttpStatusCode.BadRequest);

        var ownershipError = await ValidateScopeOwnershipAsync(teacherId, request.Scopes);
        if (ownershipError is not null)
            return Result<AppendScopesResponse>.Failure(
                _localizer, ownershipError, HttpStatusCode.BadRequest);

        // Pre-load existing scope rows so we can dedupe client-side rather
        // than catching unique-violation exceptions per row. The DB unique
        // index UX_VideoScopes_Video_Type_Target remains as a safety net.
        var existing = await _unitOfWork.VideoAssetsRepo
            .GetVideoWithScopesAsync(videoAssetId, teacherId);
        // existing != null because we already verified ownership above.

        int skipped = 0;
        var rowsToAdd = new List<VideoScope>();
        var utcNow = DateTime.UtcNow;

        foreach (var input in request.Scopes)
        {
            bool isDuplicate = existing!.Scopes.Any(s =>
                s.ScopeType == input.ScopeType
             && s.TeacherStudentId == input.TeacherStudentId
             && s.SessionId == input.SessionId
             && s.SessionGroupId == input.SessionGroupId);

            if (isDuplicate)
            {
                skipped++;
                continue;
            }

            rowsToAdd.Add(BuildScopeEntity(input, video, actingUserId, utcNow));
        }

        if (rowsToAdd.Count > 0)
        {
            await _unitOfWork.VideoAssetsRepo.AddScopesAsync(rowsToAdd);
            await _unitOfWork.SaveChangesAsync();
        }

        // Dedup student count across the FULL scope set (existing + new).
        var allScopes = existing!.Scopes.Concat(rowsToAdd).ToList();
        var resolved = await _scopeResolver.ResolveFromPersistedScopesAsync(teacherId, allScopes);

        return Result<AppendScopesResponse>.Success(new AppendScopesResponse
        {
            ScopesAdded = rowsToAdd.Count,
            ScopesSkipped = skipped,
            StudentsInScope = resolved.Count,
        }, _localizer, VideoConstants.Messages.ScopesAssigned);
    }

    // ══════════════════════════════════════════════════════════════════════
    // REPLACE SCOPES (Story D, transactional)
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<ReplaceScopesResponse>> ReplaceScopesAsync(
        long teacherId, long actingUserId, long videoAssetId, AssignScopesRequest request)
    {
        if (request.Scopes is null || request.Scopes.Count == 0)
            return Result<ReplaceScopesResponse>.Failure(
                _localizer, VideoConstants.Messages.ScopeCannotBeEmpty, HttpStatusCode.BadRequest);

        var video = await _unitOfWork.VideoAssetsRepo
            .GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
            return Result<ReplaceScopesResponse>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        var shapeError = ValidateScopeShape(request.Scopes);
        if (shapeError is not null)
            return Result<ReplaceScopesResponse>.Failure(
                _localizer, shapeError, HttpStatusCode.BadRequest);

        var ownershipError = await ValidateScopeOwnershipAsync(teacherId, request.Scopes);
        if (ownershipError is not null)
            return Result<ReplaceScopesResponse>.Failure(
                _localizer, ownershipError, HttpStatusCode.BadRequest);

        int oldCount = await _unitOfWork.VideoAssetsRepo.CountScopesForVideoAsync(videoAssetId);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _unitOfWork.VideoAssetsRepo.DeleteAllScopesForVideoAsync(videoAssetId);

            var utcNow = DateTime.UtcNow;
            var newRows = request.Scopes
                .Select(input => BuildScopeEntity(input, video, actingUserId, utcNow))
                .ToList();

            await _unitOfWork.VideoAssetsRepo.AddScopesAsync(newRows);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            // VideoAnalytics is deliberately untouched so history survives
            // reassignment (Story D).
            var resolved = await _scopeResolver.ResolveFromPersistedScopesAsync(teacherId, newRows);

            return Result<ReplaceScopesResponse>.Success(new ReplaceScopesResponse
            {
                ScopesAdded = newRows.Count,
                ScopesRemoved = oldCount,
                StudentsInScope = resolved.Count,
            }, _localizer, VideoConstants.Messages.ScopesReplaced);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // REMOVE SINGLE SCOPE (Story D, endpoint #4)
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<bool>> RemoveScopeAsync(
        long teacherId, long videoAssetId, long scopeId)
    {
        // Verify ownership before probing scope ids — defends against
        // foreign-tenant id enumeration.
        var video = await _unitOfWork.VideoAssetsRepo
            .GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
            return Result<bool>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        int totalScopes = await _unitOfWork.VideoAssetsRepo.CountScopesForVideoAsync(videoAssetId);
        if (totalScopes <= 1)
            return Result<bool>.Failure(
                _localizer, VideoConstants.Messages.LastScopeCannotBeRemoved, HttpStatusCode.BadRequest);

        bool removed = await _unitOfWork.VideoAssetsRepo
            .DeleteScopeByIdAndTeacherAsync(scopeId, teacherId);
        if (!removed)
            return Result<bool>.Failure(
                _localizer, VideoConstants.Messages.ScopeNotFound, HttpStatusCode.NotFound);

        return Result<bool>.Success(true, _localizer, VideoConstants.Messages.ScopeRemoved);
    }

    // ══════════════════════════════════════════════════════════════════════
    // DELETE VIDEO (Story E, transactional with audit snapshot)
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteVideoAsync(
        long teacherId, long actingUserId, long videoAssetId)
    {
        var video = await _unitOfWork.VideoAssetsRepo
            .GetVideoWithScopesAsync(videoAssetId, teacherId);
        if (video is null)
            return Result<bool>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Build the snapshot BEFORE the NoAction fires — once we delete,
            // the rows are gone forever (REQ-VCM-BR-03).
            var analyticsRows = await _unitOfWork.VideoAssetsRepo
                .GetAnalyticsForAuditSnapshotAsync(videoAssetId);
            var aggregates = await _unitOfWork.VideoAssetsRepo
                .GetAnalyticsAggregatesAsync(teacherId, videoAssetId);

            var utcNow = DateTime.UtcNow;
            string snapshotJson = BuildAuditSnapshot(
                video, analyticsRows, aggregates, actingUserId, utcNow);

            var audit = new VideoAssetAudit
            {
                VideoAssetId = video.Id,
                TeacherId = teacherId,
                Action = VideoAuditAction.HardDelete,
                SnapshotJson = snapshotJson,
                SnapshotArchiveUrl = null,
                DeletedByUserId = actingUserId,
                DeletedAt = utcNow,
                CreateAt = utcNow,
            };

            await _unitOfWork.VideoAssetsRepo.AddAuditAsync(audit);

            // GetVideoWithScopesAsync is AsNoTracking. Re-fetch tracked for the Remove call.
            var trackedVideo = await _unitOfWork.VideoAssetsRepo
                .GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
            if (trackedVideo is null)
            {
                if (ownsTransaction)
                    await _unitOfWork.RollbackAsync();
                return Result<bool>.Failure(
                    _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);
            }

            await _unitOfWork.VideoAssetsRepo.DeleteVideoAsync(trackedVideo);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<bool>.Success(true, _localizer, VideoConstants.Messages.VideoDeleted);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // TEACHER VIDEO LIST (Story B teacher endpoint)
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<TeacherVideoListItemDto>>>>
        GetTeacherVideosAsync(long teacherId, TeacherVideoListRequest request)
    {
        var (rows, totalCount) = await _unitOfWork.VideoAssetsRepo
            .GetTeacherVideosPagedAsync(teacherId, request.Search, request.Page, request.PageSize);

        var items = rows.Select(r => new TeacherVideoListItemDto
        {
            Id = r.Id,
            Title = r.Title,
            SourceType = r.SourceType,
            DurationSeconds = r.DurationSeconds,
            StudentsInScope = r.StudentsInScope,
            TotalOpens = r.TotalOpens,
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

    // ══════════════════════════════════════════════════════════════════════
    // ANALYTICS (Story F)
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<VideoAnalyticsResponse>> GetAnalyticsAsync(
        long teacherId, long videoAssetId, VideoAnalyticsRequest request)
    {
        var video = await _unitOfWork.VideoAssetsRepo
            .GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
            return Result<VideoAnalyticsResponse>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        var (rows, totalCount) = await _unitOfWork.VideoAssetsRepo
            .GetAnalyticsRowsForTeacherAsync(
                teacherId, videoAssetId, request.Search,
                request.SortBy, request.SortDirection,
                request.Page, request.PageSize);

        var aggregates = await _unitOfWork.VideoAssetsRepo
            .GetAnalyticsAggregatesAsync(teacherId, videoAssetId);

        var rowDtos = rows.Select(r => new VideoAnalyticsRowDto
        {
            TeacherStudentId = r.TeacherStudentId,
            StudentName = r.StudentName,
            StudentCode = r.StudentCode,
            HasOpened = r.HasOpened,
            OpenCount = r.OpenCount,
            TotalWatchSeconds = r.TotalWatchSeconds,
            VideoDurationSeconds = r.VideoDurationSeconds,
            FirstOpenedAt = r.FirstOpenedAt,
            LastOpenedAt = r.LastOpenedAt,
            EstimatedCompletionPct = r.EstimatedCompletionPct,
            RawWatchPct = r.RawWatchPct,
        }).ToList();

        var response = new VideoAnalyticsResponse
        {
            VideoAssetId = video.Id,
            Title = video.Title,
            DurationSeconds = video.DurationSeconds,
            TotalStudentsInScope = aggregates.TotalStudentsInScope,
            TotalStudentsWatched = aggregates.TotalStudentsWatched,
            Rows = rowDtos,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
        };

        return Result<VideoAnalyticsResponse>.Success(response, _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // STUDENT — VIDEO LIST (Story B student endpoint)
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<StudentVideoListItemDto>>>>
        GetStudentVideosAsync(
            long teacherId, long teacherStudentId, StudentVideoListRequest request)
    {
        // Runtime module-active gate — students have no `module` JWT claim.
        var moduleGate = await CheckModuleActiveAsync<PaginatedResponse<List<StudentVideoListItemDto>>>(teacherId);
        if (moduleGate is not null) return moduleGate;

        var (rows, totalCount) = await _unitOfWork.VideoAssetsRepo
            .GetVisibleVideosForStudentAsync(teacherId, teacherStudentId, request.Page, request.PageSize);

        var items = rows.Select(r => new StudentVideoListItemDto
        {
            Id = r.Id,
            Title = r.Title,
            Description = r.Description,
            SourceType = r.SourceType,
            SourceUrl = r.SourceUrl,
            DurationSeconds = r.DurationSeconds,
            AssignedAt = r.AssignedAt,
            HasOpened = r.HasOpened,
            LastOpenedAt = r.LastOpenedAt,
        }).ToList();

        var response = new PaginatedResponse<List<StudentVideoListItemDto>>
        {
            data = items,
            page = request.Page,
            pageSize = request.PageSize,
            totalCount = totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
        };

        return Result<PaginatedResponse<List<StudentVideoListItemDto>>>.Success(response, _localizer);
    }

    // ══════════════════════════════════════════════════════════════════════
    // STUDENT — START WATCH (Story B steps 5–9)
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<StartWatchResponse>> StartWatchAsync(
        long teacherId, long teacherStudentId, long videoAssetId, StartWatchRequest request)
    {
        var moduleGate = await CheckModuleActiveAsync<StartWatchResponse>(teacherId);
        if (moduleGate is not null) return moduleGate;

        // Idempotency replay: if the same client event was already recorded,
        // return current state without inserting anything new.
        if (request.ClientEventId.HasValue
            && await _unitOfWork.VideoAssetsRepo.HasEventWithClientIdAsync(request.ClientEventId.Value))
        {
            return await BuildStartReplayResponseAsync(
                teacherId, teacherStudentId, videoAssetId);
        }

        var video = await _unitOfWork.VideoAssetsRepo
            .GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
            return Result<StartWatchResponse>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        bool inScope = await _unitOfWork.VideoAssetsRepo
            .IsStudentInVideoScopeAsync(teacherStudentId, videoAssetId, teacherId);
        if (!inScope)
            return Result<StartWatchResponse>.Failure(
                _localizer, VideoConstants.Messages.VideoNotInScope, HttpStatusCode.Forbidden);

        // Trust-boundary duration update. Out-of-band reports are silently
        // ignored; the bool result is for telemetry, not control flow.
        if (request.VideoDurationSeconds > 0)
        {
            await _unitOfWork.VideoAssetsRepo.TryUpdateDurationWithinToleranceAsync(
                videoAssetId,
                request.VideoDurationSeconds,
                VideoConstants.DurationToleranceFraction);

            // Re-load to capture any duration update for the response.
            video = await _unitOfWork.VideoAssetsRepo
                .GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        }

        var utcNow = DateTime.UtcNow;

        // UPSERT with bounded retry — see private helper.
        var snapshot = await UpsertAnalyticsOnOpenWithRetryAsync(
            videoAssetId, teacherStudentId, teacherId, video!.DurationSeconds, utcNow);

        // Append the Open event row.
        var openEvent = new VideoWatchEvent
        {
            VideoAssetId = videoAssetId,
            TeacherId = teacherId,
            TeacherStudentId = teacherStudentId,
            DeviceId = request.DeviceId,
            EventType = VideoEventType.Open,
            PositionSeconds = snapshot.LastResumePositionSeconds,
            DeltaSinceLastSeconds = 0,
            EventUtc = utcNow,
            ClientEventId = request.ClientEventId,
            CreateAt = utcNow,
        };
        await _unitOfWork.VideoAssetsRepo.AddWatchEventAsync(openEvent);
        await _unitOfWork.SaveChangesAsync();

        int resumeFrom = ComputeResumeFromSeconds(
            snapshot.LastResumePositionSeconds, video.DurationSeconds);

        return Result<StartWatchResponse>.Success(new StartWatchResponse
        {
            VideoAssetId = video.Id,
            EmbedUrl = _urlParser.BuildEmbedUrl(video.SourceType, video.ExternalId),
            SourceType = video.SourceType,
            ResumeFromSeconds = resumeFrom,
            DurationSeconds = video.DurationSeconds,
        }, _localizer, VideoConstants.Messages.WatchStarted);
    }

    // ══════════════════════════════════════════════════════════════════════
    // STUDENT — STOP WATCH (Story C)
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<StopWatchResponse>> StopWatchAsync(
        long teacherId, long teacherStudentId, long videoAssetId, StopWatchRequest request)
    {
        var moduleGate = await CheckModuleActiveAsync<StopWatchResponse>(teacherId);
        if (moduleGate is not null) return moduleGate;

        // Idempotency replay.
        if (request.ClientEventId.HasValue
            && await _unitOfWork.VideoAssetsRepo.HasEventWithClientIdAsync(request.ClientEventId.Value))
        {
            return await BuildStopReplayResponseAsync(videoAssetId, teacherStudentId);
        }

        var video = await _unitOfWork.VideoAssetsRepo
            .GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
            return Result<StopWatchResponse>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        bool inScope = await _unitOfWork.VideoAssetsRepo
            .IsStudentInVideoScopeAsync(teacherStudentId, videoAssetId, teacherId);
        if (!inScope)
            return Result<StopWatchResponse>.Failure(
                _localizer, VideoConstants.Messages.VideoNotInScope, HttpStatusCode.Forbidden);

        var anchorUtc = await _unitOfWork.VideoAssetsRepo
            .GetLastEventUtcForDeviceAsync(teacherStudentId, videoAssetId, request.DeviceId);
        if (anchorUtc is null)
            return Result<StopWatchResponse>.Failure(
                _localizer, VideoConstants.Messages.NoActiveSession, HttpStatusCode.Conflict);

        var utcNow = DateTime.UtcNow;

        var (acceptedDelta, deltaWasClamped) = ClampDelta(
            request.DeltaSeconds, anchorUtc.Value, utcNow, video.DurationSeconds);

        int clampedPosition = ClampPosition(request.PositionSeconds, video.DurationSeconds);

        var increment = await _unitOfWork.VideoAssetsRepo.IncrementWatchAtomicAsync(
            videoAssetId, teacherStudentId, acceptedDelta, clampedPosition, utcNow);

        if (increment is null)
        {
            // Edge case: analytics row vanished between scope check and update.
            return Result<StopWatchResponse>.Failure(
                _localizer, VideoConstants.Messages.NoActiveSession, HttpStatusCode.Conflict);
        }

        var stopEvent = new VideoWatchEvent
        {
            VideoAssetId = videoAssetId,
            TeacherId = teacherId,
            TeacherStudentId = teacherStudentId,
            DeviceId = request.DeviceId,
            EventType = VideoEventType.Stop,
            PositionSeconds = clampedPosition,
            DeltaSinceLastSeconds = (int)acceptedDelta,
            EventUtc = utcNow,
            ClientEventId = request.ClientEventId,
            CreateAt = utcNow,
        };
        await _unitOfWork.VideoAssetsRepo.AddWatchEventAsync(stopEvent);
        await _unitOfWork.SaveChangesAsync();

        return Result<StopWatchResponse>.Success(new StopWatchResponse
        {
            TotalWatchSeconds = increment.TotalWatchSeconds,
            LastResumePositionSeconds = increment.LastResumePositionSeconds,
            AcceptedDelta = (int)acceptedDelta,
            DeltaWasClamped = deltaWasClamped,
        }, _localizer, VideoConstants.Messages.WatchStopped);
    }

    // ──────────────────────────────────────────────────────────────────────
    // DEFERRED — parent endpoint (was Q7(a) in Phase 3 review)
    // ──────────────────────────────────────────────────────────────────────
    //
    // Phase 5 Q1(c): the parent endpoint is dropped from v1 pending a
    // concrete Flutter screen design. The student endpoint plus the
    // existing ParentChild → StudentTeacherLink / ParentChildTeacherLink
    // data model is sufficient to add it later without DB changes.

    // ══════════════════════════════════════════════════════════════════════
    // INTEGRATION HOOK — admin teacher-purge
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<int>> PurgeAllVideosForTeacherAsync(
        long teacherId, long actingAdminUserId)
    {
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Fetch every video the teacher owns. PageSize int.MaxValue is
            // safe: the largest single-teacher inventory in production is
            // bounded by the spec's no-cap-on-scope-but-realistic-usage.
            var (rows, _) = await _unitOfWork.VideoAssetsRepo
                .GetTeacherVideosPagedAsync(teacherId, search: null, page: 1, pageSize: int.MaxValue);

            var utcNow = DateTime.UtcNow;
            int audited = 0;

            foreach (var row in rows)
            {
                var fullVideo = await _unitOfWork.VideoAssetsRepo
                    .GetVideoWithScopesAsync(row.Id, teacherId);
                if (fullVideo is null) continue;

                var analyticsRows = await _unitOfWork.VideoAssetsRepo
                    .GetAnalyticsForAuditSnapshotAsync(row.Id);
                var aggregates = await _unitOfWork.VideoAssetsRepo
                    .GetAnalyticsAggregatesAsync(teacherId, row.Id);

                string snapshotJson = BuildAuditSnapshot(
                    fullVideo, analyticsRows, aggregates, actingAdminUserId, utcNow);

                await _unitOfWork.VideoAssetsRepo.AddAuditAsync(new VideoAssetAudit
                {
                    VideoAssetId = row.Id,
                    TeacherId = teacherId,
                    Action = VideoAuditAction.HardDelete,
                    SnapshotJson = snapshotJson,
                    SnapshotArchiveUrl = null,
                    DeletedByUserId = actingAdminUserId,
                    DeletedAt = utcNow,
                    CreateAt = utcNow,
                });
                audited++;
            }

            // Bulk DELETE — NoAction FKs remove scopes, analytics, watch events.
            await _unitOfWork.VideoAssetsRepo.DeleteAllVideosForTeacherAsync(teacherId);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<int>.Success(audited, _localizer, VideoConstants.Messages.VideoDeleted);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Runtime module-active gate. Returns a Failure result when the module
    /// is deactivated; otherwise null so the caller proceeds.
    ///
    /// The bang on <c>ModuleTeacherRepo!</c> reflects a pre-existing
    /// codebase quirk (the property is declared nullable in IUnitOfWork).
    /// Per Phase 5 Q2(a): keep the bang here rather than restructuring the
    /// shared interface in this PR.
    /// </summary>
    private async Task<Result<T>?> CheckModuleActiveAsync<T>(long teacherId)
    {
        bool active = await _unitOfWork.ModuleTeacherRepo!
            .IsModuleActiveAsync(teacherId, VideoConstants.ModuleName);
        if (active) return null;

        return Result<T>.Failure(
            _localizer, VideoConstants.Messages.ModuleDeactivated, HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Validates each input scope row's shape against the
    /// <c>CK_VideoScopes_ExactlyOneTarget</c> and
    /// <c>CK_VideoScopes_ScopeTypeMatchesFK</c> rules. Returns a localization
    /// key on failure, null on success.
    /// </summary>
    private static string? ValidateScopeShape(IEnumerable<VideoScopeInputDto> scopes)
    {
        foreach (var s in scopes)
        {
            int populated =
                (s.TeacherStudentId.HasValue ? 1 : 0)
              + (s.SessionId.HasValue ? 1 : 0)
              + (s.SessionGroupId.HasValue ? 1 : 0);

            if (populated != 1)
                return VideoConstants.Messages.ScopeShapeInvalid;

            bool typeMatches = s.ScopeType switch
            {
                VideoScopeType.IndividualStudent => s.TeacherStudentId.HasValue,
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
    /// Validates each scope target belongs to the calling teacher. Returns a
    /// localization key on failure, null on success.
    /// </summary>
    private async Task<string?> ValidateScopeOwnershipAsync(
        long teacherId, IEnumerable<VideoScopeInputDto> scopes)
    {
        foreach (var s in scopes)
        {
            long targetId = s.ScopeType switch
            {
                VideoScopeType.IndividualStudent => s.TeacherStudentId!.Value,
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

    /// <summary>
    /// Builds a <see cref="VideoScope"/> entity from an input DTO. The video
    /// argument supplies <c>TeacherId</c> for the composite-FK column.
    /// </summary>
    private static VideoScope BuildScopeEntity(
        VideoScopeInputDto input, VideoAsset video, long actingUserId, DateTime utcNow)
    {
        return new VideoScope
        {
            VideoAssetId = video.Id,
            TeacherId = video.TeacherId,
            ScopeType = input.ScopeType,
            TeacherStudentId = input.TeacherStudentId,
            SessionId = input.SessionId,
            SessionGroupId = input.SessionGroupId,
            AssignedByUserId = actingUserId,
            AssignedAt = utcNow,
            CreateAt = utcNow,
        };
    }

    /// <summary>
    /// UPSERT-with-retry for the StartWatch flow's analytics row. Returns the
    /// snapshot of the analytics state after the operation.
    ///
    /// Loop body: try increment first (existing row, the common case); on
    /// miss, try INSERT and SaveChanges. If a competing transaction won the
    /// INSERT race (SQL Server unique-violation 2601/2627), loop back and
    /// the increment branch will succeed against the row they inserted.
    /// </summary>
    private async Task<VideoAnalyticsSnapshot> UpsertAnalyticsOnOpenWithRetryAsync(
        long videoAssetId, long teacherStudentId, long teacherId,
        int videoDurationSeconds, DateTime utcNow)
    {
        for (int attempt = 0; attempt <= VideoConstants.MaxOpenRetries; attempt++)
        {
            // Try the increment branch first — common case.
            var existing = await _unitOfWork.VideoAssetsRepo
                .IncrementOpenCountIfExistsAsync(videoAssetId, teacherStudentId, utcNow);
            if (existing is not null) return existing;

            // No row yet — queue an INSERT and try to save.
            var fresh = new VideoAnalytics
            {
                VideoAssetId = videoAssetId,
                TeacherId = teacherId,
                TeacherStudentId = teacherStudentId,
                OpenCount = 1,
                TotalWatchSeconds = 0,
                FirstOpenedAt = utcNow,
                LastUpdated = utcNow,
                VideoDurationAtFirstWatch = videoDurationSeconds,
                LastResumePositionSeconds = 0,
                CreateAt = utcNow,
            };

            try
            {
                await _unitOfWork.VideoAssetsRepo.AddAnalyticsRowForFirstOpenAsync(fresh);
                await _unitOfWork.SaveChangesAsync();

                return new VideoAnalyticsSnapshot
                {
                    OpenCount = 1,
                    TotalWatchSeconds = 0,
                    LastResumePositionSeconds = 0,
                };
            }
            catch (DbUpdateException ex)
                when (IsUniqueViolation(ex) && attempt < VideoConstants.MaxOpenRetries)
            {
                // Lost the insert race. The change tracker now holds a
                // detached-state entity that EF Core won't try to save again
                // because the SaveChanges call failed atomically — but the
                // entity reference is still in the change tracker as a
                // failed Added entry. We don't need to do anything: on the
                // next iteration, IncrementOpenCountIfExistsAsync runs as a
                // raw SQL UPDATE that doesn't involve the tracker, and it
                // finds the row the competing transaction inserted.
                //
                // Loop and retry.
            }
        }

        throw new InvalidOperationException(
            $"VCM analytics UPSERT exhausted retries for video {videoAssetId} student {teacherStudentId}.");
    }

    /// <summary>
    /// Detects SQL Server unique-key violation (errors 2601/2627) inside
    /// <see cref="DbUpdateException"/>. Mirrors the pattern in
    /// <c>SubscriptionService.IsUniqueViolation</c>.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var sqlException = ex.InnerException as Microsoft.Data.SqlClient.SqlException
                           ?? ex.GetBaseException() as Microsoft.Data.SqlClient.SqlException;

        return sqlException is { Number: 2601 or 2627 };
    }

    /// <summary>
    /// Two-layer clamp on the client-supplied delta:
    /// <list type="number">
    ///   <item>Clamp to <c>serverElapsed + tolerance</c>.</item>
    ///   <item>Then clamp to <c>DurationSeconds</c>.</item>
    /// </list>
    /// </summary>
    private static (long acceptedDelta, bool wasClamped) ClampDelta(
        int reportedDelta, DateTime anchorUtc, DateTime utcNow, int durationSeconds)
    {
        bool clamped = false;

        long working = Math.Max(0, reportedDelta);

        long serverElapsed = (long)Math.Ceiling((utcNow - anchorUtc).TotalSeconds);
        long elapsedCap = serverElapsed + VideoConstants.DeltaToleranceSeconds;
        if (working > elapsedCap)
        {
            working = elapsedCap;
            clamped = true;
        }

        if (durationSeconds > 0 && working > durationSeconds)
        {
            working = durationSeconds;
            clamped = true;
        }

        return (working, clamped);
    }

    /// <summary>Bounds the client-reported position to <c>[0, DurationSeconds]</c>.</summary>
    private static int ClampPosition(int reportedPosition, int durationSeconds)
    {
        if (reportedPosition < 0) return 0;
        if (durationSeconds > 0 && reportedPosition > durationSeconds) return durationSeconds;
        return reportedPosition;
    }

    /// <summary>
    /// Computes the resume seek position. Capped at
    /// <c>DurationSeconds - ResumeEndOfVideoBufferSeconds</c> so the player
    /// never resumes within the last 5 seconds (avoids "instant complete"
    /// glitches).
    /// </summary>
    private static int ComputeResumeFromSeconds(int lastPosition, int durationSeconds)
    {
        if (durationSeconds <= 0) return 0;
        int upperBound = Math.Max(0, durationSeconds - VideoConstants.ResumeEndOfVideoBufferSeconds);
        return Math.Clamp(lastPosition, 0, upperBound);
    }

    /// <summary>
    /// Idempotency replay branch for Start: returns current state without
    /// inserting anything new. Pure read — uses
    /// <see cref="IVideoAssetRepo.GetAnalyticsSnapshotAsync"/> to avoid
    /// mutating <c>LastUpdated</c>.
    /// </summary>
    private async Task<Result<StartWatchResponse>> BuildStartReplayResponseAsync(
        long teacherId, long teacherStudentId, long videoAssetId)
    {
        var video = await _unitOfWork.VideoAssetsRepo
            .GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
            return Result<StartWatchResponse>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        var snapshot = await _unitOfWork.VideoAssetsRepo
            .GetAnalyticsSnapshotAsync(videoAssetId, teacherStudentId);
        int lastResume = snapshot?.LastResumePositionSeconds ?? 0;

        return Result<StartWatchResponse>.Success(new StartWatchResponse
        {
            VideoAssetId = video.Id,
            EmbedUrl = _urlParser.BuildEmbedUrl(video.SourceType, video.ExternalId),
            SourceType = video.SourceType,
            ResumeFromSeconds = ComputeResumeFromSeconds(lastResume, video.DurationSeconds),
            DurationSeconds = video.DurationSeconds,
        }, _localizer, VideoConstants.Messages.WatchStarted);
    }

    /// <summary>
    /// Idempotency replay branch for Stop: returns current totals without
    /// re-incrementing. Pure read — same rationale as
    /// <see cref="BuildStartReplayResponseAsync"/>.
    /// </summary>
    private async Task<Result<StopWatchResponse>> BuildStopReplayResponseAsync(
        long videoAssetId, long teacherStudentId)
    {
        var snapshot = await _unitOfWork.VideoAssetsRepo
            .GetAnalyticsSnapshotAsync(videoAssetId, teacherStudentId);
        if (snapshot is null)
            return Result<StopWatchResponse>.Failure(
                _localizer, VideoConstants.Messages.NoActiveSession, HttpStatusCode.Conflict);

        return Result<StopWatchResponse>.Success(new StopWatchResponse
        {
            TotalWatchSeconds = snapshot.TotalWatchSeconds,
            LastResumePositionSeconds = snapshot.LastResumePositionSeconds,
            AcceptedDelta = 0,
            DeltaWasClamped = false,
        }, _localizer, VideoConstants.Messages.WatchStopped);
    }

    /// <summary>
    /// Serializes the audit snapshot per spec §2.5.1.
    /// </summary>
    private static string BuildAuditSnapshot(
        VideoAsset video,
        IReadOnlyList<VideoAnalyticsAuditRow> analytics,
        VideoAnalyticsAggregates aggregates,
        long deletedByUserId,
        DateTime deletedAt)
    {
        var payload = new
        {
            videoAsset = new
            {
                id = video.Id,
                teacherId = video.TeacherId,
                title = video.Title,
                description = video.Description,
                sourceUrl = video.SourceUrl,
                sourceType = video.SourceType.ToString(),
                externalId = video.ExternalId,
                durationSeconds = video.DurationSeconds,
                createdAt = video.CreateAt,
                createdByUserId = video.CreatedByUserId,
            },
            scopes = video.Scopes.Select(s => new
            {
                id = s.Id,
                scopeType = s.ScopeType.ToString(),
                teacherStudentId = s.TeacherStudentId,
                sessionId = s.SessionId,
                sessionGroupId = s.SessionGroupId,
                assignedByUserId = s.AssignedByUserId,
                assignedAt = s.AssignedAt,
            }),
            analytics = analytics.Select(a => new
            {
                teacherStudentId = a.TeacherStudentId,
                studentName = a.StudentName,
                studentCode = a.StudentCode,
                openCount = a.OpenCount,
                totalWatchSeconds = a.TotalWatchSeconds,
                firstOpenedAt = a.FirstOpenedAt,
                lastUpdated = a.LastUpdated,
                lastResumePositionSeconds = a.LastResumePositionSeconds,
            }),
            aggregates = new
            {
                totalStudentsInScope = aggregates.TotalStudentsInScope,
                totalStudentsWatched = aggregates.TotalStudentsWatched,
                totalWatchSecondsAcrossAll = analytics.Sum(a => a.TotalWatchSeconds),
                avgCompletionPctAtDelete = ComputeAvgCompletion(analytics, video.DurationSeconds),
            },
            deletedByUserId,
            deletedAt,
        };

        return JsonSerializer.Serialize(payload);
    }

    private static int ComputeAvgCompletion(
        IReadOnlyList<VideoAnalyticsAuditRow> analytics, int durationSeconds)
    {
        if (durationSeconds <= 0 || analytics.Count == 0) return 0;
        long totalSeconds = analytics.Sum(a => Math.Min(a.TotalWatchSeconds, durationSeconds));
        return (int)(totalSeconds * 100 / (analytics.Count * (long)durationSeconds));
    }

  
}
