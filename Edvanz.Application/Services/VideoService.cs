using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.VideoContentManagement;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Net;
using System.Text.Json;

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
    private readonly ISubscriptionGateService _subscriptionGate;
    private readonly IFileStorageService _fileStorage;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public VideoService(
        IUnitOfWork unitOfWork,
        IVideoScopeResolver scopeResolver,
        IVideoUrlParser urlParser,
        ISubscriptionGateService subscriptionGate,
        IFileStorageService fileStorage,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _scopeResolver = scopeResolver;
        _urlParser = urlParser;
        _subscriptionGate = subscriptionGate;
        _fileStorage = fileStorage;
        _localizer = localizer;
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

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        return await ReplaceScopesInternalAsync(
            teacherId, actingUserId, videoAssetId, request.Scopes, ownsTransaction);
    }

    /// <summary>
    /// Transactional body of scope replacement: video lookup → shape
    /// validation → ownership validation → delete-all → insert-new →
    /// resolve StudentsInScope. Extracted from <see cref="ReplaceScopesAsync"/>
    /// so Phase 4's extended <c>UpdateVideoAsync</c> can call it inside its
    /// own already-open transaction (<paramref name="ownsTransaction"/> =
    /// <c>false</c>) when the update request carries a non-null
    /// <c>Scopes</c> list — one code path for both the dedicated
    /// PUT-scopes endpoint and a folded-in scope replace during full update.
    ///
    /// Every validation/error path is unchanged from the pre-refactor
    /// inline version (<c>ScopeCannotBeEmpty</c> stays in the public
    /// wrapper since it's a request-shape check independent of any video;
    /// everything from video lookup onward — including
    /// <c>ScopeShapeInvalid</c> and <c>ScopeTargetNotFoundOrForeign</c> —
    /// lives here so it re-runs correctly under Phase 4's shared
    /// transaction). <c>VideoAnalytics</c> remains deliberately untouched.
    /// </summary>
    private async Task<Result<ReplaceScopesResponse>> ReplaceScopesInternalAsync(
        long teacherId, long actingUserId, long videoAssetId,
        List<VideoScopeInputDto> scopes, bool ownsTransaction)
    {
        var video = await _unitOfWork.VideoAssetsRepo
            .GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            return Result<ReplaceScopesResponse>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);
        }

        var shapeError = ValidateScopeShape(scopes);
        if (shapeError is not null)
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            return Result<ReplaceScopesResponse>.Failure(
                _localizer, shapeError, HttpStatusCode.BadRequest);
        }

        var ownershipError = await ValidateScopeOwnershipAsync(teacherId, scopes);
        if (ownershipError is not null)
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            return Result<ReplaceScopesResponse>.Failure(
                _localizer, ownershipError, HttpStatusCode.BadRequest);
        }

        int oldCount = await _unitOfWork.VideoAssetsRepo.CountScopesForVideoAsync(videoAssetId);

        try
        {
            await _unitOfWork.VideoAssetsRepo.DeleteAllScopesForVideoAsync(videoAssetId);

            var utcNow = DateTime.UtcNow;
            var newRows = scopes
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
    // SET VIDEO STATUS (Track D1 — Settings-row quick toggle)
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<bool>> SetVideoStatusAsync(
        long teacherId, long videoAssetId, SetVideoStatusRequest request)
    {
        bool updated = await _unitOfWork.VideoAssetsRepo.SetVideoStatusAsync(
            videoAssetId, teacherId, request.Status, request.PublishDate);

        if (!updated)
            return Result<bool>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        return Result<bool>.Success(true, _localizer, VideoConstants.Messages.VideoStatusUpdated);
    }

    /// <inheritdoc />
    public async Task<Result<VideoStatus>> ToggleVideoStatusAsync(long teacherId, long videoAssetId)
    {
        // Reads current status, flips it, and reuses the exact same
        // ExecuteUpdateAsync repo method SetVideoStatusAsync already backs —
        // no new SQL, no duplicated concurrency handling. PublishDate is
        // reset to null on toggle: an auto-flip has no meaningful "scheduled
        // for X" concept the way an explicit PATCH does.
        var video = await _unitOfWork.VideoAssetsRepo
            .GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
            return Result<VideoStatus>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        var newStatus = video.Status == VideoStatus.Published
            ? VideoStatus.Draft
            : VideoStatus.Published;

        bool updated = await _unitOfWork.VideoAssetsRepo
            .SetVideoStatusAsync(videoAssetId, teacherId, newStatus, publishDate: null);

        if (!updated)
            return Result<VideoStatus>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        return Result<VideoStatus>.Success(newStatus, _localizer, VideoConstants.Messages.VideoStatusUpdated);
    }
    public async Task<Result<VideoDetailDto>> UpdateVideoAsync(
       long teacherId, long actingUserId, long videoAssetId, UpdateVideoRequest request,
       IFormFile? attachment)
    {
        // Step 1 — fetch the tracked entity (unchanged).
        var video = await _unitOfWork.VideoAssetsRepo
            .GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
            return Result<VideoDetailDto>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        string title = request.Title?.Trim() ?? string.Empty;
        if (title.Length == 0)
            return Result<VideoDetailDto>.Failure(
                _localizer, VideoConstants.Messages.InvalidUrl, HttpStatusCode.BadRequest);

        // Step 2 — in-application concurrency check, before any mutation.
        if (!request.RowVersion.AsSpan().SequenceEqual(video.RowVersion))
            return Result<VideoDetailDto>.Failure(
                _localizer, VideoConstants.Messages.ConcurrencyConflict, HttpStatusCode.Conflict);

        // Consolidated-edit additions: attachment/exam shape validated here,
        // fail-fast, before any write — same philosophy as everything above.
        if (attachment is not null)
        {
            string? attachmentShapeError = ValidateUploadedFile(
                attachment, VideoConstants.AllowedAttachmentContentTypes, VideoConstants.AttachmentMaxSizeBytes,
                VideoConstants.Messages.AttachmentInvalidType, VideoConstants.Messages.AttachmentTooLarge);
            if (attachmentShapeError is not null)
                return Result<VideoDetailDto>.Failure(
                    _localizer, attachmentShapeError, HttpStatusCode.UnprocessableEntity);
        }

        if (request.Exam is not null)
        {
            var examShapeError = ValidateExamShape(request.Exam);
            if (examShapeError is not null)
                return Result<VideoDetailDto>.Failure(_localizer, examShapeError, HttpStatusCode.BadRequest);
        }
        // Step 3 — pre-update audit snapshot, built from data as it stood
        // BEFORE any mutation below. GetVideoByIdAndTeacherAsync doesn't
        // load Scopes (it's the tracked-for-mutation fetch), so scopes are
        // read separately via the same AsNoTracking method DeleteVideoAsync
        // uses for its own snapshot.
        var snapshotSource = await _unitOfWork.VideoAssetsRepo
            .GetVideoWithScopesAsync(videoAssetId, teacherId);
        var linkedUnitIdsBeforeUpdate = await _unitOfWork.VideoAssetsRepo
            .GetLinkedUnitIdsAsync(videoAssetId);
        var analyticsRows = await _unitOfWork.VideoAssetsRepo
            .GetAnalyticsForAuditSnapshotAsync(videoAssetId);
        var aggregates = await _unitOfWork.VideoAssetsRepo
            .GetAnalyticsAggregatesAsync(teacherId, videoAssetId);

        var utcNow = DateTime.UtcNow;
        string snapshotJson = BuildAuditSnapshot(
            snapshotSource!, analyticsRows, aggregates, actingUserId, utcNow,
            VideoAuditAction.Updated, linkedUnitIdsBeforeUpdate);

        // Step 4 — begin transaction (reentrant, same pattern as elsewhere).
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _unitOfWork.VideoAssetsRepo.AddAuditAsync(new VideoAssetAudit
            {
                VideoAssetId = video.Id,
                TeacherId = teacherId,
                Action = VideoAuditAction.Updated,
                SnapshotJson = snapshotJson,
                SnapshotArchiveUrl = null,
                DeletedByUserId = actingUserId,
                DeletedAt = utcNow,
                CreateAt = utcNow,
            });

            // Step 5 — title/description (unchanged existing logic).
            video.Title = title;
            video.Description = request.Description?.Trim();

            // Step 6 — sourceUrl change detection + analytics reset
            // (UNCHANGED existing rule — do not alter).
            string newSourceUrl = request.SourceUrl.Trim();
            bool sourceUrlChanged = !string.Equals(
                newSourceUrl, video.SourceUrl, StringComparison.Ordinal);

            if (sourceUrlChanged)
            {
                var parseOutcome = _urlParser.Parse(newSourceUrl);
                if (!parseOutcome.IsSuccess)
                {
                    string urlErrorKey = parseOutcome.Failure switch
                    {
                        VideoUrlParseFailure.UnsupportedSource => VideoConstants.Messages.UnsupportedSource,
                        _ => VideoConstants.Messages.InvalidUrl,
                    };
                    if (ownsTransaction)
                        await _unitOfWork.RollbackAsync();
                    return Result<VideoDetailDto>.Failure(_localizer, urlErrorKey, HttpStatusCode.BadRequest);
                }

                video.SourceUrl = newSourceUrl;
                video.SourceType = parseOutcome.Success!.SourceType;
                video.ExternalId = parseOutcome.Success.ExternalId;

                // Confirmed rule: a changed URL is a different video — watch
                // history resets and duration is re-learned from scratch.
                await _unitOfWork.VideoAssetsRepo.ResetAnalyticsForVideoAsync(videoAssetId);
                video.DurationSeconds = 0;
                video.IsDurationManuallySet = false;
            }

            // Step 7 — publishDate/status (unchanged existing logic).
            video.PublishDate = request.PublishDate;
            if (request.Status.HasValue)
                video.Status = request.Status.Value;

            // Step 8 — new: explicit duration override wins over the
            // sourceUrl-change reset above (applied after it).
            if (request.DurationSeconds.HasValue)
            {
                video.DurationSeconds = request.DurationSeconds.Value;
                video.IsDurationManuallySet = true;
            }

            // Step 9 — new: unit links. null = untouched; empty list = unlink all.
            if (request.UnitIds is not null)
            {
                var unitOwnershipError = await ValidateUnitIdsOwnershipAsync(teacherId, request.UnitIds);
                if (unitOwnershipError is not null)
                {
                    if (ownsTransaction)
                        await _unitOfWork.RollbackAsync();
                    return Result<VideoDetailDto>.Failure(
                        _localizer, unitOwnershipError, HttpStatusCode.BadRequest);
                }

                await _unitOfWork.VideoAssetsRepo.ReplaceUnitLinksAsync(videoAssetId, request.UnitIds);
            }

            // Step 10 — new: optional scope replacement, folded into this
            // transaction via the Phase 2 shared helper.
            if (request.Scopes is not null)
            {
                var scopesResult = await ReplaceScopesInternalAsync(
                    teacherId, actingUserId, videoAssetId, request.Scopes, ownsTransaction: false);

                if (!scopesResult.IsSuccess)
                {
                    if (ownsTransaction)
                        await _unitOfWork.RollbackAsync();
                    // Message is already localized by ReplaceScopesInternalAsync —
                    // propagate as-is rather than re-keying through the localizer
                    // (same convention as ExamHomeworkService.AddStudentsAsync).

                    return Result<VideoDetailDto>.Failure(
                        scopesResult.Message ?? string.Empty, scopesResult.StatusCode);
                }
            }

            // Step 10b — new: exam replace-all. null = untouched. Delete
            // (ExecuteDeleteAsync, cascades to questions/options via FK) then
            // insert fresh, same replace-not-diff approach Scopes uses.
            if (request.Exam is not null)
            {
                await _unitOfWork.VideoAssetsRepo.DeleteExamForVideoAsync(videoAssetId);
                var newExam = BuildExamEntity(request.Exam, videoAssetId, teacherId, actingUserId, utcNow);
                await _unitOfWork.VideoAssetsRepo.AddExamAsync(newExam);
            }

            // Step 11 — stamp UpdatedAt.
            video.UpdatedAt = utcNow;

            // Step 12 — SaveChangesAsync is also where EF Core's RowVersion
            // token gets checked at the SQL level (WHERE RowVersion =
            // @original) — second-layer safety net on top of Step 2's
            // in-application check, covering the race where two edits pass
            // Step 2 concurrently.
            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (ownsTransaction)
                    await _unitOfWork.RollbackAsync();
                return Result<VideoDetailDto>.Failure(
                    _localizer, VideoConstants.Messages.ConcurrencyConflict, HttpStatusCode.Conflict);
            }

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
        // unit links, scopes, exam) is already durably committed — blob
        // storage structurally cannot join that transaction. A failure here
        // does not roll any of that back; see Warning handling below.
        string? warning = null;
        if (attachment is not null || request.RemoveAttachment)
        {
            try
            {
                var existing = (await _unitOfWork.VideoAssetsRepo
                    .GetAttachmentsForVideoAsync(videoAssetId)).FirstOrDefault();

                if (attachment is not null)
                {
                    string newBlobPath = await UploadAttachmentBlobAsync(teacherId, videoAssetId, attachment);

                    var newAttachment = new VideoAttachment
                    {
                        VideoAssetId = videoAssetId,
                        TeacherId = teacherId,
                        FileName = attachment.FileName,
                        ContentType = attachment.ContentType,
                        FileSizeBytes = attachment.Length,
                        BlobPath = newBlobPath,
                        UploadedByUserId = actingUserId,
                        CreateAt = DateTime.UtcNow,
                    };

                    await _unitOfWork.VideoAssetsRepo.AddAttachmentAsync(newAttachment);
                    await _unitOfWork.SaveChangesAsync();

                    if (existing is not null)
                    {
                        await _unitOfWork.VideoAssetsRepo.DeleteAttachmentAsync(existing);
                        await _unitOfWork.SaveChangesAsync();
                        try { await _fileStorage.DeleteAsync(existing.BlobPath); }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine(
                                $"[VideoService] UpdateVideoAsync: failed to delete old attachment blob " +
                                $"'{existing.BlobPath}' for videoAssetId={videoAssetId} — orphaned, non-blocking. " +
                                $"Reason: {ex.Message}");
                        }
                    }
                }
                else if (request.RemoveAttachment && existing is not null)
                {
                    await _unitOfWork.VideoAssetsRepo.DeleteAttachmentAsync(existing);
                    await _unitOfWork.SaveChangesAsync();
                    try { await _fileStorage.DeleteAsync(existing.BlobPath); }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(
                            $"[VideoService] UpdateVideoAsync: failed to delete removed attachment blob " +
                            $"'{existing.BlobPath}' for videoAssetId={videoAssetId} — orphaned, non-blocking. " +
                            $"Reason: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[VideoService] UpdateVideoAsync: attachment update failed for videoAssetId={videoAssetId} " +
                    $"AFTER the video update already committed. Reason: {ex.Message}");
                warning = _localizer[VideoConstants.Messages.AttachmentUpdateFailedVideoSaved];
            }
        }

        // Resolve final attachment state for the response regardless of
        // whether this call changed it — GET-like symmetry, one extra query.
        var currentAttachment = (await _unitOfWork.VideoAssetsRepo
            .GetAttachmentsForVideoAsync(videoAssetId)).FirstOrDefault();

        var responseDto = MapToDetailDto(video);
        responseDto.Warning = warning;
        responseDto.Attachment = currentAttachment is null
            ? null
            : new VideoAttachmentDto
            {
                Id = currentAttachment.Id,
                FileName = currentAttachment.FileName,
                ContentType = currentAttachment.ContentType,
                FileSizeBytes = currentAttachment.FileSizeBytes,
                ReadUrl = await _fileStorage.GetReadUrlAsync(currentAttachment.BlobPath),
                CreatedAt = currentAttachment.CreateAt,
            };

        return Result<VideoDetailDto>.Success(
            responseDto, _localizer, VideoConstants.Messages.VideoUpdated);
    }

    // ══════════════════════════════════════════════════════════════════════
    // GET VIDEO DETAIL (G-EDIT supporting read)
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<VideoDetailDto>> GetVideoDetailAsync(long teacherId, long videoAssetId)
    {
        var video = await _unitOfWork.VideoAssetsRepo
            .GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
            return Result<VideoDetailDto>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        var dto = await MapVideoBaseAsync<VideoDetailDto>(video);

        var withScopes = await _unitOfWork.VideoAssetsRepo
            .GetVideoWithScopesAsync(videoAssetId, teacherId);
        if (withScopes is not null && withScopes.Scopes.Count > 0)
        {
            dto.Scopes = withScopes.Scopes
                .GroupBy(s => s.ScopeType)
                .Select(g => new CreateVideoScopeDto
                {
                    ScopeType = g.Key,
                    Ids = g.Key == VideoScopeType.Session
                        ? g.Select(s => s.SessionId!.Value).ToList()
                        : g.Select(s => s.SessionGroupId!.Value).ToList(),
                })
                .ToList();
        }

        var exam = await _unitOfWork.VideoAssetsRepo.GetExamWithQuestionsAsync(videoAssetId, teacherId);
        if (exam is not null)
        {
            dto.Exam = new CreateExamDto
            {
                Title = exam.Title,
                Description = exam.Description,
                Questions = exam.Questions.Select(q => new CreateExamQuestionDto
                {
                    Text = q.Text,
                    QuestionType = q.QuestionType,
                    Options = q.Options.Select(o => new CreateExamQuestionOptionDto
                    {
                        Text = o.Text,
                        IsCorrect = o.IsCorrect,
                    }).ToList(),
                }).ToList(),
            };
        }

        return Result<VideoDetailDto>.Success(dto, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<VideoOverviewDto>> GetVideoOverviewAsync(long teacherId, long videoAssetId)
    {
        var video = await _unitOfWork.VideoAssetsRepo
            .GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
            return Result<VideoOverviewDto>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        var dto = await MapVideoBaseAsync<VideoOverviewDto>(video);

        var aggregates = await _unitOfWork.VideoAssetsRepo
            .GetAnalyticsAggregatesAsync(teacherId, videoAssetId);
        dto.SeenStudentCount = aggregates.TotalStudentsWatched;
        dto.UnseenStudentCount = aggregates.UnseenCount;
        dto.CompletedStudentCount = aggregates.CompletedCount;

        return Result<VideoOverviewDto>.Success(dto, _localizer);
    }

    /// <summary>
    /// Shared base mapper for <see cref="VideoDetailDto"/> and
    /// <see cref="VideoOverviewDto"/> — both need the same Id/Title/.../
    /// RowVersion/Attachment fields; this is the single place that builds
    /// them, so a future field addition to the base shape only needs one
    /// edit. Fetches the current attachment itself (one extra query) rather
    /// than requiring every caller to pass one in.
    /// </summary>
    private async Task<TDto> MapVideoBaseAsync<TDto>(VideoAsset video) where TDto : VideoBaseDto, new()
    {
        var attachment = (await _unitOfWork.VideoAssetsRepo
            .GetAttachmentsForVideoAsync(video.Id, video.TeacherId)).FirstOrDefault();

        return new TDto
        {
            Id = video.Id,
            Title = video.Title,
            Description = video.Description,
            SourceType = video.SourceType,
            SourceUrl = video.SourceUrl,
            DurationSeconds = video.DurationSeconds,
            PublishDate = video.PublishDate,
            Status = video.Status,
            RowVersion = video.RowVersion,
            Attachment = attachment is null ? null : new VideoAttachmentDto
            {
                Id = attachment.Id,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                FileSizeBytes = attachment.FileSizeBytes,
                ReadUrl = await _fileStorage.GetReadUrlAsync(attachment.BlobPath),
                CreatedAt = attachment.CreateAt,
            },
        };
    }

    private static VideoDetailDto MapToDetailDto(VideoAsset video) => new()
    {
        Id = video.Id,
        Title = video.Title,
        Description = video.Description,
        SourceType = video.SourceType,
        SourceUrl = video.SourceUrl,
        DurationSeconds = video.DurationSeconds,
        PublishDate = video.PublishDate,
        Status = video.Status,
        RowVersion = video.RowVersion,
    };

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
            Description = r.Description,
            SourceUrl = r.SourceUrl,
            SourceType = r.SourceType,
            DurationSeconds = r.DurationSeconds,
            StudentsInScope = r.StudentsInScope,
            TotalOpens = r.TotalOpens,
            SeenStudentCount = r.SeenStudentCount,
            UnseenStudentCount = r.UnseenStudentCount,
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
                request.SortBy, request.SortDirection, request.StatusFilter,
                request.Page, request.PageSize);

        var aggregates = await _unitOfWork.VideoAssetsRepo
            .GetAnalyticsAggregatesAsync(teacherId, videoAssetId);

        var rowDtos = rows.Select(r => new VideoAnalyticsRowDto
        {
            TeacherStudentId = r.TeacherStudentId,
            StudentName = r.StudentName,
            StudentCode = r.StudentCode,
            SessionName = r.SessionName,
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
            UnseenCount = aggregates.UnseenCount,
            CompletedCount = aggregates.CompletedCount,
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
              + (s.SessionId.HasValue ? 1 : 0)
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
    /// Shared audit-snapshot serializer for both delete-time (Story E) and
    /// pre-update (Phase 4 / G-EDIT) audit rows. <paramref name="action"/> and
    /// <paramref name="linkedUnitIds"/> default so the two pre-existing delete
    /// call sites need no changes; <c>UpdateVideoAsync</c> passes both
    /// explicitly.
    ///
    /// NOTE: the payload field names <c>deletedByUserId</c>/<c>deletedAt</c>
    /// are historical — for an "Updated" snapshot they mean "actor/timestamp
    /// of this audit event," not literal deletion. The sibling <c>action</c>
    /// field disambiguates. Not renaming the JSON shape here to avoid
    /// changing the read contract for existing delete-snapshot rows.
    /// </summary>
    private static string BuildAuditSnapshot(
        VideoAsset video,
        IReadOnlyList<VideoAnalyticsAuditRow> analytics,
        VideoAnalyticsAggregates aggregates,
        long deletedByUserId,
        DateTime deletedAt,
        string action = VideoAuditAction.HardDelete,
        IReadOnlyList<long>? linkedUnitIds = null)
    {
        var payload = new
        {
            action,
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
            unitIds = linkedUnitIds ?? Array.Empty<long>(),
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

    /// <inheritdoc />
    public async Task<Result<VideoAttachmentDto>> UploadAttachmentAsync(
        long teacherId, long actingUserId, long videoAssetId,
        string fileName, string contentType, long fileSizeBytes, Stream content)
    {
        // Phase 5 Step 1 — PDF-only, checked before any I/O. Same 422 pattern
        // Phase 3 already uses for create-time attachment validation.
        if (!VideoConstants.AllowedAttachmentContentTypes.Contains(
                contentType, StringComparer.OrdinalIgnoreCase))
            return Result<VideoAttachmentDto>.Failure(
                _localizer, VideoConstants.Messages.AttachmentInvalidType, HttpStatusCode.UnprocessableEntity);

        if (fileSizeBytes > VideoConstants.AttachmentMaxSizeBytes)
            return Result<VideoAttachmentDto>.Failure(
                _localizer, VideoConstants.Messages.AttachmentTooLarge, HttpStatusCode.UnprocessableEntity);

        var video = await _unitOfWork.VideoAssetsRepo.GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
            return Result<VideoAttachmentDto>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        // Exact convention documented on VideoAttachment.BlobPath — same one
        // Phase 3's CreateVideoAsync saga already uses for create-time uploads.
        string blobPath = $"{teacherId}/{videoAssetId}/{Guid.NewGuid()}-{fileName}";
        string uploadedBlobPath = await _fileStorage.UploadAsync(blobPath, content, contentType);

        var attachment = new VideoAttachment
        {
            VideoAssetId = videoAssetId,
            TeacherId = teacherId,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            BlobPath = uploadedBlobPath,
            UploadedByUserId = actingUserId,
            CreateAt = DateTime.UtcNow,
        };

        await _unitOfWork.VideoAssetsRepo.AddAttachmentAsync(attachment);

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Blob uploaded but the DB row never landed — compensate so we
            // don't orphan a blob nothing references. Same saga philosophy
            // as Phase 3's CreateVideoAsync.
            Console.Error.WriteLine(
                $"[VideoService] UploadAttachmentAsync: DB insert failed after blob upload for " +
                $"videoAssetId={videoAssetId}. Deleting orphaned blob '{uploadedBlobPath}'. " +
                $"Reason: {ex.Message}");
            try { await _fileStorage.DeleteAsync(uploadedBlobPath); }
            catch (Exception cleanupEx)
            {
                Console.Error.WriteLine(
                    $"[VideoService] UploadAttachmentAsync: failed to delete orphaned blob " +
                    $"'{uploadedBlobPath}': {cleanupEx.Message}");
            }
            return Result<VideoAttachmentDto>.Failure(
                _localizer, "ServerError", HttpStatusCode.InternalServerError);
        }

        return Result<VideoAttachmentDto>.Success(
            new VideoAttachmentDto
            {
                Id = attachment.Id,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                FileSizeBytes = attachment.FileSizeBytes,
                ReadUrl = await _fileStorage.GetReadUrlAsync(uploadedBlobPath),
                CreatedAt = attachment.CreateAt,
            },
            _localizer,
            VideoConstants.Messages.AttachmentUploaded,
            HttpStatusCode.Created);
    }

    public Task<Result<List<VideoAttachmentDto>>> GetAttachmentsAsync(long teacherId, long videoAssetId)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<Result<ThumbnailDto>> ReplaceThumbnailAsync(
        long teacherId, long videoAssetId, string fileName, string contentType,
        long fileSizeBytes, Stream content)
    {
        // Step 1 — validate before any I/O.
        if (!VideoConstants.AllowedThumbnailContentTypes.Contains(
                contentType, StringComparer.OrdinalIgnoreCase))
            return Result<ThumbnailDto>.Failure(
                _localizer, VideoConstants.Messages.ThumbnailInvalidType, HttpStatusCode.UnprocessableEntity);

        if (fileSizeBytes > VideoConstants.ThumbnailMaxSizeBytes)
            return Result<ThumbnailDto>.Failure(
                _localizer, VideoConstants.Messages.ThumbnailTooLarge, HttpStatusCode.UnprocessableEntity);

        // Step 2 — fetch, tracked (so setting ThumbnailBlobPath below and
        // calling SaveChangesAsync is a normal EF Core update).
        var video = await _unitOfWork.VideoAssetsRepo.GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
            return Result<ThumbnailDto>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        // Step 3 — capture the old path BEFORE any mutation. Null on a
        // video's first-ever thumbnail — nothing to delete in step 6.
        string? oldBlobPath = video.ThumbnailBlobPath;

        // Step 4 — upload the new blob FIRST. Confirmed ordering: a replace
        // must never lose the old, live asset if the new upload or the DB
        // write fails.
        string ext = Path.GetExtension(fileName);
        string newBlobPath = $"{teacherId}/{videoAssetId}/thumbnail-{Guid.NewGuid()}{ext}";
        string uploadedBlobPath = await _fileStorage.UploadAsync(newBlobPath, content, contentType);

        // Step 5 — point the DB at the new blob. On failure, compensate by
        // deleting the just-uploaded blob; the old thumbnail (still the
        // value referenced by the DB row up to this point) is untouched.
        video.ThumbnailBlobPath = uploadedBlobPath;
        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[VideoService] ReplaceThumbnailAsync: DB update failed after blob upload for " +
                $"videoAssetId={videoAssetId}. Deleting orphaned new blob '{uploadedBlobPath}'. " +
                $"Reason: {ex.Message}");
            try { await _fileStorage.DeleteAsync(uploadedBlobPath); }
            catch (Exception cleanupEx)
            {
                Console.Error.WriteLine(
                    $"[VideoService] ReplaceThumbnailAsync: failed to delete orphaned new blob " +
                    $"'{uploadedBlobPath}' during compensation: {cleanupEx.Message}");
            }
            return Result<ThumbnailDto>.Failure(
                _localizer, "ServerError", HttpStatusCode.InternalServerError);
        }

        // Step 6 — ONLY after the DB write succeeds, best-effort delete the
        // old blob. Non-blocking: an orphaned old blob is cheap, non-user-
        // facing cleanup debt; failing the whole request here would be worse.
        if (oldBlobPath is not null)
        {
            try
            {
                await _fileStorage.DeleteAsync(oldBlobPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[VideoService] ReplaceThumbnailAsync: failed to delete old thumbnail blob " +
                    $"'{oldBlobPath}' for videoAssetId={videoAssetId} — orphaned, non-blocking. " +
                    $"Reason: {ex.Message}");
            }
        }

        // Step 7 — fresh SAS URL for the new blob.
        string readUrl = await _fileStorage.GetReadUrlAsync(uploadedBlobPath);

        return Result<ThumbnailDto>.Success(
            new ThumbnailDto { ReadUrl = readUrl },
            _localizer,
            VideoConstants.Messages.ThumbnailReplaced,
            HttpStatusCode.OK);
    }

    public Task<Result<bool>> DeleteAttachmentAsync(long teacherId, long videoAssetId, long attachmentId)
    {
        throw new NotImplementedException();
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

        return Result<VideoUnitResponse>.Success(response, _localizer, "Success",HttpStatusCode.OK);
    }

    
    /// <inheritdoc />
    public async Task<Result<CreateVideoResponse>> CreateVideoAsync(
        long teacherId, long actingUserId, CreateVideoRequest request,
        IFormFile? thumbnail, IFormFile? attachment)
    {
        // Free-tier quota: videos (see ModuleQuota table).
        if (!await _subscriptionGate.CanCreateAsync(
                teacherId, ModuleQuotaKeys.Videos,
                () => _unitOfWork.VideoAssetsRepo.CountByTeacherAsync(teacherId)))
            return Result<CreateVideoResponse>.Failure(
                _localizer, SubscriptionConstants.Messages.SubscriptionRequired, HttpStatusCode.Forbidden);

        string title = request.Title?.Trim() ?? string.Empty;
        if (title.Length == 0)
            return Result<CreateVideoResponse>.Failure(
                _localizer, VideoConstants.Messages.InvalidUrl, HttpStatusCode.BadRequest);

        // Parse URL — distinguishes "not a URL" from "unsupported provider".
        var parseOutcome = _urlParser.Parse(request.SourceUrl);
        if (!parseOutcome.IsSuccess)
        {
            string urlErrorKey = parseOutcome.Failure switch
            {
                VideoUrlParseFailure.UnsupportedSource => VideoConstants.Messages.UnsupportedSource,
                _ => VideoConstants.Messages.InvalidUrl,
            };
            return Result<CreateVideoResponse>.Failure(
                _localizer, urlErrorKey, HttpStatusCode.BadRequest);
        }

        // Phase 3 Step 2: fail fast on file shape BEFORE any DB or blob write.
        // 422 matches the existing UploadAttachmentAsync doc-comment convention
        // for AttachmentTooLarge — a well-formed request whose payload is
        // semantically rejected, not a malformed request (400).
        if (thumbnail is not null)
        {
            string? thumbnailShapeError = ValidateUploadedFile(
                thumbnail,
                VideoConstants.AllowedThumbnailContentTypes,
                VideoConstants.ThumbnailMaxSizeBytes,
                VideoConstants.Messages.ThumbnailInvalidType,
                VideoConstants.Messages.ThumbnailTooLarge);
            if (thumbnailShapeError is not null)
                return Result<CreateVideoResponse>.Failure(
                    _localizer, thumbnailShapeError, HttpStatusCode.UnprocessableEntity);
        }

        if (attachment is not null)
        {
            string? attachmentShapeError = ValidateUploadedFile(
                attachment,
                VideoConstants.AllowedAttachmentContentTypes,
                VideoConstants.AttachmentMaxSizeBytes,
                VideoConstants.Messages.AttachmentInvalidType,
                VideoConstants.Messages.AttachmentTooLarge);
            if (attachmentShapeError is not null)
                return Result<CreateVideoResponse>.Failure(
                    _localizer, attachmentShapeError, HttpStatusCode.UnprocessableEntity);
        }


        // ── Saga step 1: VideoAsset row (+ unit links) commits BEFORE any blob I/O ──

        // Unit assignment happens via a dedicated endpoint, not at creation.

        // ── Merged-creation refactor: scope + exam validated BEFORE any
        // write, same fail-fast philosophy as the file-shape checks above.
        var scopeInputs = new List<VideoScopeInputDto>();
        if (request.Scopes is { Count: > 0 })
        {
            foreach (var group in request.Scopes)
            {
                foreach (var id in group.Ids.Distinct())
                {
                    scopeInputs.Add(group.ScopeType switch
                    {
                        VideoScopeType.Session => new VideoScopeInputDto
                        {
                            ScopeType = VideoScopeType.Session,
                            SessionId = id,
                        },
                        VideoScopeType.SessionGroup => new VideoScopeInputDto
                        {
                            ScopeType = VideoScopeType.SessionGroup,
                            SessionGroupId = id,
                        },
                        _ => throw new ArgumentOutOfRangeException(
                            nameof(group.ScopeType), group.ScopeType, "Unrecognized scope type."),
                    });
                }
            }

            var scopeShapeError = ValidateScopeShape(scopeInputs);
            if (scopeShapeError is not null)
                return Result<CreateVideoResponse>.Failure(
                    _localizer, scopeShapeError, HttpStatusCode.BadRequest);

            var scopeOwnershipError = await ValidateScopeOwnershipAsync(teacherId, scopeInputs);
            if (scopeOwnershipError is not null)
                return Result<CreateVideoResponse>.Failure(
                    _localizer, scopeOwnershipError, HttpStatusCode.BadRequest);
        }

        if (request.Exam is not null)
        {
            var examShapeError = ValidateExamShape(request.Exam);
            if (examShapeError is not null)
                return Result<CreateVideoResponse>.Failure(
                    _localizer, examShapeError, HttpStatusCode.BadRequest);
        }

        // ── Saga step 1: VideoAsset row (+ scopes + exam) commits BEFORE any
        // blob I/O. Everything here is one SQL transaction — video, scopes,
        // and exam are all-or-nothing. Blob uploads (below) necessarily
        // cannot join this transaction; that part stays a saga with
        // compensation, same as Phase 3.
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        VideoAsset video;
        int scopesAdded = 0;
        int studentsInScope = 0;
        long? examId = null;
        try
        {
            video = new VideoAsset
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
            await _unitOfWork.SaveChangesAsync(); // generates video.Id

            var utcNow = DateTime.UtcNow;

            if (scopeInputs.Count > 0)
            {
                var scopeRows = scopeInputs
                    .Select(input => BuildScopeEntity(input, video, actingUserId, utcNow))
                    .ToList();

                await _unitOfWork.VideoAssetsRepo.AddScopesAsync(scopeRows);
                await _unitOfWork.SaveChangesAsync();

                scopesAdded = scopeRows.Count;
                var resolved = await _scopeResolver.ResolveFromPersistedScopesAsync(teacherId, scopeRows);
                studentsInScope = resolved.Count;
            }

            if (request.Exam is not null)
            {
                var exam = BuildExamEntity(request.Exam, video.Id, teacherId, actingUserId, utcNow);
                await _unitOfWork.VideoAssetsRepo.AddExamAsync(exam);
                await _unitOfWork.SaveChangesAsync(); // generates exam.Id (+ cascaded question/option ids)

                examId = exam.Id;
            }

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }

        // ── Saga step 2: blob I/O — outside the SQL transaction, compensated on failure ──
        string? thumbnailBlobPath = null;
        if (thumbnail is not null)
        {
            try
            {
                thumbnailBlobPath = await UploadThumbnailBlobAsync(teacherId, video.Id, thumbnail);
                await _unitOfWork.VideoAssetsRepo.SetThumbnailBlobPathAsync(video.Id, thumbnailBlobPath);
            }
            catch (Exception ex)
            {
                await CompensateFailedCreateAsync(
                    teacherId, actingUserId, video.Id,
                    thumbnailBlobPath, attachmentBlobPath: null, ex, "thumbnail upload");
                return Result<CreateVideoResponse>.Failure(
                    _localizer, "ServerError", HttpStatusCode.InternalServerError);
            }
        }

        VideoAttachmentDto? attachmentDto = null;
        if (attachment is not null)
        {
            string? attachmentBlobPath = null;
            try
            {
                attachmentBlobPath = await UploadAttachmentBlobAsync(teacherId, video.Id, attachment);

                var attachmentEntity = new VideoAttachment
                {
                    VideoAssetId = video.Id,
                    TeacherId = teacherId,
                    FileName = attachment.FileName,
                    ContentType = attachment.ContentType,
                    FileSizeBytes = attachment.Length,
                    BlobPath = attachmentBlobPath,
                    UploadedByUserId = actingUserId,
                    CreateAt = DateTime.UtcNow,
                };

                await _unitOfWork.VideoAssetsRepo.AddAttachmentAsync(attachmentEntity);
                await _unitOfWork.SaveChangesAsync();

                attachmentDto = new VideoAttachmentDto
                {
                    Id = attachmentEntity.Id,
                    FileName = attachmentEntity.FileName,
                    ContentType = attachmentEntity.ContentType,
                    FileSizeBytes = attachmentEntity.FileSizeBytes,
                    ReadUrl = await _fileStorage.GetReadUrlAsync(attachmentBlobPath),
                    CreatedAt = attachmentEntity.CreateAt,
                };
            }
            catch (Exception ex)
            {
                await CompensateFailedCreateAsync(
                    teacherId, actingUserId, video.Id,
                    thumbnailBlobPath, attachmentBlobPath, ex, "attachment upload");
                return Result<CreateVideoResponse>.Failure(
                    _localizer, "ServerError", HttpStatusCode.InternalServerError);
            }
        }

        string? thumbnailReadUrl = thumbnailBlobPath is null
            ? null
            : await _fileStorage.GetReadUrlAsync(thumbnailBlobPath);
        return Result<CreateVideoResponse>.Success(
                   new CreateVideoResponse
                   {
                       VideoAssetId = video.Id,
                       ThumbnailReadUrl = thumbnailReadUrl,
                       Attachment = attachmentDto,
                       ScopesAdded = scopesAdded,
                       StudentsInScope = studentsInScope,
                       ExamId = examId,
                   },
                   _localizer,
                   VideoConstants.Messages.VideoCreated,
                   HttpStatusCode.Created);
    }

    /// <summary>
    /// Validates an uploaded file's content-type and size before any DB or blob
    /// write (Phase 3 Step 2 — fail fast). Returns a localization key on
    /// failure, null on success.
    /// </summary>
    private static string? ValidateUploadedFile(
        IFormFile file, string[] allowedContentTypes, long maxSizeBytes,
        string invalidTypeKey, string tooLargeKey)
    {
        bool typeAllowed = allowedContentTypes.Any(
            t => string.Equals(t, file.ContentType, StringComparison.OrdinalIgnoreCase));
        if (!typeAllowed)
            return invalidTypeKey;

        if (file.Length > maxSizeBytes)
            return tooLargeKey;

        return null;
    }

    /// <summary>
    /// Uploads a thumbnail to <c>{teacherId}/{videoAssetId}/thumbnail-{guid}.{ext}</c>,
    /// mirroring <see cref="VideoAttachment.BlobPath"/>'s documented convention.
    /// </summary>
    private async Task<string> UploadThumbnailBlobAsync(
        long teacherId, long videoAssetId, IFormFile thumbnail)
    {
        string ext = Path.GetExtension(thumbnail.FileName);
        string blobPath = $"{teacherId}/{videoAssetId}/thumbnail-{Guid.NewGuid()}{ext}";

        await using var stream = thumbnail.OpenReadStream();
        return await _fileStorage.UploadAsync(blobPath, stream, thumbnail.ContentType);
    }

    /// <summary>
    /// Uploads an attachment to <c>{teacherId}/{videoAssetId}/{guid}-{fileName}</c> —
    /// the exact convention documented on <see cref="VideoAttachment.BlobPath"/>.
    /// </summary>
    private async Task<string> UploadAttachmentBlobAsync(
        long teacherId, long videoAssetId, IFormFile attachment)
    {
        string blobPath = $"{teacherId}/{videoAssetId}/{Guid.NewGuid()}-{attachment.FileName}";

        await using var stream = attachment.OpenReadStream();
        return await _fileStorage.UploadAsync(blobPath, stream, attachment.ContentType);
    }

    /// <summary>
    /// Compensating action for the CreateVideoAsync saga (Phase 3): a blob step
    /// failed after the VideoAsset row already committed. Deletes whichever
    /// blobs did upload, then hard-deletes the video row via the same
    /// <see cref="DeleteVideoAsync(long, long, long)"/> path Story E uses — so a
    /// compensated create gets an audit trail exactly like a deliberate delete,
    /// and unit links are removed by the same NoAction FK chain.
    ///
    /// NOTE: no ILogger is injected into VideoService (same gap noted in
    /// PaymentService) — using Console.Error as an interim measure. Swap for
    /// ILogger once one is added to this service's dependencies.
    /// </summary>
    private async Task CompensateFailedCreateAsync(
        long teacherId, long actingUserId, long videoAssetId,
        string? thumbnailBlobPath, string? attachmentBlobPath,
        Exception originalException, string failedStep)
    {
        Console.Error.WriteLine(
            $"[VideoService] CreateVideoAsync compensation triggered: {failedStep} failed for " +
            $"videoAssetId={videoAssetId}, teacherId={teacherId}. Reason: {originalException.Message}");

        if (thumbnailBlobPath is not null)
        {
            try { await _fileStorage.DeleteAsync(thumbnailBlobPath); }
            catch (Exception cleanupEx)
            {
                Console.Error.WriteLine(
                    $"[VideoService] Compensation: failed to delete thumbnail blob " +
                    $"'{thumbnailBlobPath}': {cleanupEx.Message}");
            }
        }

        if (attachmentBlobPath is not null)
        {
            try { await _fileStorage.DeleteAsync(attachmentBlobPath); }
            catch (Exception cleanupEx)
            {
                Console.Error.WriteLine(
                    $"[VideoService] Compensation: failed to delete attachment blob " +
                    $"'{attachmentBlobPath}': {cleanupEx.Message}");
            }
        }

        try
        {
            await DeleteVideoAsync(teacherId, actingUserId, videoAssetId);
        }
        catch (Exception deleteEx)
        {
            Console.Error.WriteLine(
                $"[VideoService] Compensation: failed to delete orphaned VideoAsset " +
                $"{videoAssetId}: {deleteEx.Message}. Manual cleanup required — the video row " +
                "may still exist pointing at a missing or partial blob.");
        }
    }
    /// <summary>
    /// Validates an exam's shape before any DB write (fail-fast, same
    /// philosophy as ValidateScopeShape). Returns a localization key on
    /// failure, null on success.
    /// </summary>
    private static string? ValidateExamShape(CreateExamDto exam)
    {
        if (string.IsNullOrWhiteSpace(exam.Title))
            return VideoConstants.Messages.ExamTitleRequired;

        if (exam.Questions is null || exam.Questions.Count == 0)
            return VideoConstants.Messages.ExamMustHaveQuestions;

        foreach (var q in exam.Questions)
        {
            if (string.IsNullOrWhiteSpace(q.Text))
                return VideoConstants.Messages.ExamQuestionTextRequired;

            if (q.Options is null || q.Options.Count < 2)
                return VideoConstants.Messages.ExamQuestionNeedsOptions;

            int correctCount = q.Options.Count(o => o.IsCorrect);

            if (q.QuestionType == VideoExamQuestionType.SingleChoice && correctCount != 1)
                return VideoConstants.Messages.SingleChoiceNeedsExactlyOneCorrect;

            if (q.QuestionType == VideoExamQuestionType.MultipleChoice && correctCount < 1)
                return VideoConstants.Messages.MultipleChoiceNeedsAtLeastOneCorrect;
        }

        return null;
    }

    /// <summary>
    /// Builds the full VideoExam entity graph (exam → questions → options) from
    /// the create-time DTO. Not persisted here — caller AddExamAsync's the
    /// returned root and the change tracker cascades the whole graph on
    /// SaveChangesAsync.
    /// </summary>
    private static VideoExam BuildExamEntity(
        CreateExamDto dto, long videoAssetId, long teacherId, long actingUserId, DateTime utcNow)
    {
        var exam = new VideoExam
        {
            VideoAssetId = videoAssetId,
            TeacherId = teacherId,
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            CreatedByUserId = actingUserId,
            CreateAt = utcNow,
        };

        int questionOrder = 0;
        foreach (var qDto in dto.Questions)
        {
            var question = new VideoExamQuestion
            {
                Text = qDto.Text.Trim(),
                QuestionType = qDto.QuestionType,
                SortOrder = questionOrder++,
                CreateAt = utcNow,
            };

            int optionOrder = 0;
            foreach (var oDto in qDto.Options)
            {
                question.Options.Add(new VideoExamQuestionOption
                {
                    Text = oDto.Text.Trim(),
                    IsCorrect = oDto.IsCorrect,
                    SortOrder = optionOrder++,
                    CreateAt = utcNow,
                });
            }

            exam.Questions.Add(question);
        }

        return exam;
    }
    public async Task<Result<string>> GetAttachmentDownloadUrlAsync(
        long teacherId, long videoAssetId, long attachmentId)
    {
        var attachment = await _unitOfWork.VideoAssetsRepo
            .GetAttachmentByIdAsync(attachmentId, videoAssetId, teacherId);
        if (attachment is null)
            return Result<string>.Failure(
                _localizer, VideoConstants.Messages.AttachmentNotFound, HttpStatusCode.NotFound);

        string url = await _fileStorage.GetReadUrlAsync(attachment.BlobPath, attachment.FileName);
        return Result<string>.Success(url, _localizer);
    }
    /// <summary>
    /// Validates every id belongs to the calling teacher. Shared by
    /// <see cref="AssignVideoToUnitsAsync"/> and <see cref="UpdateVideoAsync"/>'s
    /// UnitIds handling so both run through identical ownership checks.
    /// </summary>
    private async Task<string?> ValidateUnitIdsOwnershipAsync(long teacherId, IEnumerable<long> unitIds)
    {
        foreach (var unitId in unitIds.Distinct())
        {
            var unit = await _unitOfWork.VideoUnitsRepo.GetUnitByIdAndTeacherAsync(unitId, teacherId);
            if (unit is null)
                return VideoConstants.Messages.VideoUnitNotFound;
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<Result<AssignVideoUnitsResponse>> AssignVideoToUnitsAsync(
        long teacherId, long videoAssetId, AssignVideoUnitsRequest request)
    {
        var video = await _unitOfWork.VideoAssetsRepo.GetVideoByIdAndTeacherAsync(videoAssetId, teacherId);
        if (video is null)
            return Result<AssignVideoUnitsResponse>.Failure(
                _localizer, VideoConstants.Messages.VideoNotFound, HttpStatusCode.NotFound);

        var unitIds = request.UnitIds?.Distinct().ToList() ?? new List<long>();

        var ownershipError = await ValidateUnitIdsOwnershipAsync(teacherId, unitIds);
        if (ownershipError is not null)
            return Result<AssignVideoUnitsResponse>.Failure(
                _localizer, ownershipError, HttpStatusCode.BadRequest);

        // ReplaceUnitLinksAsync's delete is an immediate ExecuteDeleteAsync
        // while the insert is staged for SaveChangesAsync — without a
        // transaction, a failed SaveChangesAsync would leave the video with
        // its links deleted but the new ones never inserted. Wrapping this
        // standalone call closes that gap (UpdateVideoAsync's own use of
        // ReplaceUnitLinksAsync is already safe — it runs inside that
        // method's existing transaction).
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _unitOfWork.VideoAssetsRepo.ReplaceUnitLinksAsync(videoAssetId, unitIds);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }

        return Result<AssignVideoUnitsResponse>.Success(
            new AssignVideoUnitsResponse { UnitIds = unitIds },
            _localizer, VideoConstants.Messages.VideoUnitsAssigned);
    }
}
