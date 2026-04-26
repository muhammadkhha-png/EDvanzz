using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Subscription;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Options;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Helpers;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Teacher-facing subscription operations (§4.1 / §6.3 / §6.4).
///
/// CRITICAL FLOW — ConfirmPaymentAsync:
///   This service owns the §6.3 confirm pipeline shared by the Paymob webhook
///   and the admin manual-approval path. The flow:
///     (1) Capture paymentConfirmedAt = UtcNow ONCE (BR-SUB-013).
///     (2) Begin Serializable transaction.
///     (3) Pessimistic-lock currentSub via UPDLOCK + HOLDLOCK.
///     (4) Compute new StartDate per §6.2.
///     (5) Flip previous IsCurrent = false, insert new IsCurrent = true.
///     (6) Persist pending row resolution.
///     (7) SaveChanges — RowVersion concurrency check fires here.
///     (8) Commit.
///     (9) Synchronously invalidate Redis cache.
///     (10) Enqueue IRenewalNotificationJob fire-and-forget.
///   Bounded retry (max 2) on DbUpdateConcurrencyException or unique-violation.
///
/// IDEMPOTENCY:
///   Every confirm path is idempotent. A second call with a pending row already in
///   Status = Confirmed returns Success with WasIdempotentReplay = true (EC-03).
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ISubscriptionCacheService _cache;
    private readonly IEncryptionService _encryption;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly SubscriptionDefaultsOptions _defaults;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        IUnitOfWork unitOfWork,
        IPaymentGateway paymentGateway,
        ISubscriptionCacheService cache,
        IEncryptionService encryption,
        IBackgroundJobClient backgroundJobs,
        IOptions<SubscriptionDefaultsOptions> defaults,
        IStringLocalizer<Messages> localizer,
        ILogger<SubscriptionService> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentGateway = paymentGateway;
        _cache = cache;
        _encryption = encryption;
        _backgroundJobs = backgroundJobs;
        _defaults = defaults.Value;
        _localizer = localizer;
        _logger = logger;
    }

    // ════════════════════════════════════════════════
    // QUERY: GET CURRENT SUBSCRIPTION (FR-SUB-003)
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<CurrentSubscriptionDto>> GetCurrentAsync(long teacherId)
    {
        // Status is DERIVED at read time per D-08 — never stored.
        var projection = await _unitOfWork.Users.GetCurrentSubscriptionStatusAsync(teacherId);

        if (projection is null)
        {
            return Result<CurrentSubscriptionDto>.Failure(
                _localizer, SubscriptionConstants.Messages.NoActiveSubscription, HttpStatusCode.NotFound);
        }

        // Load the renewal price separately — it lives on StudentCapacityPackage,
        // not on the subscription row (BR-SUB-009).
        decimal renewalAmount = await ResolveCurrentPackagePriceAsync(projection.StudentCapacityPackageId);

        // SubscriptionStatusCalculator.Derive expects a TeacherSubscription instance.
        // Build a transient one from the projection's two dates — IsCurrent = true so
        // Derive returns Active/ExpiringSoon/Expired (not Historical).
        var subForStatus = new TeacherSubscription
        {
            StartDate = projection.StartDate,
            EndDate = projection.EndDate,
            IsCurrent = true
        };

        var dto = new CurrentSubscriptionDto
        {
            Id = projection.SubscriptionId,
            StartDate = projection.StartDate,
            EndDate = projection.EndDate,
            DaysRemaining = ComputeDaysRemaining(projection.EndDate),
            Status = SubscriptionStatusCalculator.Derive(subForStatus, projection.EndDate),
            RenewalAmountEGP = renewalAmount
        };

        return Result<CurrentSubscriptionDto>.Success(dto, _localizer);
    }

    // ════════════════════════════════════════════════
    // QUERY: PAYMENT HISTORY (FR-SUB-039)
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<SubscriptionHistoryItemDto>>>> GetHistoryPagedAsync(
        long teacherId, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var allRows = await _unitOfWork.Users.GetAllSubscriptionsByTeacherIdAsync(teacherId);

        // The repo returns the unpaged list; paginate in memory because a teacher
        // typically has <50 historical rows — flagged in Phase 03 review.
        int totalCount = allRows.Count;
        var pageRows = allRows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SubscriptionHistoryItemDto
            {
                Id = s.Id,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                AmountPaidEGP = s.AmountPaidEGP,
                PaymentMethod = s.PaymentMethod,
                PaymentChannel = s.PaymentChannel,
                MaskedTransactionReference = MaskReference(s.TransactionReference),
                PaymentConfirmedAt = s.PaymentConfirmedAt ,
            })
            .ToList();

        var paged = new PaginatedResponse<List<SubscriptionHistoryItemDto>>
        {
            data = pageRows,
            page = page,
            pageSize = pageSize,
            totalCount = totalCount
        };

        return Result<PaginatedResponse<List<SubscriptionHistoryItemDto>>>.Success(paged, _localizer);
    }

    // ════════════════════════════════════════════════
    // COMMAND: INITIATE RENEWAL (§6.4 / FR-SUB-030)
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<RenewInitiateResponse>> InitiateRenewalAsync(
        long teacherId, RenewInitiateRequest request)
    {
        // ── Validation ──
        if (request.PaymentMethod == PaymentMethod.SuperAdminManual)
        {
            return Result<RenewInitiateResponse>.Failure(
                _localizer, SubscriptionConstants.Messages.SuperAdminMethodNotAllowed);
        }

        if (await _unitOfWork.SubscriptionPaymentsRepo.HasInFlightPaymentAsync(teacherId))
        {
            return Result<RenewInitiateResponse>.Failure(
                _localizer, SubscriptionConstants.Messages.PendingPaymentAlreadyInFlight,
                HttpStatusCode.Conflict);
        }

        // ── Resolve the price from the teacher's package (BR-SUB-009) ──
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is null)
        {
            return Result<RenewInitiateResponse>.Failure(
                _localizer, SubscriptionConstants.Messages.TeacherNotFound, HttpStatusCode.NotFound);
        }

        if (teacher.StudentCapacityPackageId is null)
        {
            return Result<RenewInitiateResponse>.Failure(
                _localizer, SubscriptionConstants.Messages.CapacityPackageNotAssigned);
        }

        var package = await _unitOfWork.GetRepository<StudentCapacityPackage, long>()
            .GetByIdAsync(teacher.StudentCapacityPackageId.Value);

        if (package is null || package.MonthlyPriceEGP <= 0m)
        {
            return Result<RenewInitiateResponse>.Failure(
                _localizer, SubscriptionConstants.Messages.CapacityPackagePriceNotSet);
        }

        decimal amountEGP = package.MonthlyPriceEGP;

        // ── Persist a fresh pending row in Status = Initiated ──
        var pending = new PendingSubscriptionPayment
        {
            TeacherId = teacherId,
            PaymentMethod = request.PaymentMethod,
            PaymentChannel = request.PaymentChannel,
            Status = PendingPaymentStatus.Initiated,
            AmountEGP = amountEGP,
            InitiatedAt = DateTime.UtcNow,
            CreateAt = DateTime.UtcNow
        };

        await _unitOfWork.SubscriptionPaymentsRepo.AddAsync(pending);
        await _unitOfWork.SaveChangesAsync();

        // ── Decide between Paymob and manual flows ──
        // D-04: when Paymob is stubbed off (PaymobOptions.Enabled = false),
        // CreateSessionAsync returns Success = false and we fall through to manual.
        if (request.PaymentChannel == PaymentChannel.Paymob)
        {
            var session = await _paymentGateway.CreateSessionAsync(
                pending.Id, amountEGP, BuildGatewayDescription(teacher));

            if (session.Success)
            {
                pending.PaymobSessionId = session.SessionId;
                _unitOfWork.SubscriptionPaymentsRepo.UpdatePending(pending);
                await _unitOfWork.SaveChangesAsync();

                var paymobPayload = new PaymobInitiatePayload
                {
                    PendingPaymentId = pending.Id,
                    IframeUrl = session.IframeUrl!,
                    SessionId = session.SessionId!
                };

                return Result<RenewInitiateResponse>.Success(
                    new RenewInitiateResponse { Mode = "paymob", Paymob = paymobPayload },
                    _localizer,
                    SubscriptionConstants.Messages.SubscriptionRenewalInitiated);
            }

            // Paymob failure / stub mode — fall through to manual flow below.
            _logger.LogInformation(
                "Paymob unavailable (code {Code}) — falling back to manual for pending {PendingId}",
                session.ErrorCode, pending.Id);
        }

        // ── Manual flow ──
        var manualPayload = BuildManualPayload(pending.Id, amountEGP, request.PaymentMethod);

        return Result<RenewInitiateResponse>.Success(
            new RenewInitiateResponse { Mode = "manual", Manual = manualPayload },
            _localizer,
            SubscriptionConstants.Messages.SubscriptionRenewalInitiated);
    }

    // ════════════════════════════════════════════════
    // COMMAND: SUBMIT MANUAL DETAILS (FR-SUB-033)
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<RenewStatusDto>> SubmitManualAsync(
        long teacherId, ManualSubmitRequest request)
    {
        // ── Validation ──
        if (string.IsNullOrWhiteSpace(request.TransactionReference))
        {
            return Result<RenewStatusDto>.Failure(
                _localizer, SubscriptionConstants.Messages.TransactionReferenceRequired);
        }

        if (string.IsNullOrWhiteSpace(request.PaymentPhoneNumber))
        {
            return Result<RenewStatusDto>.Failure(
                _localizer, SubscriptionConstants.Messages.PaymentPhoneRequired);
        }

        // ── Tenant-guarded fetch ──
        var pending = await _unitOfWork.SubscriptionPaymentsRepo
            .GetByIdAndTeacherAsync(request.PendingPaymentId, teacherId);

        if (pending is null)
        {
            return Result<RenewStatusDto>.Failure(
                _localizer, SubscriptionConstants.Messages.PendingPaymentNotFound, HttpStatusCode.NotFound);
        }

        if (pending.Status != PendingPaymentStatus.Initiated)
        {
            return Result<RenewStatusDto>.Failure(
                _localizer, SubscriptionConstants.Messages.PendingPaymentNotInInitiated,
                HttpStatusCode.Conflict);
        }

        // ── Encrypt the submitted details (REQ-SUB-NFR-004) ──
        var sensitiveBlob =
            $"phone:{request.PaymentPhoneNumber.Trim()};ref:{request.TransactionReference.Trim()}";

        pending.EncryptedSubmittedDetails = _encryption.Encrypt(sensitiveBlob);
        pending.SubmittedTransactionReference = request.TransactionReference.Trim();
        pending.Status = PendingPaymentStatus.AwaitingSuperAdminApproval;
        _unitOfWork.SubscriptionPaymentsRepo.UpdatePending(pending);
        await _unitOfWork.SaveChangesAsync();

        return Result<RenewStatusDto>.Success(
            ToRenewStatusDto(pending),
            _localizer,
            SubscriptionConstants.Messages.ManualSubmissionRecorded);
    }

    // ════════════════════════════════════════════════
    // QUERY: POLL RENEWAL STATUS
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<RenewStatusDto>> GetRenewalStatusAsync(
        long teacherId, long pendingPaymentId)
    {
        var pending = await _unitOfWork.SubscriptionPaymentsRepo
            .GetByIdAndTeacherAsync(pendingPaymentId, teacherId);

        if (pending is null)
        {
            return Result<RenewStatusDto>.Failure(
                _localizer, SubscriptionConstants.Messages.PendingPaymentNotFound, HttpStatusCode.NotFound);
        }

        return Result<RenewStatusDto>.Success(ToRenewStatusDto(pending), _localizer);
    }

    // ════════════════════════════════════════════════
    // CRITICAL: CONFIRM PAYMENT (§6.3 / §6.6)
    // ════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<ConfirmPaymentResultDto>> ConfirmPaymentAsync(
        long pendingPaymentId, long? resolvedByUserId)
    {
        // ── Pre-transaction load + idempotency short-circuit (EC-03) ──
        var pending = resolvedByUserId.HasValue
            ? await _unitOfWork.SubscriptionPaymentsRepo.GetByIdForAdminAsync(pendingPaymentId)
            : await _unitOfWork.SubscriptionPaymentsRepo.GetByIdForAdminAsync(pendingPaymentId);
        // Webhook path also uses GetByIdForAdminAsync — webhook trusts the gateway's
        // session id resolution upstream and does not have a teacher tenant context.

        if (pending is null)
        {
            return Result<ConfirmPaymentResultDto>.Failure(
                _localizer, SubscriptionConstants.Messages.PendingPaymentNotFound, HttpStatusCode.NotFound);
        }

        if (pending.Status == PendingPaymentStatus.Confirmed)
        {
            // Idempotent replay (EC-03). Return the existing subscription row info.
            var existingProjection = await _unitOfWork.Users
                .GetCurrentSubscriptionStatusAsync(pending.TeacherId);

            return Result<ConfirmPaymentResultDto>.Success(new ConfirmPaymentResultDto
            {
                SubscriptionId = existingProjection?.SubscriptionId ?? 0,
                StartDate = existingProjection?.StartDate ?? DateTime.MinValue,
                EndDate = existingProjection?.EndDate ?? DateTime.MinValue,
                WasIdempotentReplay = true
            }, _localizer, SubscriptionConstants.Messages.PaymentAlreadyConfirmed);
        }

        // ── §6.3 step 1: capture paymentConfirmedAt ONCE (BR-SUB-013) ──
        DateTime paymentConfirmedAt = DateTime.UtcNow;

        // ── Bounded retry on concurrency conflict (§6.6) ──
        for (int attempt = 1; attempt <= SubscriptionConstants.MaxConcurrencyRetries + 1; attempt++)
        {
            try
            {
                var result = await ConfirmPaymentOnceAsync(pending, paymentConfirmedAt, resolvedByUserId);
                return result;
            }
            catch (DbUpdateConcurrencyException) when (attempt <= SubscriptionConstants.MaxConcurrencyRetries)
            {
                _logger.LogWarning(
                    "Concurrency conflict on pending {PendingId} attempt {Attempt} — retrying",
                    pendingPaymentId, attempt);

                // Reset the entity so the next attempt re-reads fresh state.
                _unitOfWork.SubscriptionPaymentsRepo.DetachPending(pending);
                pending = await _unitOfWork.SubscriptionPaymentsRepo.GetByIdForAdminAsync(pendingPaymentId)
                          ?? throw new InvalidOperationException("Pending row vanished mid-retry");
            }
            catch (DbUpdateException ex)
                when (IsUniqueViolation(ex) && attempt <= SubscriptionConstants.MaxConcurrencyRetries)
            {
                // Filtered unique index IX_TeacherSubscriptions_Current was violated —
                // a competing transaction beat us to flipping IsCurrent. Retry.
                _logger.LogWarning(
                    "Unique-violation on IsCurrent index for pending {PendingId} attempt {Attempt} — retrying",
                    pendingPaymentId, attempt);
                _unitOfWork.SubscriptionPaymentsRepo.DetachPending(pending);
           
                pending = await _unitOfWork.SubscriptionPaymentsRepo.GetByIdForAdminAsync(pendingPaymentId)
                          ?? throw new InvalidOperationException("Pending row vanished mid-retry");
            }
        }

        // Exhausted retries.
        _logger.LogError("ConfirmPaymentAsync exhausted retries for pending {PendingId}", pendingPaymentId);
        return Result<ConfirmPaymentResultDto>.Failure(
            _localizer, SubscriptionConstants.Messages.ConcurrentRenewalDetected,
            HttpStatusCode.Conflict);
    }

    // ════════════════════════════════════════════════
    // PRIVATE: SINGLE CONFIRM ATTEMPT (§6.3 steps 2–10)
    // ════════════════════════════════════════════════

    private async Task<Result<ConfirmPaymentResultDto>> ConfirmPaymentOnceAsync(
        PendingSubscriptionPayment pending,
        DateTime paymentConfirmedAt,
        long? resolvedByUserId)
    {
        // ── §6.3 step 2: Serializable transaction ──
        await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            // ── §6.3 step 3: pessimistic-lock the current subscription ──
            var currentSub = await _unitOfWork.Users
                .GetCurrentSubscriptionForUpdateAsync(pending.TeacherId);

            // ── §6.3 step 4: compute new StartDate per §6.2 ──
            DateTime newStart = ComputeRenewalStartDate(currentSub, paymentConfirmedAt);
            DateTime newEnd = newStart.AddDays(_defaults.PeriodDays);

            var newSubscription = new TeacherSubscription
            {
                TeacherId = pending.TeacherId,
                StartDate = newStart,
                EndDate = newEnd,
                IsCurrent = true,
                PaymentMethod = pending.PaymentMethod,
                PaymentChannel = pending.PaymentChannel,
                AmountPaidEGP = pending.AmountEGP,
                TransactionReference = pending.SubmittedTransactionReference,
                EncryptedPaymentDetails = pending.EncryptedSubmittedDetails,
                PaymentConfirmedAt = paymentConfirmedAt,
                CreatedByUserId = resolvedByUserId,
                CreateAt = DateTime.UtcNow
            };

            // ── §6.3 step 5: flip previous IsCurrent + insert new ──
            await _unitOfWork.Users.FlipCurrentAndInsertNewAsync(currentSub, newSubscription);
            pending.Status = PendingPaymentStatus.Confirmed;
            pending.ResolvedAt = paymentConfirmedAt;
            pending.ResolvedByUserId = resolvedByUserId;
            _unitOfWork.SubscriptionPaymentsRepo.UpdatePending(pending);

            // ── §6.3 step 7: SaveChanges — RowVersion check fires here ──
            await _unitOfWork.SaveChangesAsync();

            // ── §6.3 step 8: Commit ──
            await _unitOfWork.CommitAsync();

            // ── §6.3 step 9: synchronously invalidate the cache (BEFORE enqueue) ──
            // Order matters: invalidate first so any racing read after this point
            // hits the DB and sees the new row. Enqueue second — if the cache
            // invalidation fails, we still want the notification to fire.
            await _cache.InvalidateAsync(pending.TeacherId);

            // ── §6.3 step 10: fire-and-forget notification ──
            _backgroundJobs.Enqueue<IRenewalNotificationJob>(
                job => job.SendAsync(pending.TeacherId, newSubscription.Id));

            return Result<ConfirmPaymentResultDto>.Success(new ConfirmPaymentResultDto
            {
                SubscriptionId = newSubscription.Id,
                StartDate = newStart,
                EndDate = newEnd,
                WasIdempotentReplay = false
            }, _localizer, SubscriptionConstants.Messages.PaymentConfirmed);
        }
        catch
        {
            // Roll back; let the outer retry loop catch concurrency exceptions.
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    // ════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════

    /// <summary>
    /// §6.2 StartDate rule:
    ///   - Early renewal (currentSub still active): newStart = currentSub.EndDate.
    ///   - Late or first renewal (currentSub null/expired): newStart = paymentConfirmedAt.
    /// </summary>
    private static DateTime ComputeRenewalStartDate(
        TeacherSubscription? currentSub, DateTime paymentConfirmedAt)
    {
        if (currentSub is null) return paymentConfirmedAt;

        // If the current subscription's EndDate is in the future, we're an early
        // renewal — chain on top of it. Otherwise, start fresh from confirmation.
        return currentSub.EndDate > paymentConfirmedAt
            ? currentSub.EndDate
            : paymentConfirmedAt;
    }

    private async Task<decimal> ResolveCurrentPackagePriceAsync(long? packageId)
    {
        if (packageId is null) return 0m;

        var package = await _unitOfWork.GetRepository<StudentCapacityPackage, long>()
            .GetByIdAsync(packageId.Value);

        return package?.MonthlyPriceEGP ?? 0m;
    }

    private static RenewStatusDto ToRenewStatusDto(PendingSubscriptionPayment pending) => new()
    {
        PendingPaymentId = pending.Id,
        Status = pending.Status,
        InitiatedAt = pending.InitiatedAt,
        ResolvedAt = pending.ResolvedAt,
        RejectionReason = pending.RejectionReason
    };

    /// <summary>
    /// Masks the trailing portion of a transaction reference for tutor-facing payment history (BR-SUB-011).
    /// "PMB123456789" → "PMB****6789".
    /// </summary>
    private static string? MaskReference(string? reference)
    {
        if (string.IsNullOrEmpty(reference)) return null;
        if (reference.Length <= 7) return reference; // too short to mask meaningfully

        return string.Concat(
            reference.AsSpan(0, 3),
            "****",
            reference.AsSpan(reference.Length - 4));
    }

    private string BuildManualPayLine(decimal amountEGP, PaymentMethod method)
    {
        string template = method switch
        {
            PaymentMethod.VodafoneCash => _localizer[
                SubscriptionConstants.Messages.ManualPayInstructionsVodafoneCash],
            PaymentMethod.InstaPay => _localizer[
                SubscriptionConstants.Messages.ManualPayInstructionsInstaPay],
            _ => string.Empty
        };

        string payTo = method switch
        {
            PaymentMethod.VodafoneCash => _defaults.ManualPayToVodafoneCash,
            PaymentMethod.InstaPay => _defaults.ManualPayToInstaPay,
            _ => string.Empty
        };

        return string.Format(template, amountEGP, payTo);
    }

    private ManualInitiatePayload BuildManualPayload(
        long pendingPaymentId, decimal amountEGP, PaymentMethod method)
    {
        string payTo = method switch
        {
            PaymentMethod.VodafoneCash => _defaults.ManualPayToVodafoneCash,
            PaymentMethod.InstaPay => _defaults.ManualPayToInstaPay,
            _ => string.Empty
        };

        return new ManualInitiatePayload
        {
            PendingPaymentId = pendingPaymentId,
            PayToNumber = payTo,
            Instructions = BuildManualPayLine(amountEGP, method),
            AmountEGP = amountEGP
        };
    }

    private static string BuildGatewayDescription(Teacher teacher) =>
        $"Edvanz subscription renewal — teacher {teacher.TeacherCode}";

    /// <summary>
    /// Detects SQL Server unique-key violation (errors 2601/2627) inside DbUpdateException.
    /// Specifically used to recognize a race-loss on the IX_TeacherSubscriptions_Current
    /// filtered unique index.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var sqlException = ex.InnerException as Microsoft.Data.SqlClient.SqlException
                           ?? ex.GetBaseException() as Microsoft.Data.SqlClient.SqlException;

        return sqlException is { Number: 2601 or 2627 };
    }
    /// <summary>
    /// Days remaining until <paramref name="endDateUtc"/>, clamped to zero on
    /// the day-of-expiry and beyond. Used by GetCurrentAsync — the helper class
    /// SubscriptionStatusCalculator does not expose a DaysRemaining method, so
    /// the calculation lives here next to the only consumer.
    /// </summary>
    private static int ComputeDaysRemaining(DateTime endDateUtc)
    {
        TimeSpan delta = endDateUtc - DateTime.UtcNow;
        if (delta <= TimeSpan.Zero) return 0;
        return (int)Math.Ceiling(delta.TotalDays);
    }
}