using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Subscription;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Options;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Hangfire;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Super-admin subscription operations (§4.4 / FR-SUB-060…064).
///
/// MANUAL OVERRIDES (Activate / Extend / SetEndDate):
///   These bypass payment. The new TeacherSubscription row carries
///   PaymentChannel = SuperAdminOverride and AmountPaidEGP = 0.
///   CreatedByUserId records the admin user for audit (REQ-ADM-016 / FR-SUB-064).
///
/// PENDING QUEUE:
///   Approval delegates to ISubscriptionService.ConfirmPaymentAsync — one confirm
///   pipeline serves both webhook and manual-approval paths (§6.3).
///   Rejection enqueues IPendingPaymentRejectedNotificationJob to inform the tutor.
///
/// EC-24 GUARD on ApprovePendingAsync:
///   Refuses to approve when the teacher already has a CURRENT subscription
///   created within the last 24 hours — prevents accidental double-renewal when
///   the tutor paid via Paymob (auto-confirmed) and admin reviews a stale manual
///   submission for the same period.
/// </summary>
public class AdminSubscriptionService : IAdminSubscriptionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ISubscriptionCacheService _cache;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly SubscriptionDefaultsOptions _defaults;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ILogger<AdminSubscriptionService> _logger;
    private readonly IEncryptionService _encryption;

    public AdminSubscriptionService(IEncryptionService encryption,
        IUnitOfWork unitOfWork,
        ISubscriptionService subscriptionService,
        ISubscriptionCacheService cache,
        IBackgroundJobClient backgroundJobs,
        IOptions<SubscriptionDefaultsOptions> defaults,
        IStringLocalizer<Messages> localizer,
        ILogger<AdminSubscriptionService> logger)
    {
        _unitOfWork = unitOfWork;
        _subscriptionService = subscriptionService;
        _cache = cache;
        _backgroundJobs = backgroundJobs;
        _defaults = defaults.Value;
        _localizer = localizer;
        _logger = logger;
        _encryption = encryption;

    }

    // ════════════════════════════════════════════════
    // MANUAL OVERRIDES
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<CurrentSubscriptionDto>> ActivateAsync(
        long adminUserId, AdminActivateRequest request)
    {
        // ── Validation ──
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(request.TeacherId);
        if (teacher is null)
        {
            return Result<CurrentSubscriptionDto>.Failure(
                _localizer, SubscriptionConstants.Messages.TeacherNotFound, HttpStatusCode.NotFound);
        }

        DateTime now = DateTime.UtcNow;
        DateTime startDate = request.StartDate ?? now;
        DateTime endDate = request.EndDate ?? startDate.AddDays(_defaults.PeriodDays);

        if (endDate <= startDate)
        {
            return Result<CurrentSubscriptionDto>.Failure(
                _localizer, SubscriptionConstants.Messages.EndDateMustBeAfterStart);
        }

        // ── Build the override row ──
        var newSubscription = new TeacherSubscription
        {
            TeacherId = request.TeacherId,
            StartDate = startDate,
            EndDate = endDate,
            IsCurrent = true,
            PaymentMethod = PaymentMethod.SuperAdminManual,
            PaymentChannel = PaymentChannel.SuperAdminOverride,
            AmountPaidEGP = 0m,
            TransactionReference = null,
            EncryptedPaymentDetails = null,
            PaymentConfirmedAt = now,
            CreatedByUserId = adminUserId,
            CreateAt = now
        };

        // ── Atomically flip previous current + insert new (filtered unique index protects us) ──
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var previousCurrent = await _unitOfWork.Users
                .GetCurrentSubscriptionForUpdateAsync(request.TeacherId);

            await _unitOfWork.Users.FlipCurrentAndInsertNewAsync(previousCurrent, newSubscription);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        // ── Invalidate cache so the next request sees the new row ──
        await _cache.InvalidateAsync(request.TeacherId);

        return await BuildCurrentDtoResultAsync(
            request.TeacherId, SubscriptionConstants.Messages.SubscriptionActivated);
    }

    /// <inheritdoc />
    public async Task<Result<CurrentSubscriptionDto>> ExtendAsync(
        long adminUserId, AdminExtendRequest request)
    {
        // ── Validation ──
        if (request.ExtensionDays <= 0)
        {
            return Result<CurrentSubscriptionDto>.Failure(
                _localizer, SubscriptionConstants.Messages.ExtensionDaysMustBePositive);
        }

        // ── Mutate the current row's EndDate inside a transaction ──
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var currentSub = await _unitOfWork.Users
                .GetCurrentSubscriptionForUpdateAsync(request.TeacherId);

            if (currentSub is null)
            {
                await _unitOfWork.RollbackAsync();
                return Result<CurrentSubscriptionDto>.Failure(
                    _localizer, SubscriptionConstants.Messages.NoActiveSubscription, HttpStatusCode.NotFound);
            }

            currentSub.EndDate = currentSub.EndDate.AddDays(request.ExtensionDays);
            // Audit: stamp the admin user as the most recent modifier.
            currentSub.CreatedByUserId = adminUserId;

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        await _cache.InvalidateAsync(request.TeacherId);

        return await BuildCurrentDtoResultAsync(
            request.TeacherId, SubscriptionConstants.Messages.SubscriptionExtended);
    }

    /// <inheritdoc />
    public async Task<Result<CurrentSubscriptionDto>> SetEndDateAsync(
        long adminUserId, AdminSetEndDateRequest request)
    {
        // ── Load the row by id (any historical row may be the target — not just current) ──
        var subscription = await _unitOfWork.GetRepository<TeacherSubscription, long>()
            .GetByIdAsync(request.SubscriptionId);

        if (subscription is null)
        {
            return Result<CurrentSubscriptionDto>.Failure(
                _localizer, SubscriptionConstants.Messages.SubscriptionNotFound, HttpStatusCode.NotFound);
        }

        if (request.NewEndDate <= subscription.StartDate)
        {
            return Result<CurrentSubscriptionDto>.Failure(
                _localizer, SubscriptionConstants.Messages.EndDateMustBeAfterStart);
        }

        // ── Persist the override ──
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            subscription.EndDate = request.NewEndDate;
            subscription.CreatedByUserId = adminUserId;

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        // Cache invalidation only matters when the row touched IS the current row.
        if (subscription.IsCurrent)
        {
            await _cache.InvalidateAsync(subscription.TeacherId);
        }

        return await BuildCurrentDtoResultAsync(
            subscription.TeacherId, SubscriptionConstants.Messages.SubscriptionEndDateUpdated);
    }

    // ════════════════════════════════════════════════
    // PENDING PAYMENT QUEUE
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<AdminPendingQueueItemDto>>>> GetPendingQueueAsync(
        int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var (items, totalCount) = await _unitOfWork.SubscriptionPaymentsRepo
            .GetAdminQueuePagedAsync(page, pageSize);

        // Enrich each row with teacher identity. Single round-trip per teacher is
        // acceptable here — the admin queue is rarely deeper than a few dozen rows.
        var dtoList = new List<AdminPendingQueueItemDto>(items.Count);
        foreach (var pending in items)
        {
            var teacherInfo = await _unitOfWork.Users
                .GetTeacherForReminderAsync(pending.TeacherId);

            (string phoneNumber, string transactionRef) = DecryptSubmittedDetails(
                pending.EncryptedSubmittedDetails, pending.SubmittedTransactionReference);

            dtoList.Add(new AdminPendingQueueItemDto
            {
                Id = pending.Id,
                TeacherId = pending.TeacherId,
                TeacherName = teacherInfo?.FullName ?? string.Empty,
                TeacherCode = string.Empty, // populated below if available
                PaymentMethod = pending.PaymentMethod,
                PaymentChannel = pending.PaymentChannel,
                AmountEGP = pending.AmountEGP,
                PaymentPhoneNumber = phoneNumber,
                TransactionReference = transactionRef,
                InitiatedAt = pending.InitiatedAt
            });
        }

        var paged = new PaginatedResponse<List<AdminPendingQueueItemDto>>
        {
            data= dtoList,
            page = page,
            pageSize = pageSize,
            totalCount = totalCount
        };

        return Result<PaginatedResponse<List<AdminPendingQueueItemDto>>>.Success(paged, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<ConfirmPaymentResultDto>> ApprovePendingAsync(
        long adminUserId, long pendingPaymentId)
    {
        // ── Load the pending row ──
        var pending = await _unitOfWork.SubscriptionPaymentsRepo
            .GetByIdForAdminAsync(pendingPaymentId);

        if (pending is null)
        {
            return Result<ConfirmPaymentResultDto>.Failure(
                _localizer, SubscriptionConstants.Messages.PendingPaymentNotFound, HttpStatusCode.NotFound);
        }

        if (pending.Status != PendingPaymentStatus.AwaitingSuperAdminApproval)
        {
            return Result<ConfirmPaymentResultDto>.Failure(
                _localizer, SubscriptionConstants.Messages.PendingPaymentNotAwaitingApproval,
                HttpStatusCode.Conflict);
        }

        // ── EC-24 guard: refuse if a current sub was created within the last 24 hours ──
        var currentSub = await _unitOfWork.Users
            .GetCurrentSubscriptionStatusAsync(pending.TeacherId);

        if (currentSub is not null && IsRecentlyCreated(currentSub, pending.InitiatedAt))
        {
            _logger.LogWarning(
                "EC-24 duplicate-payment guard tripped for pending {PendingId}: current sub {SubId} created at {StartDate}",
                pendingPaymentId, currentSub.SubscriptionId, currentSub.StartDate);

            return Result<ConfirmPaymentResultDto>.Failure(
                _localizer, SubscriptionConstants.Messages.DuplicatePaymentDetected,
                HttpStatusCode.Conflict);
        }

        // ── Delegate to the shared confirm pipeline (§6.3) ──
        return await _subscriptionService.ConfirmPaymentAsync(pendingPaymentId, adminUserId);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> RejectPendingAsync(
        long adminUserId, long pendingPaymentId, string rejectionReason)
    {
        // ── Validation ──
        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            return Result<bool>.Failure(
                _localizer, SubscriptionConstants.Messages.RejectionReasonRequired);
        }

        // ── Load + state check ──
        var pending = await _unitOfWork.SubscriptionPaymentsRepo
            .GetByIdForAdminAsync(pendingPaymentId);

        if (pending is null)
        {
            return Result<bool>.Failure(
                _localizer, SubscriptionConstants.Messages.PendingPaymentNotFound, HttpStatusCode.NotFound);
        }

        if (pending.Status != PendingPaymentStatus.AwaitingSuperAdminApproval)
        {
            return Result<bool>.Failure(
                _localizer, SubscriptionConstants.Messages.PendingPaymentNotAwaitingApproval,
                HttpStatusCode.Conflict);
        }

        // ── Persist rejection ──
        string trimmedReason = rejectionReason.Trim();
        if (trimmedReason.Length > SubscriptionConstants.RejectionReasonMaxLength)
        {
            trimmedReason = trimmedReason[..SubscriptionConstants.RejectionReasonMaxLength];
        }

        pending.Status = PendingPaymentStatus.Rejected;
        pending.ResolvedAt = DateTime.UtcNow;
        pending.ResolvedByUserId = adminUserId;
        pending.RejectionReason = trimmedReason;

        _unitOfWork.SubscriptionPaymentsRepo.UpdatePending(pending);
        await _unitOfWork.SaveChangesAsync();

        // ── Fire the rejection notification (push + WhatsApp + UserNotification) ──
        _backgroundJobs.Enqueue<IPendingPaymentRejectedNotificationJob>(
            job => job.SendAsync(pending.TeacherId, pending.Id, trimmedReason));

        return Result<bool>.Success(true, _localizer, SubscriptionConstants.Messages.PendingPaymentRejected);
    }

    // ════════════════════════════════════════════════
    // PRICING (REQ-ADM-016)
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<bool>> UpdatePackagePriceAsync(
        long adminUserId, long packageId, decimal newMonthlyPriceEGP)
    {
        if (newMonthlyPriceEGP < 0m)
        {
            return Result<bool>.Failure(
                _localizer, SubscriptionConstants.Messages.PriceMustBeNonNegative);
        }

        var package = await _unitOfWork.GetRepository<StudentCapacityPackage, long>()
            .GetByIdAsync(packageId);

        if (package is null)
        {
            return Result<bool>.Failure(
                _localizer, SubscriptionConstants.Messages.PackageNotFound, HttpStatusCode.NotFound);
        }

        package.MonthlyPriceEGP = newMonthlyPriceEGP;
        package.PriceUpdatedAt = DateTime.UtcNow;
        package.PriceUpdatedByUserId = adminUserId;

        await _unitOfWork.Users.UpdateCapacityPackagePriceAsync(package);
        await _unitOfWork.SaveChangesAsync();

        // BR-SUB-009: in-flight pending payments retain their initiation-time price snapshot.
        // No mass-update of pending rows here.

        return Result<bool>.Success(true, _localizer, SubscriptionConstants.Messages.PackagePriceUpdated);
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    /// <summary>
    /// Reads the post-mutation current subscription and wraps it in a
    /// CurrentSubscriptionDto Result. Used by all three manual-override paths so
    /// they return the same shape as the teacher-facing GetCurrentAsync.
    /// </summary>
    private async Task<Result<CurrentSubscriptionDto>> BuildCurrentDtoResultAsync(
        long teacherId, string successMessageKey)
    {
        var freshResult = await _subscriptionService.GetCurrentAsync(teacherId);
        if (!freshResult.IsSuccess || freshResult.Data is null)
        {
            // Defensive — should never happen since we just inserted/updated a row.
            return Result<CurrentSubscriptionDto>.Failure(
                _localizer, SubscriptionConstants.Messages.SubscriptionNotFound, HttpStatusCode.NotFound);
        }

        return Result<CurrentSubscriptionDto>.Success(
            freshResult.Data, _localizer, successMessageKey);
    }

    /// <summary>
    /// EC-24: returns true if the current subscription's StartDate is within the
    /// last <see cref="SubscriptionConstants.DuplicatePaymentGuardHours"/> hours
    /// relative to the supplied <paramref name="pendingInitiatedAt"/>.
    /// </summary>
    private static bool IsRecentlyCreated(
        Domain.Interfaces.CurrentSubscriptionStatusProjection currentSub,
        DateTime pendingInitiatedAt)
    {
        TimeSpan since = pendingInitiatedAt - currentSub.StartDate;
        return since.TotalHours >= 0 && since.TotalHours < SubscriptionConstants.DuplicatePaymentGuardHours;
    }

    /// <summary>
    /// Decrypts the manual-submit payload (BR-SUB-011 admin-only view).
    /// EncryptedSubmittedDetails has the format "phone:{number};ref:{txid}".
    /// On decrypt failure (corrupt blob, key rotation), returns ("", trimmed-reference).
    /// </summary>
    private (string PhoneNumber, string TransactionRef) DecryptSubmittedDetails(
          string? encryptedBlob, string? plainReferenceFallback)
    {
        if (string.IsNullOrEmpty(encryptedBlob))
        {
            return (string.Empty, plainReferenceFallback ?? string.Empty);
        }

        try
        {
            // Format produced by SubscriptionService.SubmitManualAsync:
            //   "phone:{paymentPhone};ref:{transactionReference}"
            string plain = _encryption.Decrypt(encryptedBlob);

            string phone = ExtractField(plain, "phone:");
            string txRef = ExtractField(plain, "ref:");

            return (
                phone,
                string.IsNullOrEmpty(txRef) ? (plainReferenceFallback ?? string.Empty) : txRef);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt pending-payment submitted details");
            return (string.Empty, plainReferenceFallback ?? string.Empty);
        }
    }

    private static string ExtractField(string blob, string prefix)
    {
        int start = blob.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0) return string.Empty;

        start += prefix.Length;
        int end = blob.IndexOf(';', start);
        return end < 0 ? blob[start..].Trim() : blob[start..end].Trim();
    }

}