using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Center;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Helpers;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>Center-facing subscription: read the current entitlement + derived status + live usage,
/// and submit/cancel a package request (one live Pending at a time; SuperAdmin approves).</summary>
public class CenterSubscriptionService : ICenterSubscriptionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;

    public CenterSubscriptionService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<Result<CenterSubscriptionDto>> GetSubscriptionAsync(long centerId)
    {
        var center = await _unitOfWork.Centers.GetCenterByIdAsync(centerId);
        if (center == null)
            return Result<CenterSubscriptionDto>.Failure(_localizer, "CenterNotFound", HttpStatusCode.NotFound);

        var sub = await _unitOfWork.Centers.GetCurrentCenterSubscriptionAsync(centerId);
        var usedFull = await _unitOfWork.Centers.CountActiveTeachersByPlanAsync(centerId, SubscriptionPlanType.Full);
        var usedManagerial = await _unitOfWork.Centers.CountActiveTeachersByPlanAsync(centerId, SubscriptionPlanType.Managerial);
        var usedManagerialPlus = await _unitOfWork.Centers.CountActiveTeachersByPlanAsync(centerId, SubscriptionPlanType.ManagerialPlus);
        var usedStudents = await _unitOfWork.Centers.CountCenterStudentsTotalAsync(centerId);
        var usedStudentsFull = await _unitOfWork.Centers.CountCenterStudentsUnderPlanAsync(centerId, SubscriptionPlanType.Full);
        var usedStudentsManagerial = await _unitOfWork.Centers.CountCenterStudentsUnderPlanAsync(centerId, SubscriptionPlanType.Managerial);
        var usedStudentsManagerialPlus = await _unitOfWork.Centers.CountCenterStudentsUnderPlanAsync(centerId, SubscriptionPlanType.ManagerialPlus);
        var pending = await _unitOfWork.Centers.GetPendingRequestByCenterAsync(centerId);

        var dto = new CenterSubscriptionDto
        {
            HasSubscription = sub != null,
            UsedFullTeachers = usedFull,
            UsedManagerialTeachers = usedManagerial,
            UsedManagerialPlusTeachers = usedManagerialPlus,
            UsedStudentsTotal = usedStudents,
            UsedStudentsUnderFull = usedStudentsFull,
            UsedStudentsUnderManagerial = usedStudentsManagerial,
            UsedStudentsUnderManagerialPlus = usedStudentsManagerialPlus,
            HasPendingRequest = pending != null
        };

        if (sub != null)
        {
            var subForStatus = new TeacherSubscription { StartDate = sub.StartDate, EndDate = sub.EndDate, IsCurrent = true };
            dto.Status = SubscriptionStatusCalculator.Derive(subForStatus, DateTime.UtcNow).ToString();
            dto.StartDate = sub.StartDate;
            dto.EndDate = sub.EndDate;
            dto.DaysRemaining = SubscriptionStatusCalculator.DeriveDaysRemaining(subForStatus, DateTime.UtcNow);
            dto.FullTeacherSlots = sub.FullTeacherSlots;
            dto.ManagerialTeacherSlots = sub.ManagerialTeacherSlots;
            dto.ManagerialPlusTeacherSlots = sub.ManagerialPlusTeacherSlots;
            dto.StudentCapacityTotal = sub.StudentCapacityTotal;
            dto.StudentCapacityUnderFull = sub.StudentCapacityUnderFull;
            dto.StudentCapacityUnderManagerial = sub.StudentCapacityUnderManagerial;
            dto.StudentCapacityUnderManagerialPlus = sub.StudentCapacityUnderManagerialPlus;
        }

        if (pending != null)
        {
            dto.PendingRequest = new SubmitCenterSubscriptionRequestDto
            {
                FullTeacherSlots = pending.FullTeacherSlots,
                ManagerialTeacherSlots = pending.ManagerialTeacherSlots,
                ManagerialPlusTeacherSlots = pending.ManagerialPlusTeacherSlots,
                StudentCapacityTotal = pending.StudentCapacityTotal,
                StudentCapacityUnderFull = pending.StudentCapacityUnderFull,
                StudentCapacityUnderManagerial = pending.StudentCapacityUnderManagerial,
                StudentCapacityUnderManagerialPlus = pending.StudentCapacityUnderManagerialPlus,
                Note = pending.Note
            };
            dto.PendingRequestAmountEGP = pending.ComputedAmountEGP;
        }

        var latest = pending ?? await _unitOfWork.Centers.GetLatestRequestByCenterAsync(centerId);
        if (latest != null)
        {
            dto.LatestRequest = new CenterLatestRequestDto
            {
                Status = latest.Status.ToString(),
                FullTeacherSlots = latest.FullTeacherSlots,
                ManagerialTeacherSlots = latest.ManagerialTeacherSlots,
                ManagerialPlusTeacherSlots = latest.ManagerialPlusTeacherSlots,
                StudentCapacityTotal = latest.StudentCapacityTotal,
                StudentCapacityUnderFull = latest.StudentCapacityUnderFull,
                StudentCapacityUnderManagerial = latest.StudentCapacityUnderManagerial,
                StudentCapacityUnderManagerialPlus = latest.StudentCapacityUnderManagerialPlus,
                AmountEGP = latest.ComputedAmountEGP,
                Note = latest.Note,
                RequestedAt = latest.RequestedAt,
                ResolvedAt = latest.ResolvedAt,
                RejectionReason = latest.Status == SubscriptionRequestStatus.Rejected ? latest.RejectionReason : null
            };
        }

        return Result<CenterSubscriptionDto>.Success(dto, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<string>> SubmitRequestAsync(long centerId, long userId, SubmitCenterSubscriptionRequestDto dto)
    {
        var center = await _unitOfWork.Centers.GetCenterByIdAsync(centerId);
        if (center == null)
            return Result<string>.Failure(_localizer, "CenterNotFound", HttpStatusCode.NotFound);
        if (dto.FullTeacherSlots < 0 || dto.ManagerialTeacherSlots < 0 || dto.ManagerialPlusTeacherSlots < 0
            || dto.StudentCapacityTotal < 0 || dto.StudentCapacityUnderFull < 0
            || dto.StudentCapacityUnderManagerial < 0 || dto.StudentCapacityUnderManagerialPlus < 0)
            return Result<string>.Failure(_localizer, "InvalidQuotaPackage", HttpStatusCode.BadRequest);

        var existing = await _unitOfWork.Centers.GetPendingRequestByCenterAsync(centerId);
        if (existing != null)
            return Result<string>.Failure(_localizer, "CenterSubscriptionRequestPending", HttpStatusCode.Conflict);

        var pricing = await _unitOfWork.GetRepository<CenterSubscriptionPricingSetting, long>().GetByIdAsync(1);
        var amount = dto.FullTeacherSlots * (pricing?.FullTeacherSlotPriceEGP ?? 0m)
                   + dto.ManagerialTeacherSlots * (pricing?.ManagerialTeacherSlotPriceEGP ?? 0m)
                   + dto.ManagerialPlusTeacherSlots * (pricing?.ManagerialPlusTeacherSlotPriceEGP ?? 0m);

        var request = new CenterSubscriptionRequest
        {
            CenterId = centerId,
            FullTeacherSlots = dto.FullTeacherSlots,
            ManagerialTeacherSlots = dto.ManagerialTeacherSlots,
            ManagerialPlusTeacherSlots = dto.ManagerialPlusTeacherSlots,
            StudentCapacityTotal = dto.StudentCapacityTotal,
            StudentCapacityUnderFull = dto.StudentCapacityUnderFull,
            StudentCapacityUnderManagerial = dto.StudentCapacityUnderManagerial,
            StudentCapacityUnderManagerialPlus = dto.StudentCapacityUnderManagerialPlus,
            ComputedAmountEGP = amount,
            Note = dto.Note,
            Status = SubscriptionRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            RequestedByUserId = userId,
            CreateAt = DateTime.UtcNow
        };
        await _unitOfWork.GetRepository<CenterSubscriptionRequest, long>().AddAsync(request);

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A concurrent submit racing past the pending-check collides with
            // UX_CenterSubscriptionRequests_Center_Pending — surface the clean 409, not a raw 500.
            return Result<string>.Failure(_localizer, "CenterSubscriptionRequestPending", HttpStatusCode.Conflict);
        }

        return Result<string>.Success("ok", _localizer, "CenterSubscriptionRequestSubmitted");
    }

    /// <inheritdoc />
    public async Task<Result<string>> CancelRequestAsync(long centerId, long userId)
    {
        var pending = await _unitOfWork.Centers.GetPendingRequestByCenterAsync(centerId);
        if (pending == null)
            return Result<string>.Failure(_localizer, "CenterSubscriptionRequestNotFound", HttpStatusCode.NotFound);

        pending.Status = SubscriptionRequestStatus.Cancelled;
        pending.ResolvedAt = DateTime.UtcNow;
        pending.ResolvedByUserId = userId;
        await _unitOfWork.SaveChangesAsync();

        return Result<string>.Success("ok", _localizer, "Success");
    }
}
