using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Center;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// SuperAdmin management of center subscriptions. Activation mirrors the teacher flow: lock the
/// current row (UPDLOCK), early-renew from its EndDate (no days lost), flip IsCurrent + insert the
/// new period (the filtered unique index guarantees exactly one current row), commit, then invalidate
/// the effective-subscription cache for EVERY teacher of the center (their status is redirected to
/// this subscription — see UserRepo.GetCurrentSubscriptionStatusAsync).
/// </summary>
public class AdminCenterSubscriptionService : IAdminCenterSubscriptionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ISubscriptionCacheService _cache;

    public AdminCenterSubscriptionService(
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer,
        ISubscriptionCacheService cache)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<Result<string>> ActivateAsync(long adminUserId, ActivateCenterSubscriptionDto dto)
    {
        var center = await _unitOfWork.Centers.GetCenterByIdAsync(dto.CenterId);
        if (center == null)
            return Result<string>.Failure(_localizer, "CenterNotFound", HttpStatusCode.NotFound);
        if (!IsValidPackage(dto))
            return Result<string>.Failure(_localizer, "InvalidQuotaPackage", HttpStatusCode.BadRequest);

        var amount = await ComputeAmountAsync(dto);
        return await ActivateCoreAsync(adminUserId, dto.CenterId, dto, dto.DurationDays, amount, dto.Note);
    }

    /// <inheritdoc />
    public async Task<Result<List<CenterSubscriptionRequestQueueItemDto>>> GetPendingRequestsAsync()
    {
        var requests = await _unitOfWork.Centers.GetPendingCenterSubscriptionRequestsAsync();
        var list = new List<CenterSubscriptionRequestQueueItemDto>();
        foreach (var r in requests)
        {
            var center = await _unitOfWork.Centers.GetCenterByIdAsync(r.CenterId);
            list.Add(new CenterSubscriptionRequestQueueItemDto
            {
                RequestId = r.Id,
                CenterId = r.CenterId,
                CenterName = center?.Name ?? string.Empty,
                CenterCode = center?.CenterCode ?? string.Empty,
                FullTeacherSlots = r.FullTeacherSlots,
                ManagerialTeacherSlots = r.ManagerialTeacherSlots,
                StudentCapacityTotal = r.StudentCapacityTotal,
                StudentCapacityUnderFull = r.StudentCapacityUnderFull,
                StudentCapacityUnderManagerial = r.StudentCapacityUnderManagerial,
                ComputedAmountEGP = r.ComputedAmountEGP,
                Note = r.Note,
                RequestedAt = r.RequestedAt,
                Status = r.Status
            });
        }
        return Result<List<CenterSubscriptionRequestQueueItemDto>>.Success(list, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<string>> ApproveRequestAsync(long adminUserId, long requestId, ApproveCenterSubscriptionRequestDto dto)
    {
        var request = await _unitOfWork.Centers.GetCenterSubscriptionRequestByIdAsync(requestId);
        if (request == null)
            return Result<string>.Failure(_localizer, "CenterSubscriptionRequestNotFound", HttpStatusCode.NotFound);
        if (request.Status != SubscriptionRequestStatus.Pending)
            return Result<string>.Failure(_localizer, "CenterSubscriptionRequestNotPending", HttpStatusCode.Conflict);
        if (!IsValidPackage(dto))
            return Result<string>.Failure(_localizer, "InvalidQuotaPackage", HttpStatusCode.BadRequest);

        var amount = await ComputeAmountAsync(dto);

        // Mark the request resolved in the SAME transaction as the activation.
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var previous = await _unitOfWork.Centers.GetCurrentCenterSubscriptionForUpdateAsync(request.CenterId);
            var now = DateTime.UtcNow;
            // Early renewal: the new package starts NOW (it becomes IsCurrent immediately, so its
            // quotas apply immediately) and the old period's unused remainder is APPENDED to its
            // end — no paid days lost. Never future-date StartDate: a future-dated current row
            // derives Expired at every date-based gate (HasActiveSubscriptionAsync /
            // SubscriptionStatusCalculator), knocking the whole center back to free tier until
            // the old period would have ended.
            var carryOver = previous != null && previous.EndDate > now
                ? previous.EndDate - now
                : TimeSpan.Zero;

            var newSub = new CenterSubscription
            {
                CenterId = request.CenterId,
                StartDate = now,
                EndDate = now.AddDays(dto.DurationDays) + carryOver,
                FullTeacherSlots = dto.FullTeacherSlots,
                ManagerialTeacherSlots = dto.ManagerialTeacherSlots,
                StudentCapacityTotal = dto.StudentCapacityTotal,
                StudentCapacityUnderFull = dto.StudentCapacityUnderFull,
                StudentCapacityUnderManagerial = dto.StudentCapacityUnderManagerial,
                AmountPaidEGP = amount,
                PaymentConfirmedAt = now,
                Note = dto.Note,
                CreatedByUserId = adminUserId,
                CreateAt = now
            };
            await _unitOfWork.Centers.FlipCurrentAndInsertNewCenterSubscriptionAsync(previous, newSub);

            var trackedRequest = await _unitOfWork.Centers.GetCenterSubscriptionRequestByIdAsync(requestId);
            if (trackedRequest != null)
            {
                trackedRequest.Status = SubscriptionRequestStatus.Approved;
                trackedRequest.ResolvedAt = now;
                trackedRequest.ResolvedByUserId = adminUserId;
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            await InvalidateCenterTeacherCacheAsync(request.CenterId);
            return Result<string>.Success("ok", _localizer, "CenterSubscriptionRequestApproved");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<string>.Failure(_localizer, "ServerError");
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> RejectRequestAsync(long adminUserId, long requestId, RejectCenterSubscriptionRequestDto dto)
    {
        var request = await _unitOfWork.Centers.GetCenterSubscriptionRequestByIdAsync(requestId);
        if (request == null)
            return Result<string>.Failure(_localizer, "CenterSubscriptionRequestNotFound", HttpStatusCode.NotFound);
        if (request.Status != SubscriptionRequestStatus.Pending)
            return Result<string>.Failure(_localizer, "CenterSubscriptionRequestNotPending", HttpStatusCode.Conflict);

        request.Status = SubscriptionRequestStatus.Rejected;
        request.ResolvedAt = DateTime.UtcNow;
        request.ResolvedByUserId = adminUserId;
        request.RejectionReason = dto.RejectionReason;
        await _unitOfWork.SaveChangesAsync();

        return Result<string>.Success("ok", _localizer, "CenterSubscriptionRequestRejected");
    }

    /// <inheritdoc />
    public async Task<Result<CenterPricingDto>> GetPricingAsync()
    {
        var pricing = await _unitOfWork.GetRepository<CenterSubscriptionPricingSetting, long>().GetByIdAsync(1);
        var dto = new CenterPricingDto
        {
            FullTeacherSlotPriceEGP = pricing?.FullTeacherSlotPriceEGP ?? 0m,
            ManagerialTeacherSlotPriceEGP = pricing?.ManagerialTeacherSlotPriceEGP ?? 0m
        };
        return Result<CenterPricingDto>.Success(dto, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<CenterPricingDto>> UpdatePricingAsync(long adminUserId, CenterPricingDto dto)
    {
        if (dto.FullTeacherSlotPriceEGP < 0 || dto.ManagerialTeacherSlotPriceEGP < 0)
            return Result<CenterPricingDto>.Failure(_localizer, "InvalidRevenueSharePercent", HttpStatusCode.BadRequest);

        var repo = _unitOfWork.GetRepository<CenterSubscriptionPricingSetting, long>();
        var pricing = await repo.GetByIdAsync(1);
        if (pricing == null)
            return Result<CenterPricingDto>.Failure(_localizer, "ServerError");

        pricing.FullTeacherSlotPriceEGP = dto.FullTeacherSlotPriceEGP;
        pricing.ManagerialTeacherSlotPriceEGP = dto.ManagerialTeacherSlotPriceEGP;
        pricing.UpdatedAt = DateTime.UtcNow;
        pricing.UpdatedByUserId = adminUserId;
        await repo.UpdateAsync(pricing);
        await _unitOfWork.SaveChangesAsync();

        return Result<CenterPricingDto>.Success(dto, _localizer, "CenterPricingUpdated");
    }

    // ── shared ──
    private async Task<Result<string>> ActivateCoreAsync(long adminUserId, long centerId, CenterQuotaPackage pkg, int durationDays, decimal amount, string? note)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var previous = await _unitOfWork.Centers.GetCurrentCenterSubscriptionForUpdateAsync(centerId);
            var now = DateTime.UtcNow;
            // Same early-renewal rule as the approve path: start NOW, append the old period's
            // unused remainder — never future-date StartDate (future start derives Expired at
            // every date-based gate and blocks the center until the old period's end).
            var carryOver = previous != null && previous.EndDate > now
                ? previous.EndDate - now
                : TimeSpan.Zero;

            var newSub = new CenterSubscription
            {
                CenterId = centerId,
                StartDate = now,
                EndDate = now.AddDays(durationDays <= 0 ? 30 : durationDays) + carryOver,
                FullTeacherSlots = pkg.FullTeacherSlots,
                ManagerialTeacherSlots = pkg.ManagerialTeacherSlots,
                StudentCapacityTotal = pkg.StudentCapacityTotal,
                StudentCapacityUnderFull = pkg.StudentCapacityUnderFull,
                StudentCapacityUnderManagerial = pkg.StudentCapacityUnderManagerial,
                AmountPaidEGP = amount,
                PaymentConfirmedAt = now,
                Note = note,
                CreatedByUserId = adminUserId,
                CreateAt = now
            };
            await _unitOfWork.Centers.FlipCurrentAndInsertNewCenterSubscriptionAsync(previous, newSub);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            await InvalidateCenterTeacherCacheAsync(centerId);
            return Result<string>.Success("ok", _localizer, "CenterSubscriptionActivated");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<string>.Failure(_localizer, "ServerError");
        }
    }

    private async Task<decimal> ComputeAmountAsync(CenterQuotaPackage pkg)
    {
        var pricing = await _unitOfWork.GetRepository<CenterSubscriptionPricingSetting, long>().GetByIdAsync(1);
        var fullRate = pricing?.FullTeacherSlotPriceEGP ?? 0m;
        var managerialRate = pricing?.ManagerialTeacherSlotPriceEGP ?? 0m;
        return pkg.FullTeacherSlots * fullRate + pkg.ManagerialTeacherSlots * managerialRate;
    }

    private async Task InvalidateCenterTeacherCacheAsync(long centerId)
    {
        // Best-effort: the center's teachers cache the effective (center-redirected) status by teacherId.
        var teacherIds = await _unitOfWork.Centers.GetTeacherIdsByCenterAsync(centerId);
        foreach (var tid in teacherIds)
        {
            try { await _cache.InvalidateAsync(tid); } catch { /* cache best-effort */ }
        }
    }

    private static bool IsValidPackage(CenterQuotaPackage p) =>
        p.FullTeacherSlots >= 0 && p.ManagerialTeacherSlots >= 0 &&
        p.StudentCapacityTotal >= 0 && p.StudentCapacityUnderFull >= 0 && p.StudentCapacityUnderManagerial >= 0;
}
