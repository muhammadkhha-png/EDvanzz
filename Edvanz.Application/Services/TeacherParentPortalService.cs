using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Edvanz.Application.Common;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ParentPortal;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;

namespace Edvanz.Application.Services;

/// <inheritdoc cref="ITeacherParentPortalService"/>
public sealed class TeacherParentPortalService : ITeacherParentPortalService
{
    private const string ApproveAction = "approve";
    private const string RejectAction = "reject";

    private const int MaxPageSize = 100;
    private const int MaxBulkIds = 200;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public TeacherParentPortalService(
        IUnitOfWork unitOfWork,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<ParentPortalRequestListItemDto>>>> GetPendingRequestsAsync(
        long teacherId, int page, int pageSize)
    {
        int safePage = page < 1 ? 1 : page;
        int safePageSize = pageSize < 1 ? 20 : pageSize > MaxPageSize ? MaxPageSize : pageSize;

        // §3.8: total is counted separately, before the page is fetched.
        int total = await _unitOfWork.ParentPortalAccesses.CountPendingForTeacherAsync(teacherId);
        var rows = await _unitOfWork.ParentPortalAccesses
            .GetPendingForTeacherPagedAsync(teacherId, safePage, safePageSize);

        var payload = new PaginatedResponse<List<ParentPortalRequestListItemDto>>
        {
            totalCount = total,
            page = safePage,
            pageSize = safePageSize,
            totalPages = (int)Math.Ceiling(total / (double)safePageSize),
            data = rows.Select(ToRequestItem).ToList()
        };

        return Result<PaginatedResponse<List<ParentPortalRequestListItemDto>>>.Success(
            payload, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<ParentPortalFollowerListItemDto>> ApproveRequestAsync(
        long teacherId, long requestId, long actingUserId) =>
        await ResolveSingleAsync(teacherId, requestId, actingUserId,
            ParentPortalAccessStatus.Active, "ParentPortalAccessGranted");

    /// <inheritdoc />
    public async Task<Result<ParentPortalFollowerListItemDto>> RejectRequestAsync(
        long teacherId, long requestId, long actingUserId) =>
        await ResolveSingleAsync(teacherId, requestId, actingUserId,
            ParentPortalAccessStatus.Rejected, "ParentPortalAccessRejected");

    /// <inheritdoc />
    public async Task<Result<ParentPortalBulkResultDto>> BulkResolveAsync(
        long teacherId, ParentPortalBulkActionDto dto, long actingUserId)
    {
        string action = (dto.Action ?? string.Empty).Trim().ToLowerInvariant();
        if (action != ApproveAction && action != RejectAction)
            return Result<ParentPortalBulkResultDto>.Failure(
                _localizer, "BadRequest", HttpStatusCode.BadRequest);

        var ids = (dto.Ids ?? new List<long>()).Where(id => id > 0).Distinct().Take(MaxBulkIds).ToList();
        if (ids.Count == 0)
            return Result<ParentPortalBulkResultDto>.Failure(
                _localizer, "BadRequest", HttpStatusCode.BadRequest);

        // Tenant-scoped fetch: ids outside this teacher simply do not come back.
        var rows = await _unitOfWork.ParentPortalAccesses.GetByIdsForTeacherAsync(ids, teacherId);
        var byId = rows.ToDictionary(r => r.Id);

        var target = action == ApproveAction
            ? ParentPortalAccessStatus.Active
            : ParentPortalAccessStatus.Rejected;

        var now = DateTime.UtcNow;
        var result = new ParentPortalBulkResultDto();

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync();
        try
        {
            foreach (long id in ids)
            {
                if (!byId.TryGetValue(id, out var grant) ||
                    grant.Status != ParentPortalAccessStatus.Pending ||
                    // Approving a follower for a roster record that has since been deleted would
                    // grant access to nothing — skip it rather than create a dead grant.
                    (target == ParentPortalAccessStatus.Active && grant.TeacherStudent is null))
                {
                    result.SkippedIds.Add(id);
                    continue;
                }

                Apply(grant, target, actingUserId, now);
                await _unitOfWork.ParentPortalAccesses.UpdateAsync(grant);
                result.ProcessedIds.Add(id);
                result.Affected++;
            }

            await _unitOfWork.SaveChangesAsync();
            if (ownsTransaction) await _unitOfWork.CommitAsync();
        }
        catch
        {
            if (ownsTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }

        return Result<ParentPortalBulkResultDto>.Success(
            result, _localizer,
            target == ParentPortalAccessStatus.Active ? "ParentPortalAccessGranted" : "ParentPortalAccessRejected",
            HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<List<ParentPortalFollowerListItemDto>>> GetFollowersAsync(
        long teacherId, long teacherStudentId)
    {
        // Tenant guard on the STUDENT too, so a foreign roster id 404s instead of returning [].
        var student = await _unitOfWork.Users.GetActiveTeacherStudentByIdAsync(teacherId, teacherStudentId);
        if (student is null)
            return Result<List<ParentPortalFollowerListItemDto>>.Failure(
                _localizer, "RosterStudentNotFound", HttpStatusCode.NotFound);

        var rows = await _unitOfWork.ParentPortalAccesses
            .GetFollowersForStudentAsync(teacherId, teacherStudentId);

        // GetFollowersForStudentAsync does not load the navigation (one known student), so the
        // name/code are filled from the already-resolved roster record.
        var items = rows.Select(r => ToFollowerItem(r, student)).ToList();

        return Result<List<ParentPortalFollowerListItemDto>>.Success(items, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> RevokeFollowerAsync(long teacherId, long accessId, long actingUserId)
    {
        var grant = await _unitOfWork.ParentPortalAccesses.GetByIdForTeacherAsync(accessId, teacherId);
        if (grant is null ||
            (grant.Status != ParentPortalAccessStatus.Active && grant.Status != ParentPortalAccessStatus.Pending))
            return Result<bool>.Failure(_localizer, "ParentPortalRequestNotFound", HttpStatusCode.NotFound);

        Apply(grant, ParentPortalAccessStatus.Revoked, actingUserId, DateTime.UtcNow);
        await _unitOfWork.ParentPortalAccesses.UpdateAsync(grant);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, "ParentPortalAccessRevoked", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<ParentPortalSummaryDto>> GetSummaryAsync(long teacherId)
    {
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);

        var dto = new ParentPortalSummaryDto
        {
            PendingCount = await _unitOfWork.ParentPortalAccesses.CountPendingForTeacherAsync(teacherId),
            FollowedStudentsCount = await _unitOfWork.ParentPortalAccesses
                .CountFollowedStudentsForTeacherAsync(teacherId),
            StudentsMissingParentPhone = await _unitOfWork.Users
                .CountStudentsMissingParentPhoneAsync(teacherId),
            PortalEnabled = config?.ParentPortalEnabled ?? false
        };

        return Result<ParentPortalSummaryDto>.Success(dto, _localizer, "Success", HttpStatusCode.OK);
    }

    // ══════════════════════════════════════════════════════════════════════
    // PRIVATE
    // ══════════════════════════════════════════════════════════════════════

    private async Task<Result<ParentPortalFollowerListItemDto>> ResolveSingleAsync(
        long teacherId, long requestId, long actingUserId,
        ParentPortalAccessStatus target, string successKey)
    {
        var grant = await _unitOfWork.ParentPortalAccesses.GetByIdForTeacherAsync(requestId, teacherId);
        if (grant is null)
            return Result<ParentPortalFollowerListItemDto>.Failure(
                _localizer, "ParentPortalRequestNotFound", HttpStatusCode.NotFound);

        // Approving an already-Active grant is a no-op success (double-tap / retry safe).
        if (grant.Status == target)
            return Result<ParentPortalFollowerListItemDto>.Success(
                ToFollowerItem(grant, grant.TeacherStudent), _localizer, successKey, HttpStatusCode.OK);

        if (grant.Status != ParentPortalAccessStatus.Pending)
            return Result<ParentPortalFollowerListItemDto>.Failure(
                _localizer, "ParentPortalRequestNotFound", HttpStatusCode.NotFound);

        if (target == ParentPortalAccessStatus.Active && grant.TeacherStudent is null)
            return Result<ParentPortalFollowerListItemDto>.Failure(
                _localizer, "ParentPortalStudentRemoved", HttpStatusCode.NotFound);

        Apply(grant, target, actingUserId, DateTime.UtcNow);
        await _unitOfWork.ParentPortalAccesses.UpdateAsync(grant);
        await _unitOfWork.SaveChangesAsync();

        return Result<ParentPortalFollowerListItemDto>.Success(
            ToFollowerItem(grant, grant.TeacherStudent), _localizer, successKey, HttpStatusCode.OK);
    }

    /// <summary>Single mutation point for a teacher-side decision, so the audit columns can never drift apart.</summary>
    private static void Apply(
        ParentPortalAccess grant, ParentPortalAccessStatus target, long actingUserId, DateTime nowUtc)
    {
        grant.Status = target;
        grant.RespondedAt = nowUtc;
        grant.RespondedByUserId = actingUserId;
    }

    private static ParentPortalRequestListItemDto ToRequestItem(ParentPortalAccess grant) => new()
    {
        Id = grant.Id,
        TeacherStudentId = grant.TeacherStudentId,
        StudentName = grant.TeacherStudent?.StudentName,
        StudentCode = grant.TeacherStudent?.StudentCode,
        ClaimedPhoneMasked = EgyptianPhoneNumber.Mask(grant.ClaimedPhone),
        PhoneMatchesRoster = EgyptianPhoneNumber.AreSameNumber(
            grant.ClaimedPhone, grant.TeacherStudent?.ParentPhoneNumber),
        RequestedAt = grant.RequestedAt,
        Status = grant.Status
    };

    private static ParentPortalFollowerListItemDto ToFollowerItem(
        ParentPortalAccess grant, TeacherStudent? student) => new()
    {
        Id = grant.Id,
        TeacherStudentId = grant.TeacherStudentId,
        StudentName = student?.StudentName,
        StudentCode = student?.StudentCode,
        ClaimedPhoneMasked = EgyptianPhoneNumber.Mask(grant.ClaimedPhone),
        Status = grant.Status,
        AutoApproved = grant.AutoApproved,
        RequestedAt = grant.RequestedAt,
        RespondedAt = grant.RespondedAt,
        LastSeenAt = grant.LastSeenAt
    };
}
