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
/// Teacher-independence request/approval flow. A center-owned teacher asks to leave the center; a
/// SuperAdmin approves by DETACHING the teacher (clears <c>Teacher.CenterId</c> and the
/// center-plan/revenue overrides) so the teacher becomes a normal standalone teacher who subscribes
/// on their own. Mirrors the center-subscription request pattern (one live Pending per teacher).
/// </summary>
public class TeacherIndependenceService : ITeacherIndependenceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ISubscriptionCacheService _subscriptionCache;

    public TeacherIndependenceService(
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer,
        ISubscriptionCacheService subscriptionCache)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _subscriptionCache = subscriptionCache;
    }

    /// <inheritdoc />
    public async Task<Result<TeacherIndependenceRequestDto>> SubmitAsync(long teacherId, long teacherUserId, SubmitIndependenceRequestDto dto)
    {
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher == null)
            return Result<TeacherIndependenceRequestDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);
        if (teacher.CenterId == null)
            return Result<TeacherIndependenceRequestDto>.Failure(_localizer, "IndependenceNotUnderCenter", HttpStatusCode.BadRequest);

        var existing = await _unitOfWork.Centers.GetPendingIndependenceRequestByTeacherAsync(teacherId);
        if (existing != null)
            return Result<TeacherIndependenceRequestDto>.Failure(_localizer, "IndependenceRequestAlreadyPending", HttpStatusCode.Conflict);

        var now = DateTime.UtcNow;
        var request = new TeacherIndependenceRequest
        {
            TeacherId = teacherId,
            CenterId = teacher.CenterId.Value,
            Note = Clip(dto.Note),
            Status = SubscriptionRequestStatus.Pending,
            RequestedAt = now,
            RequestedByUserId = teacherUserId,
            CreateAt = now
        };
        await _unitOfWork.GetRepository<TeacherIndependenceRequest, long>().AddAsync(request);
        await _unitOfWork.SaveChangesAsync();

        return Result<TeacherIndependenceRequestDto>.Success(ToDto(request), _localizer, "IndependenceRequestSubmitted");
    }

    /// <inheritdoc />
    public async Task<Result<TeacherIndependenceRequestDto?>> GetMyRequestAsync(long teacherId)
    {
        var latest = await _unitOfWork.Centers.GetLatestIndependenceRequestByTeacherAsync(teacherId);
        return Result<TeacherIndependenceRequestDto?>.Success(latest == null ? null : ToDto(latest), _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<string>> CancelAsync(long teacherId, long teacherUserId)
    {
        var pending = await _unitOfWork.Centers.GetPendingIndependenceRequestByTeacherAsync(teacherId);
        if (pending == null)
            return Result<string>.Failure(_localizer, "IndependenceRequestNotFound", HttpStatusCode.NotFound);

        pending.Status = SubscriptionRequestStatus.Cancelled;
        pending.ResolvedAt = DateTime.UtcNow;
        pending.ResolvedByUserId = teacherUserId;
        await _unitOfWork.SaveChangesAsync();

        return Result<string>.Success("ok", _localizer, "IndependenceRequestCancelled");
    }

    /// <inheritdoc />
    public async Task<Result<List<IndependenceRequestQueueItemDto>>> GetPendingRequestsAsync()
    {
        var requests = await _unitOfWork.Centers.GetPendingIndependenceRequestsAsync();
        var list = requests.Select(r => new IndependenceRequestQueueItemDto
        {
            RequestId = r.Id,
            TeacherId = r.TeacherId,
            TeacherName = r.Teacher?.User?.FullName ?? string.Empty,
            TeacherCode = r.Teacher?.TeacherCode ?? string.Empty,
            CenterId = r.CenterId,
            CenterName = r.Center?.Name ?? string.Empty,
            CenterCode = r.Center?.CenterCode ?? string.Empty,
            Note = r.Note,
            RequestedAt = r.RequestedAt,
            Status = r.Status
        }).ToList();
        return Result<List<IndependenceRequestQueueItemDto>>.Success(list, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<string>> ApproveAsync(long adminUserId, long requestId)
    {
        var request = await _unitOfWork.Centers.GetIndependenceRequestByIdAsync(requestId);
        if (request == null)
            return Result<string>.Failure(_localizer, "IndependenceRequestNotFound", HttpStatusCode.NotFound);
        if (request.Status != SubscriptionRequestStatus.Pending)
            return Result<string>.Failure(_localizer, "IndependenceRequestNotPending", HttpStatusCode.Conflict);

        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(request.TeacherId);
        if (teacher == null)
            return Result<string>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Detach: the teacher becomes a standalone tenant. Their subscription status stops
            // resolving through the center (UserRepo.GetCurrentSubscriptionStatusAsync keys on CenterId),
            // so they fall to their own free tier until they subscribe. Their login is untouched.
            teacher.CenterId = null;
            teacher.CenterPlanType = null;
            teacher.RevenueSharePercentOverride = null;
            teacher.StudentCodeModeOverride = null;

            request.Status = SubscriptionRequestStatus.Approved;
            request.ResolvedAt = DateTime.UtcNow;
            request.ResolvedByUserId = adminUserId;

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            // The effective (previously center-redirected) subscription projection is cached by teacherId.
            try { await _subscriptionCache.InvalidateAsync(teacher.Id); } catch { /* cache best-effort */ }

            return Result<string>.Success("ok", _localizer, "IndependenceRequestApproved");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<string>.Failure(_localizer, "ServerError");
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> RejectAsync(long adminUserId, long requestId, RejectIndependenceRequestDto dto)
    {
        var request = await _unitOfWork.Centers.GetIndependenceRequestByIdAsync(requestId);
        if (request == null)
            return Result<string>.Failure(_localizer, "IndependenceRequestNotFound", HttpStatusCode.NotFound);
        if (request.Status != SubscriptionRequestStatus.Pending)
            return Result<string>.Failure(_localizer, "IndependenceRequestNotPending", HttpStatusCode.Conflict);

        request.Status = SubscriptionRequestStatus.Rejected;
        request.ResolvedAt = DateTime.UtcNow;
        request.ResolvedByUserId = adminUserId;
        request.RejectionReason = Clip(dto.RejectionReason);
        await _unitOfWork.SaveChangesAsync();

        return Result<string>.Success("ok", _localizer, "IndependenceRequestRejected");
    }

    private static string? Clip(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length > 500 ? s.Substring(0, 500) : s;
    }

    private static TeacherIndependenceRequestDto ToDto(TeacherIndependenceRequest r) => new()
    {
        RequestId = r.Id,
        Status = r.Status,
        RequestedAt = r.RequestedAt,
        ResolvedAt = r.ResolvedAt,
        Note = r.Note,
        RejectionReason = r.RejectionReason
    };
}
