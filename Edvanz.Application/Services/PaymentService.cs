using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Payment;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements all Payment Module (Module 4) operations.
///
/// ARCHITECTURAL NOTES:
/// - All write operations use the ownsTransaction pattern for nested call safety.
/// - Counter/wallet updates use RowVersion concurrency with retry loop (MaxConcurrencyRetries).
/// - All queries go through IPaymentRepo named methods — no raw expressions.
/// - Denormalized fields populated on every record creation for post-delete/purge survival.
///
/// REQ-PAY-001 through REQ-PAY-092 coverage:
/// - Collection: CollectPaymentAsync (REQ-PAY-001-025)
/// - Edit/Delete: EditPaymentAsync, DeletePaymentAsync (REQ-PAY-027, BR-PAY-002)
/// - Unpaid: GetUnpaidStudentsAsync (REQ-PAY-028-033)
/// - Wallet: GetAllWalletsAsync, ResetWalletAsync (REQ-PAY-034-038)
/// - Dashboard: GetDashboardAsync (REQ-PAY-039-044)
/// - Departure: GetDepartureSummaryAsync, ConfirmDepartureAsync (REQ-PAY-066-075)
/// - Transfer: GetTransferSummaryAsync, ConfirmTransferAsync (REQ-PAY-085-092)
/// - Sync: SyncOfflinePaymentsAsync (REQ-PAY-076-084)
/// - Integration: OnStudentAssigned/Unassigned, OnSessionDeleting, OnStudentPurged
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;
    private readonly ITimeZoneService _timeZoneService;
    private readonly IPaymentNotifier _paymentNotifier;              // ← Phase 4
    private readonly ILogger<PaymentService> _logger;          // ← strengthen batch exception handling

    public PaymentService(
        IUnitOfWork unitOfWork,
        IStringLocalizer<Domain.Resources.Messages> localizer,
        ITimeZoneService timeZoneService,
        IPaymentNotifier paymentNotifier, ILogger<PaymentService> logger)                            // ← Phase 4
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _timeZoneService = timeZoneService;
        _paymentNotifier = paymentNotifier;
        _logger = logger;// ← Phase 4
    }

    // ══════════════════════════════════════════════
    // PAYMENT COLLECTION
    // ══════════════════════════════════════════════
    // ══════════════════════════════════════════════
    // BATCH COLLECTION (UI: "Mark N students as Paid")
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<BatchCollectResultDto>> BatchCollectPaymentAsync(
        BatchCollectPaymentDto dto, long teacherId, long collectedByUserId)
    {
        if (dto.Items is null || dto.Items.Count == 0)
            return Result<BatchCollectResultDto>.Failure(
                _localizer, PaymentConstants.Messages.PaymentBatchEmpty, HttpStatusCode.BadRequest);

        var results = new List<BatchCollectItemResultDto>(dto.Items.Count);

        foreach (var item in dto.Items)
        {
            // Fan the shared batch fields + JWT-resolved identity onto a single-collect DTO.
            var itemDto = new CollectPaymentDto
            {
                TeacherId = teacherId,
                CollectedByUserId = collectedByUserId,
                SessionId = dto.SessionId,
                PaymentMethod = dto.PaymentMethod,
                TeacherStudentId = item.TeacherStudentId,
                Amount = item.Amount,
                OnlineTransactionRef = item.OnlineTransactionRef,
                DuplicateConfirmed = dto.ConfirmAllDuplicates,
                AlreadyPaidConfirmed = dto.ConfirmAllAlreadyPaid
            };

            // Each item runs as its own owned transaction inside CollectPaymentAsync:
            // best-effort — one student's failure never rolls back the others, and the
            // existing per-student notification behavior is preserved unchanged.
            try
            {
                var itemResult = await CollectPaymentAsync(itemDto);
                results.Add(MapBatchItemResult(item.TeacherStudentId, itemResult));
            }
            catch (Exception)
            {
                // CollectPaymentAsync rethrows genuinely exceptional (non-business) errors.
                // Capture as a per-item failure so one bad record can't abort the batch.
                // NOTE: no ILogger is injected into PaymentService; if one is added, log here.
                results.Add(new BatchCollectItemResultDto
                {
                    TeacherStudentId = item.TeacherStudentId,
                    Status = BatchCollectItemStatus.Failed,
                    Message = _localizer["ServerError"] // existing global resource key
                });
            }
        }

        var envelope = new BatchCollectResultDto
        {
            TotalRequested = dto.Items.Count,
            CollectedCount = results.Count(r => r.Status == BatchCollectItemStatus.Collected),
            NeedsConfirmationCount = results.Count(r => r.Status == BatchCollectItemStatus.NeedsConfirmation),
            FailedCount = results.Count(r => r.Status == BatchCollectItemStatus.Failed),
            Results = results
        };

        return Result<BatchCollectResultDto>.Success(
            envelope, _localizer, PaymentConstants.Messages.PaymentBatchCollectedSuccess);
    }

    /// <summary>
    /// Classifies a single-collect result into a batch item outcome.
    /// result.Message is already localized by CollectPaymentAsync, so no localizer is needed.
    /// </summary>
    private static BatchCollectItemResultDto MapBatchItemResult(
        long teacherStudentId, Result<CollectPaymentResultDto> result)
    {
        if (!result.IsSuccess)
            return new BatchCollectItemResultDto
            {
                TeacherStudentId = teacherStudentId,
                Status = BatchCollectItemStatus.Failed,
                Message = result.Message
            };

        var data = result.Data;

        if (data?.Transaction is not null)
            return new BatchCollectItemResultDto
            {
                TeacherStudentId = teacherStudentId,
                Status = BatchCollectItemStatus.Collected,
                Message = result.Message,
                Transaction = data.Transaction
            };

        if (data is not null && (data.IsSameDayDuplicate || data.IsAlreadyPaid))
            return new BatchCollectItemResultDto
            {
                TeacherStudentId = teacherStudentId,
                Status = BatchCollectItemStatus.NeedsConfirmation,
                Message = result.Message,
                IsSameDayDuplicate = data.IsSameDayDuplicate,
                IsAlreadyPaid = data.IsAlreadyPaid,
                TodayPaidAmount = data.TodayPaidAmount
            };

        // Defensive: success with neither a transaction nor a known warning flag.
        return new BatchCollectItemResultDto
        {
            TeacherStudentId = teacherStudentId,
            Status = BatchCollectItemStatus.Failed,
            Message = result.Message
        };
    }

    /// <inheritdoc />
    public async Task<Result<CollectPaymentResultDto>> CollectPaymentAsync(CollectPaymentDto dto)
    {
        // Default CollectedByUserId to TeacherId for audit trail
        dto.CollectedByUserId ??= dto.TeacherId;

        // 1. Validate teacher
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(dto.TeacherId);
        if (teacher is null)
            return Result<CollectPaymentResultDto>.Failure(
                _localizer, PaymentConstants.Messages.TeacherNotFound, HttpStatusCode.NotFound);

        // 2. Validate session
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionId, dto.TeacherId);
        if (session is null)
            return Result<CollectPaymentResultDto>.Failure(
                _localizer, PaymentConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        // 3. Validate student
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
            dto.TeacherStudentId, dto.TeacherId);
        if (student is null)
        {
            var deletedStudent = await _unitOfWork.Students.GetByIdAndTeacherIgnoreFiltersAsync(
                dto.TeacherStudentId, dto.TeacherId);
            if (deletedStudent is not null && deletedStudent.IsDeleted)
                return Result<CollectPaymentResultDto>.Failure(
                    _localizer, PaymentConstants.Messages.PaymentStudentInRecycleBin,
                    HttpStatusCode.BadRequest);

            return Result<CollectPaymentResultDto>.Failure(
                _localizer, PaymentConstants.Messages.StudentNotFound, HttpStatusCode.NotFound);
        }

        // 4. Validate student is assigned to session
        var activeAssignment = await _unitOfWork.AttendanceRepo.GetActiveAssignmentAsync(dto.TeacherStudentId);
        if (activeAssignment is null || activeAssignment.SessionId != dto.SessionId)
            return Result<CollectPaymentResultDto>.Failure(
                _localizer, PaymentConstants.Messages.PaymentStudentNotAssigned, HttpStatusCode.BadRequest);

        // 5. Validate amount
        if (dto.Amount <= 0)
            return Result<CollectPaymentResultDto>.Failure(
                _localizer, PaymentConstants.Messages.PaymentAmountInvalid, HttpStatusCode.BadRequest);

        var localDate = _timeZoneService.GetTeacherLocalDate(dto.TeacherId);

        // 6. REQ-PAY-020: Same-day duplicate detection
        if (!dto.DuplicateConfirmed)
        {
            var sameDayTransactions = await _unitOfWork.PaymentsRepo
                .GetSameDayTransactionsAsync(dto.TeacherId, dto.TeacherStudentId, localDate);
            if (sameDayTransactions.Count > 0)
            {
                var todayTotal = sameDayTransactions.Sum(t => t.AmountPaid);
                return Result<CollectPaymentResultDto>.Success(new CollectPaymentResultDto
                {
                    Transaction = null,
                    IsSameDayDuplicate = true,
                    TodayPaidAmount = todayTotal,
                    TodayPaidSessionName = sameDayTransactions.First().SessionName
                }, _localizer, PaymentConstants.Messages.PaymentSameDayWarning);
            }
        }

        // 7. Determine which periods this payment settles. A payment fills the OLDEST unpaid
        // month first and cascades forward. Monthly sessions may settle overdue months through
        // the current local month PLUS at most one month in advance; per-session (per-class)
        // billing keeps its original single-period behavior.
        bool isMonthly = session.PaymentType == PaymentType.Monthly;

        List<PaymentPeriod> payablePeriods;
        if (isMonthly)
        {
            var currentMonthStart = new DateTime(localDate.Year, localDate.Month, 1);
            // End of NEXT month = current-month arrears + one month in advance (the maximum).
            var advanceCapEnd = currentMonthStart.AddMonths(2).AddDays(-1);
            payablePeriods = await _unitOfWork.PaymentsRepo
                .GetUnpaidPeriodsThroughAsync(dto.TeacherId, dto.TeacherStudentId, dto.SessionId, advanceCapEnd);
        }
        else
        {
            var earliest = await _unitOfWork.PaymentsRepo
                .GetEarliestUnpaidPeriodAsync(dto.TeacherId, dto.TeacherStudentId, dto.SessionId);
            payablePeriods = earliest is null
                ? new List<PaymentPeriod>()
                : new List<PaymentPeriod> { earliest };
        }

        // REQ-PAY-026: Already-paid — nothing owed within the payable window. For monthly this
        // also means the student is already paid one month ahead (cannot pay further in advance).
        if (payablePeriods.Count == 0)
        {
            return Result<CollectPaymentResultDto>.Success(new CollectPaymentResultDto
            {
                Transaction = null,
                IsAlreadyPaid = true
            }, _localizer, PaymentConstants.Messages.PaymentAlreadyPaid);
        }

        var firstPeriod = payablePeriods[0];
        // Pro-rating only surfaces when the oldest owed month is the prorated first period.
        bool isProRated = firstPeriod.IsProRated && firstPeriod.PeriodSequence == 1
            && firstPeriod.PeriodType == PeriodType.Monthly;
        string? proRatedLabel = isProRated
            ? $"Pro-rated: {firstPeriod.ProRatedFraction:P0} of full amount" : null;
        decimal amountDue = firstPeriod.AmountDue;

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            var now = DateTime.UtcNow;

            // 8. Cascade the collected cash across the payable periods, oldest first. Each month
            // is settled up to its own remaining due and the cash is attributed to the month it
            // clears (that period's AmountPaid). A single transaction records the whole cash event.
            decimal amountLeft = dto.Amount;
            decimal totalApplied = 0m;
            decimal totalTargetedDue = 0m;
            int periodsNewlyPaid = 0;
            PaymentPeriod? firstTouched = null;
            // PAY-1: capture how much cash landed on each period so we can record a per-period
            // settlement ledger below (enables reversing the exact set of periods this cash cleared).
            var appliedSlices = new List<(PaymentPeriod Period, decimal Amount)>();

            foreach (var p in payablePeriods)
            {
                if (amountLeft <= 0m) break;
                decimal remaining = p.AmountDue - p.AmountPaid;
                if (remaining <= 0m) continue;

                // Monthly caps each month at its remaining and rolls the rest to the next month.
                // Per-session dumps the full amount into its single period (legacy overpay allowed).
                decimal apply = isMonthly ? Math.Min(amountLeft, remaining) : amountLeft;
                p.AmountPaid += apply;
                amountLeft -= apply;
                totalApplied += apply;
                totalTargetedDue += remaining;
                firstTouched ??= p;
                appliedSlices.Add((p, apply));

                p.PaymentStatus = p.AmountPaid >= p.AmountDue
                    ? PaymentStatus.Paid
                    : PaymentStatus.PartiallyPaid;
                if (p.PaymentStatus == PaymentStatus.Paid) periodsNewlyPaid++;

                await _unitOfWork.PaymentsRepo.UpdatePaymentPeriodAsync(p);
            }

            // Monthly: reject cash beyond current-month arrears + one month in advance rather than
            // silently dropping it (server-computed amounts never hit this; a manual overpay can).
            if (isMonthly && amountLeft > 0m)
            {
                if (ownsTransaction) await _unitOfWork.RollbackAsync();
                return Result<CollectPaymentResultDto>.Failure(
                    _localizer, PaymentConstants.Messages.PaymentAmountExceedsAdvanceLimit,
                    HttpStatusCode.UnprocessableEntity);
            }

            bool isPartial = totalApplied < totalTargetedDue;
            var period = firstTouched; // for the notifier + result below

            // 9. Record ONE transaction for the whole cash event (dated now → this month), even
            // when it clears several months. AmountDue = the total remaining it was applied to.
            var transaction = new PaymentTransaction
            {
                TeacherId = dto.TeacherId,
                TeacherStudentId = dto.TeacherStudentId,
                SessionId = dto.SessionId,
                SessionOccurrenceId = firstTouched?.PeriodType == PeriodType.PerSession
                    ? await GetOccurrenceIdForPeriodAsync(firstTouched) : null,
                PaymentPeriodId = firstTouched?.Id,
                StudentSessionAssignmentId = activeAssignment.Id,
                AmountDue = totalTargetedDue,
                AmountPaid = totalApplied,
                PaymentMethod = dto.PaymentMethod,
                PaymentTransactionStatus = isPartial ? PaymentStatus.PartiallyPaid : PaymentStatus.Paid,
                CollectedByUserId = dto.CollectedByUserId,
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                SessionName = session.SessionName,
                CollectedAt = now,
                LocalCollectedAt = dto.IsOfflineRecord && dto.OfflineCollectedAt.HasValue
                    ? dto.OfflineCollectedAt.Value : localDate.Add(now.TimeOfDay),
                IsPartial = isPartial,
                IsProRated = isProRated,
                ProRatedTierLabel = proRatedLabel,
                IsOnlinePayment = dto.PaymentMethod == PaymentCollectionMethod.OnlinePhoneCash
                    || dto.PaymentMethod == PaymentCollectionMethod.OnlineInstaPay,
                OnlineTransactionRef = dto.OnlineTransactionRef,
                IsOfflineRecord = dto.IsOfflineRecord,
                OfflineDeviceId = dto.OfflineDeviceId,
                SyncStatus = dto.IsOfflineRecord ? PaymentSyncStatus.Synced : PaymentSyncStatus.NotApplicable,
                CreateAt = now
            };

            await _unitOfWork.PaymentsRepo.AddAsync(transaction);

            // 9b. PAY-1: record one allocation per settled period. The transaction reference (nav)
            // lets EF fix up the FK on SaveChanges even though its Id is not yet generated. A later
            // refund/edit reverses these exact periods instead of only the denormalized first one.
            if (appliedSlices.Count > 0)
            {
                var allocations = appliedSlices.Select(s => new PaymentTransactionAllocation
                {
                    PaymentTransaction = transaction,
                    PaymentPeriodId = s.Period.Id,
                    TeacherId = dto.TeacherId,
                    AmountApplied = s.Amount,
                    CreateAt = now
                }).ToList();
                await _unitOfWork.PaymentsRepo.AddPaymentTransactionAllocationsRangeAsync(allocations);
            }

            // 10. Update student payment counter (totals + count of months newly cleared) with retry.
            await UpdatePaymentCounterAfterCollectionAsync(
                dto.TeacherId, dto.TeacherStudentId, totalApplied,
                session.SessionName, periodsNewlyPaid);

            // 11. Update assistant wallet if collector is an assistant.
            await UpdateAssistantWalletAfterCollectionAsync(
                dto.TeacherId, dto.CollectedByUserId!.Value, totalApplied);

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            // Phase 4: Notify messaging engine post-commit.
            // Guard: period null means no unpaid period existed (e.g. overpayment path) —
            // skip notification in that case.
            // Guard: ownsTransaction ensures nested callers (SyncOfflinePaymentsAsync)
            // do not double-fire; the outermost owner is solely responsible.
            // PaymentNotifier.SafeDispatchAsync swallows its own errors — a messaging
            // failure never rolls back or fails an already-committed payment.
            //
            // isPartial: period.PaymentStatus is already updated to Paid/PartiallyPaid/Unpaid
            // by step 10 before SaveChangesAsync, so it reflects the post-collection state.
            //
            // Concurrency retry: the DbUpdateConcurrencyException catch rolls back and
            // calls CollectPaymentAsync recursively. The retry is a fresh call with
            // ownsTransaction = true, so the notifier fires exactly once — on the
            // attempt that successfully commits.
            if (ownsTransaction && period is not null)
            {
               
                await _paymentNotifier.OnPaymentRecordedAsync(
                    dto.TeacherId,
                    dto.TeacherStudentId,
                    dto.SessionId,
                    session.SessionName,
                    dto.Amount,
                    FormatPeriodLabel(period),
                    isPartial = period.PaymentStatus == PaymentStatus.PartiallyPaid);
            }

            var resultDto = new CollectPaymentResultDto
            {
                Transaction = MapToTransactionDto(transaction),
                IsProRated = isProRated,
                ProRatedTierLabel = proRatedLabel,
                OriginalAmount = isProRated ? amountDue / (period?.ProRatedFraction ?? 1m) : null,
                ProRatedAmount = isProRated ? amountDue : null
            };
            // Resolve the collector's display name for the collection receipt.
            await EnrichCollectorNameAsync(resultDto.Transaction);

            return Result<CollectPaymentResultDto>.Success(
                resultDto, _localizer, PaymentConstants.Messages.PaymentCollectedSuccess);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            // Retry once on concurrency conflict — set flags to skip duplicate checks
            dto.DuplicateConfirmed = true;
            dto.AlreadyPaidConfirmed = true;
            return await CollectPaymentAsync(dto);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<PaymentStatusDto>> GetStudentPaymentStatusAsync(
        long teacherId, long teacherStudentId)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
            teacherStudentId, teacherId);
        if (student is null)
            return Result<PaymentStatusDto>.Failure(
                _localizer, PaymentConstants.Messages.StudentNotFound, HttpStatusCode.NotFound);

        var counter = await _unitOfWork.PaymentsRepo
            .GetPaymentCounterAsync(teacherId, teacherStudentId);

        var period = await _unitOfWork.PaymentsRepo
            .GetEarliestUnpaidPeriodAsync(teacherId, teacherStudentId, student.SessionId);

        // Session nav is not loaded by GetActiveByIdAndTeacherAsync — resolve separately
        string? sessionName = null;
        if (student.SessionId.HasValue)
        {
            var sessionForStatus = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(
                student.SessionId.Value, teacherId);
            sessionName = sessionForStatus?.SessionName;
        }

        var statusDto = new PaymentStatusDto
        {
            TeacherStudentId = teacherStudentId,
            StudentName = student.StudentName,
            StudentCode = student.StudentCode,
            SessionName = sessionName,
            CurrentStatus = period?.PaymentStatus ?? PaymentStatus.Paid,
            AmountDue = period?.AmountDue ?? 0,
            AmountPaid = period?.AmountPaid ?? 0,
            Outstanding = counter?.TotalOutstanding ?? 0,
            PeriodLabel = period is not null ? FormatPeriodLabel(period) : null,
            ConsecutiveUnpaid = counter?.ConsecutiveUnpaid ?? 0,
            IsProRated = period?.IsProRated ?? false,
            ProRatedTierLabel = period?.IsProRated == true
                ? $"Pro-rated: {period.ProRatedFraction:P0}" : null,
            HasCustomAmount = counter?.CustomPaymentAmount.HasValue ?? false,
            CustomAmount = counter?.CustomPaymentAmount
        };

        return Result<PaymentStatusDto>.Success(
            statusDto, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<DuplicateCheckResultDto>> CheckDuplicatePaymentAsync(
        long teacherId, long teacherStudentId)
    {
        var localDate = _timeZoneService.GetTeacherLocalDate(teacherId);

        var sameDayTransactions = await _unitOfWork.PaymentsRepo
            .GetSameDayTransactionsAsync(teacherId, teacherStudentId, localDate);

        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
            teacherStudentId, teacherId);

        var period = student?.SessionId is not null
            ? await _unitOfWork.PaymentsRepo
                .GetEarliestUnpaidPeriodAsync(teacherId, teacherStudentId, student.SessionId)
            : null;

        return Result<DuplicateCheckResultDto>.Success(new DuplicateCheckResultDto
        {
            HasSameDayPayment = sameDayTransactions.Count > 0,
            TodayPaidAmount = sameDayTransactions.Sum(t => t.AmountPaid),
            TodayPaidPeriodLabel = sameDayTransactions.FirstOrDefault()?.SessionName,
            IsCurrentPeriodPaid = period is null,
            CurrentPeriodLabel = period is not null ? FormatPeriodLabel(period) : null
        }, _localizer, PaymentConstants.Messages.Success);
    }

    // ══════════════════════════════════════════════
    // PAYMENT EDIT/DELETE
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaymentTransactionDto>> EditPaymentAsync(EditPaymentDto dto)
    {
        var transaction = await _unitOfWork.PaymentsRepo
            .GetTransactionByIdAndTeacherAsync(dto.TransactionId, dto.TeacherId);
        if (transaction is null)
            return Result<PaymentTransactionDto>.Failure(
                _localizer, PaymentConstants.Messages.PaymentNotFound, HttpStatusCode.NotFound);

        // PAY-3: a paid amount can never be negative (a negative "payment" corrupts collected-cash
        // figures and period balances). Zero stays valid — a full revert edits the amount to 0.
        if (dto.NewAmount.HasValue && dto.NewAmount.Value < 0m)
            return Result<PaymentTransactionDto>.Failure(
                _localizer, PaymentConstants.Messages.PaymentAmountNegative, HttpStatusCode.UnprocessableEntity);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Log the edit
            var editLog = new PaymentEditLog
            {
                PaymentTransactionId = transaction.Id,
                EditAction = dto.NewAmount.HasValue ? PaymentEditAction.AmountChanged : PaymentEditAction.StatusChanged,
                PreviousAmount = transaction.AmountPaid,
                NewAmount = dto.NewAmount ?? transaction.AmountPaid,
                PreviousStatus = transaction.PaymentTransactionStatus,
                NewStatus = dto.NewStatus ?? transaction.PaymentTransactionStatus,
                EditedByUserId = dto.EditedByUserId,
                EditedAt = DateTime.UtcNow,
                EditReason = dto.EditReason,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.PaymentsRepo.AddPaymentEditLogAsync(editLog);

            // Apply changes
            decimal amountDiff = 0;
            if (dto.NewAmount.HasValue)
            {
                amountDiff = dto.NewAmount.Value - transaction.AmountPaid;
                transaction.AmountPaid = dto.NewAmount.Value;
                transaction.IsPartial = transaction.AmountPaid < transaction.AmountDue;
            }

            if (dto.NewStatus.HasValue)
                transaction.PaymentTransactionStatus = dto.NewStatus.Value;

            await _unitOfWork.PaymentsRepo.UpdateAsync(transaction);

            // Adjust the settled periods to match the new amount (PAY-1). A cascade payment can have
            // settled several periods, so the delta is applied across the EXACT set it touched:
            // reducing un-funds the most-advanced months first (LIFO); increasing cascades the surplus
            // forward onto the next unpaid months. (Legacy transactions with no ledger fall back to the
            // single denormalized period inside the helper.) Counter/wallet move by the cash delta only.
            if (amountDiff < 0)
                await ReversePeriodAllocationsAsync(transaction, -amountDiff);
            else if (amountDiff > 0)
                await ApplyForwardAllocationsAsync(
                    transaction, amountDiff, _timeZoneService.GetTeacherLocalDate(dto.TeacherId));

            // Update counter + wallet if amount changed.
            if (amountDiff != 0)
            {
                if (transaction.TeacherStudentId.HasValue)
                {
                    var counter = await _unitOfWork.PaymentsRepo
                        .GetPaymentCounterAsync(dto.TeacherId, transaction.TeacherStudentId.Value);
                    if (counter is not null)
                    {
                        counter.TotalAmountPaid += amountDiff;
                        counter.TotalOutstanding -= amountDiff;
                        counter.ConsecutiveUnpaid = await _unitOfWork.PaymentsRepo
                            .RecalculateConsecutiveUnpaidAsync(dto.TeacherId, transaction.TeacherStudentId.Value);
                        await _unitOfWork.PaymentsRepo.UpdatePaymentCounterAsync(counter);
                    }
                }

                // Keep the collecting assistant's wallet in sync with the amount change.
                await AdjustAssistantWalletAsync(dto.TeacherId, transaction.CollectedByUserId, amountDiff);
            }

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var editedDto = MapToTransactionDto(transaction);
            // Resolve the collector's display name for the edit receipt.
            await EnrichCollectorNameAsync(editedDto);
            return Result<PaymentTransactionDto>.Success(
                editedDto, _localizer, PaymentConstants.Messages.PaymentEditSuccess);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeletePaymentAsync(
        long teacherId, long transactionId, long deletedByUserId)
    {
        var transaction = await _unitOfWork.PaymentsRepo
            .GetTransactionByIdAndTeacherAsync(transactionId, teacherId);
        if (transaction is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.PaymentNotFound, HttpStatusCode.NotFound);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Log the deletion
            var editLog = new PaymentEditLog
            {
                PaymentTransactionId = transaction.Id,
                EditAction = PaymentEditAction.Deleted,
                PreviousAmount = transaction.AmountPaid,
                NewAmount = 0,
                PreviousStatus = transaction.PaymentTransactionStatus,
                NewStatus = PaymentStatus.Unpaid,
                EditedByUserId = deletedByUserId,
                EditedAt = DateTime.UtcNow,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.PaymentsRepo.AddPaymentEditLogAsync(editLog);

            // Soft delete
            transaction.IsDeleted = true;
            transaction.DeletedAt = DateTime.UtcNow;
            await _unitOfWork.PaymentsRepo.UpdateAsync(transaction);

            // Reverse the EXACT set of periods this payment settled (PAY-1). A cascade collection can
            // have cleared several months while the transaction stores only the first period id;
            // reversing that one alone would leave the later months reading Paid with no backing cash.
            // Counter/wallet (below) reverse the full cash amount independently, so a purged period
            // never distorts the totals.
            await ReversePeriodAllocationsAsync(transaction, transaction.AmountPaid);

            if (transaction.TeacherStudentId.HasValue)
            {
                var counter = await _unitOfWork.PaymentsRepo
                    .GetPaymentCounterAsync(teacherId, transaction.TeacherStudentId.Value);
                if (counter is not null)
                {
                    counter.TotalAmountPaid -= transaction.AmountPaid;
                    counter.TotalOutstanding += transaction.AmountPaid;
                    counter.ConsecutiveUnpaid = await _unitOfWork.PaymentsRepo
                        .RecalculateConsecutiveUnpaidAsync(teacherId, transaction.TeacherStudentId.Value);
                    await _unitOfWork.PaymentsRepo.UpdatePaymentCounterAsync(counter);
                }
            }

            // Reverse the collecting assistant's wallet — the refunded cash is no longer held by them.
            await AdjustAssistantWalletAsync(teacherId, transaction.CollectedByUserId, -transaction.AmountPaid);

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.PaymentDeleteSuccess);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<PaymentEditLogDto>>> GetPaymentEditHistoryAsync(
        long teacherId, long transactionId)
    {
        var transaction = await _unitOfWork.PaymentsRepo
            .GetTransactionByIdAndTeacherAsync(transactionId, teacherId);
        if (transaction is null)
            return Result<List<PaymentEditLogDto>>.Failure(
                _localizer, PaymentConstants.Messages.PaymentNotFound, HttpStatusCode.NotFound);

        var logs = await _unitOfWork.PaymentsRepo.GetPaymentEditLogsAsync(transactionId);
        var dtos = logs.Select(l => new PaymentEditLogDto
        {
            Id = l.Id,
            EditAction = l.EditAction,
            PreviousAmount = l.PreviousAmount,
            NewAmount = l.NewAmount,
            PreviousStatus = l.PreviousStatus,
            NewStatus = l.NewStatus,
            EditedAt = l.EditedAt,
            EditedByUserId = l.EditedByUserId,
            EditReason = l.EditReason
        }).ToList();

        return Result<List<PaymentEditLogDto>>.Success(dtos, _localizer, PaymentConstants.Messages.Success);
    }

    // ══════════════════════════════════════════════
    // STUDENT PAYMENT HISTORY
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<PaymentHistoryDto>>> GetStudentPaymentHistoryAsync(
        long teacherId, long teacherStudentId,
        DateTime? startDate, DateTime? endDate,
        int page, int pageSize)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
            teacherStudentId, teacherId);
        if (student is null)
            return Result<PaginatedResponse<PaymentHistoryDto>>.Failure(
                _localizer, PaymentConstants.Messages.StudentNotFound, HttpStatusCode.NotFound);

        var counter = await _unitOfWork.PaymentsRepo
            .GetPaymentCounterAsync(teacherId, teacherStudentId);

        // Honor the optional startDate/endDate window (previously ignored).
        var periods = await _unitOfWork.PaymentsRepo
            .GetPaymentPeriodsByStudentInRangeAsync(teacherId, teacherStudentId, startDate, endDate);

        var transfers = await _unitOfWork.PaymentsRepo
            .GetStudentTransferEventsAsync(teacherId, teacherStudentId);

        var departures = await _unitOfWork.PaymentsRepo
            .GetStudentDeparturesAsync(teacherId, teacherStudentId);

        var historyDto = new PaymentHistoryDto
        {
            TeacherStudentId = teacherStudentId,
            StudentName = student.StudentName,
            StudentCode = student.StudentCode,
            TotalAmountPaid = counter?.TotalAmountPaid ?? 0,
            TotalOutstanding = counter?.TotalOutstanding ?? 0,
            // Periods populated after pagination below
            Transfers = transfers.Select(t => new SessionTransferEventDto
            {
                Id = t.Id,
                SourceSessionName = t.SourceSessionName,
                DestinationSessionName = t.DestinationSessionName,
                PaymentStatusAtTransfer = t.PaymentStatusAtTransfer,
                OutstandingBalance = t.OutstandingBalance,
                CreditBalance = t.CreditBalance,
                TransferredAt = t.TransferredAt
            }).ToList(),
            Departures = departures.Select(d => new StudentDepartureDto
            {
                Id = d.Id,
                SessionName = d.SessionName,
                StudentName = d.StudentName,
                PaymentStatusAtDeparture = d.PaymentStatusAtDeparture,
                TotalOccurrencesInPeriod = d.TotalOccurrencesInPeriod,
                AttendedOccurrences = d.AttendedOccurrences,
                FullPeriodAmount = d.FullPeriodAmount,
                ProRatedAmount = d.ProRatedAmount,
                FinalAmount = d.FinalAmount,
                IsTutorOverride = d.IsTutorOverride,
                DepartureOutcome = d.DepartureOutcome,
                DepartedAt = d.DepartedAt
            }).ToList()
        };

        // Apply pagination to periods list
        var pagedPeriods = periods
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        historyDto.Periods = pagedPeriods.Select(MapToPeriodDto).ToList();
        // Resolve each period-transaction's collector display name (batch, no N+1).
        await EnrichCollectorNamesAsync(historyDto.Periods);

        var response = new PaginatedResponse<PaymentHistoryDto>
        {
            totalCount = periods.Count,
            page = page,
            pageSize = pageSize,
            totalPages = (int)Math.Ceiling(periods.Count / (double)pageSize),
            data = historyDto
        };

        return Result<PaginatedResponse<PaymentHistoryDto>>.Success(
            response, _localizer, PaymentConstants.Messages.Success);
    }

    // ══════════════════════════════════════════════
    // CUSTOM AMOUNT
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<decimal?>> GetCustomAmountAsync(long teacherId, long teacherStudentId)
    {
        var counter = await _unitOfWork.PaymentsRepo
            .GetPaymentCounterAsync(teacherId, teacherStudentId);
        return Result<decimal?>.Success(
            counter?.CustomPaymentAmount, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> SetCustomAmountAsync(SetCustomAmountDto dto)
    {
        // PAY-4: a custom price must be positive. A negative/zero amount would drive negative dues
        // and negative expected revenue on the next assignment or period regeneration. Null is
        // allowed — it clears the override and reverts the student to the session default.
        if (dto.CustomAmount.HasValue && dto.CustomAmount.Value <= 0m)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.PaymentCustomAmountInvalid,
                HttpStatusCode.UnprocessableEntity);

        var counter = await _unitOfWork.PaymentsRepo
            .GetPaymentCounterAsync(dto.TeacherId, dto.TeacherStudentId);
        if (counter is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.StudentNotFound, HttpStatusCode.NotFound);

        counter.CustomPaymentAmount = dto.CustomAmount;
        await _unitOfWork.PaymentsRepo.UpdatePaymentCounterAsync(counter);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.CustomAmountSetSuccess);
    }

    // ══════════════════════════════════════════════
    // UNPAID OVERVIEW
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<UnpaidStudentDto>>>> GetUnpaidStudentsAsync(
        long teacherId, UnpaidStudentsFilterDto filter)
    {
        // Arrears must be judged THROUGH a cutoff month (CLAUDE.md §7.4): periods are pre-generated
        // to the session end, so the previous counter-driven read reported every future month as
        // owed and listed fully-paid students as unpaid. No explicit asOfMonth → the teacher's
        // current local (Africa/Cairo) month, matching the api/v1 screens.
        if (!TryResolveAsOfMonthEnd(teacherId, filter.AsOfMonth, out var throughMonthEnd))
            return Result<PaginatedResponse<List<UnpaidStudentDto>>>.Failure(
                _localizer, PaymentConstants.Messages.PaymentInvalidMonthFormat,
                HttpStatusCode.UnprocessableEntity);

        var (items, totalCount) = await _unitOfWork.PaymentsRepo.GetUnpaidStudentsPagedAsync(
            teacherId, filter.SessionId, filter.SessionGroupId,
            filter.PaymentType, filter.MinConsecutiveUnpaid,
            filter.Search, throughMonthEnd, filter.Page, filter.PageSize);

        var dtos = new List<UnpaidStudentDto>(items.Count);
        foreach (var row in items)
        {
            dtos.Add(new UnpaidStudentDto
            {
                TeacherStudentId = row.TeacherStudentId,
                StudentName = row.StudentName,
                StudentCode = row.StudentCode,
                SessionName = row.SessionName,
                SessionId = row.SessionId,
                // Unpaid periods through the cutoff are always a contiguous tail (collection
                // cascades oldest-first), so the consecutive count IS the total count — BR-PAY-006.
                // This is what finally makes REQ-PAY-029/030 (2+ consecutive) discriminate.
                ConsecutiveUnpaid = row.UnpaidPeriodCount,
                TotalUnpaidPeriods = row.UnpaidPeriodCount,
                TotalOutstanding = row.TotalOutstanding,
                LastPaymentDate = row.LastPaymentDate,
                // REQ-PAY-031: this list was declared on the DTO but never assigned — every
                // response shipped an empty array.
                UnpaidPeriodLabels = row.UnpaidPeriods.Select(FormatUnpaidPeriodLabel).ToList()
            });
        }

        var response = new PaginatedResponse<List<UnpaidStudentDto>>
        {
            totalCount = totalCount,
            page = filter.Page,
            pageSize = filter.PageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize),
            data = dtos
        };

        return Result<PaginatedResponse<List<UnpaidStudentDto>>>.Success(
            response, _localizer, PaymentConstants.Messages.UnpaidStudentsLoaded);
    }

    /// <summary>
    /// Resolves the optional "YYYY-MM" arrears cutoff to that month's LAST day, defaulting to the
    /// teacher's current local (Africa/Cairo) month when omitted (§7.4). Returns false ONLY when a
    /// value was supplied but is malformed or out of range — the caller maps that to 422.
    /// </summary>
    private bool TryResolveAsOfMonthEnd(long teacherId, string? asOfMonth, out DateTime monthEnd)
    {
        int year;
        int month;

        if (string.IsNullOrWhiteSpace(asOfMonth))
        {
            var today = _timeZoneService.GetTeacherLocalDate(teacherId);
            year = today.Year;
            month = today.Month;
        }
        else
        {
            var parts = asOfMonth.Trim().Split('-');
            if (parts.Length != 2
                || !int.TryParse(parts[0], out year) || year < 1 || year > 9999
                || !int.TryParse(parts[1], out month) || month < 1 || month > 12)
            {
                monthEnd = default;
                return false;
            }
        }

        monthEnd = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
        return true;
    }

    /// <summary>
    /// Display label for one unpaid period: the calendar month for a Monthly obligation, the
    /// occurrence date for a PerSession one.
    /// </summary>
    private static string FormatUnpaidPeriodLabel(UnpaidPeriodRef period) =>
        period.PeriodType == PeriodType.Monthly
            ? period.PeriodStart.ToString("MMMM yyyy")
            : period.PeriodStart.ToString("yyyy-MM-dd");

    /// <inheritdoc />
    public async Task<Result<int>> GetUnpaidCountBySessionAsync(long teacherId, long sessionId)
    {
        var count = await _unitOfWork.PaymentsRepo.CountUnpaidStudentsBySessionAsync(teacherId, sessionId);
        return Result<int>.Success(count, _localizer, PaymentConstants.Messages.Success);
    }

    // ══════════════════════════════════════════════
    // COLLECTOR VIEW
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<List<CollectorSummaryDto>>> GetCollectorSummaryAsync(
        long teacherId, DateTime? startDate, DateTime? endDate, long? scopeToCollectorUserId = null)
    {
        var collectorData = await _unitOfWork.PaymentsRepo
            .GetDashboardPerCollectorAsync(teacherId, startDate, endDate);

        // TODO(assistant-dashboard): interim own-scoping (REQ-PAY-014). An assistant caller sees ONLY
        // their own collection summary; a teacher (scope null) sees every collector. The dedicated
        // assistant collection view is to be built end-to-end by frontend + backend.
        if (scopeToCollectorUserId is long ownUserId)
            collectorData = collectorData.Where(c => c.UserId == ownUserId).ToList();

        var dtos = collectorData.Select(c => new CollectorSummaryDto
        {
            UserId = c.UserId,
            UserName = c.UserName ?? "Unknown",
            UserRole = "Collector", // Would be resolved from user data
            TotalCollected = c.Collected,
            TransactionCount = c.TransactionCount
        }).ToList();

        return Result<List<CollectorSummaryDto>>.Success(
            dtos, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<StudentPaymentStatusCountsDto>> GetStudentPaymentStatusCountsAsync(long teacherId)
    {
        // Buckets are judged through the teacher's current local month (future pre-generated
        // periods are not counted as owed).
        var today = _timeZoneService.GetTeacherLocalDate(teacherId);
        var monthEnd = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
        var (paid, proRated, unpaid) = await _unitOfWork.PaymentsRepo
            .GetStudentPaymentStatusCountsAsync(teacherId, monthEnd);

        var dto = new StudentPaymentStatusCountsDto
        {
            PaidCount = paid,
            ProRatedCount = proRated,
            UnpaidCount = unpaid
        };

        return Result<StudentPaymentStatusCountsDto>.Success(
            dto, _localizer, PaymentConstants.Messages.Success);
    }

    // ══════════════════════════════════════════════
    // WALLET MANAGEMENT
    // ══════════════════════════════════════════════

    /// <summary>Maps an <see cref="AssistantWallet"/> row to its wire DTO (single-sourced mapper).</summary>
    private static AssistantWalletDto MapWalletDto(AssistantWallet w) => new()
    {
        AssistantId = w.AssistantId,
        AssistantName = w.Assistant?.User?.FullName ?? "Unknown",
        CurrentBalance = w.CurrentBalance,
        TotalCollected = w.TotalCollected,
        TransactionCount = w.TransactionCount,
        LastCollectionAt = w.LastCollectionAt
    };

    /// <inheritdoc />
    public async Task<Result<AssistantWalletsSummaryDto>> GetAllWalletsAsync(
        long teacherId, long? scopeToAssistantUserId = null)
    {
        // TODO(assistant-dashboard): interim own-scoping. An assistant caller sees ONLY their own
        // wallet (never peers'); the combined total is just their own balance. The proper assistant
        // dashboard is to be built end-to-end by frontend + backend.
        if (scopeToAssistantUserId is long ownUserId)
        {
            var own = await _unitOfWork.PaymentsRepo.GetAssistantWalletByUserIdAsync(teacherId, ownUserId);
            var ownList = own is null
                ? new List<AssistantWalletDto>()
                : new List<AssistantWalletDto> { MapWalletDto(own) };

            return Result<AssistantWalletsSummaryDto>.Success(new AssistantWalletsSummaryDto
            {
                TotalCurrentBalance = own?.CurrentBalance ?? 0m,
                Assistants = ownList
            }, _localizer, PaymentConstants.Messages.Success);
        }

        var wallets = await _unitOfWork.PaymentsRepo.GetAllAssistantWalletsAsync(teacherId);
        var dtos = wallets.Select(MapWalletDto).ToList();

        var summary = new AssistantWalletsSummaryDto
        {
            TotalCurrentBalance = dtos.Sum(d => d.CurrentBalance),
            Assistants = dtos
        };

        return Result<AssistantWalletsSummaryDto>.Success(
            summary, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<AssistantWalletDto>> GetWalletDetailAsync(
        long teacherId, long assistantId, long? restrictToAssistantUserId = null)
    {
        // TODO(assistant-dashboard): interim own-scoping. When an assistant calls, the requested
        // assistantId is IGNORED and their OWN wallet (by user id) is returned so they can never read
        // a peer's. Teacher/SuperAdmin (restrict null) resolve the requested assistant as before.
        var wallet = restrictToAssistantUserId is long ownUserId
            ? await _unitOfWork.PaymentsRepo.GetAssistantWalletByUserIdAsync(teacherId, ownUserId)
            : await _unitOfWork.PaymentsRepo.GetAssistantWalletAsync(teacherId, assistantId);

        if (wallet is null)
            return Result<AssistantWalletDto>.Failure(
                _localizer, PaymentConstants.Messages.WalletNotFound, HttpStatusCode.NotFound);

        return Result<AssistantWalletDto>.Success(
            MapWalletDto(wallet), _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<WalletResetLogDto>> ResetWalletAsync(WalletResetDto dto)
    {
        var wallet = await _unitOfWork.PaymentsRepo.GetAssistantWalletAsync(dto.TeacherId, dto.AssistantId);
        if (wallet is null)
            return Result<WalletResetLogDto>.Failure(
                _localizer, PaymentConstants.Messages.WalletNotFound, HttpStatusCode.NotFound);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            var resetLog = new WalletResetLog
            {
                TeacherId = dto.TeacherId,
                AssistantId = dto.AssistantId,
                AssistantWalletId = wallet.Id,
                AmountReset = wallet.CurrentBalance,
                ResetByUserId = dto.ResetByUserId,
                ResetAt = DateTime.UtcNow,
                AssistantName = wallet.Assistant?.User?.FullName,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.PaymentsRepo.AddWalletResetLogAsync(resetLog);

            wallet.CurrentBalance = 0;
            await _unitOfWork.PaymentsRepo.UpdateAssistantWalletAsync(wallet);

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<WalletResetLogDto>.Success(new WalletResetLogDto
            {
                Id = resetLog.Id,
                AssistantName = resetLog.AssistantName,
                AmountReset = resetLog.AmountReset,
                ResetAt = resetLog.ResetAt,
                ResetByUserId = resetLog.ResetByUserId
            }, _localizer, PaymentConstants.Messages.WalletResetSuccess);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<WalletWithdrawalResult>> WithdrawFromWalletAsync(
        long teacherId, long assistantId, decimal? amount, long withdrawnByUserId)
    {
        var wallet = await _unitOfWork.PaymentsRepo.GetAssistantWalletAsync(teacherId, assistantId);
        if (wallet is null)
            return Result<WalletWithdrawalResult>.Failure(
                _localizer, PaymentConstants.Messages.WalletNotFound, HttpStatusCode.NotFound);

        // Amount defaults to the full current balance (a "take everything" handover).
        decimal withdrawAmount = amount ?? wallet.CurrentBalance;
        if (withdrawAmount <= 0m)
            return Result<WalletWithdrawalResult>.Failure(
                _localizer, PaymentConstants.Messages.PaymentWithdrawAmountInvalid,
                HttpStatusCode.UnprocessableEntity);
        if (withdrawAmount > wallet.CurrentBalance)
            return Result<WalletWithdrawalResult>.Failure(
                _localizer, PaymentConstants.Messages.PaymentWalletInsufficientBalance,
                HttpStatusCode.Conflict);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Withdraw and full-reset are the same real-world event (tutor takes the cash), so a
            // partial withdrawal is recorded in the same WalletResetLog ledger — with AmountReset
            // set to the withdrawn amount rather than the full balance.
            var log = new WalletResetLog
            {
                TeacherId = teacherId,
                AssistantId = assistantId,
                AssistantWalletId = wallet.Id,
                AmountReset = withdrawAmount,
                ResetByUserId = withdrawnByUserId,
                ResetAt = DateTime.UtcNow,
                AssistantName = wallet.Assistant?.User?.FullName,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.PaymentsRepo.AddWalletResetLogAsync(log);

            wallet.CurrentBalance -= withdrawAmount;
            await _unitOfWork.PaymentsRepo.UpdateAssistantWalletAsync(wallet);

            await _unitOfWork.SaveChangesAsync();
            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<WalletWithdrawalResult>.Success(new WalletWithdrawalResult
            {
                WithdrawalId = log.Id,
                Amount = withdrawAmount,
                WalletBalanceAfter = wallet.CurrentBalance,
                RequestedAt = log.ResetAt
            }, _localizer, PaymentConstants.Messages.Success);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            // A concurrent collection bumped the wallet's RowVersion — roll back and let the
            // caller retry with a fresh read (idempotency-key retries are safe). Clean 409, not 500.
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            return Result<WalletWithdrawalResult>.Failure(
                _localizer, PaymentConstants.Messages.PaymentWalletConcurrencyConflict,
                HttpStatusCode.Conflict);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════
    // DASHBOARD
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaymentDashboardDto>> GetDashboardAsync(
        long teacherId, PaymentDashboardFilterDto filter, long? scopeToCollectorUserId = null)
    {
        // TODO(assistant-dashboard): interim own-scoping. An assistant caller gets a dashboard scoped
        // to THEIR OWN collections only: CollectedRevenue = what they personally collected, and every
        // teacher-wide figure (expected / remaining / per-session) is returned NULL — not 0 — so the
        // client can render an assistant view without mistaking "not yours" for a real zero. A proper
        // assistant dashboard (with its own expected/target semantics) is to be designed and built
        // end-to-end by frontend + backend.
        if (scopeToCollectorUserId is long ownUserId)
        {
            var collectors = await _unitOfWork.PaymentsRepo
                .GetDashboardPerCollectorAsync(teacherId, filter.StartDate, filter.EndDate);
            // GetDashboardPerCollectorAsync returns value tuples, so match explicitly rather than
            // relying on a null FirstOrDefault.
            var mineRows = collectors.Where(c => c.UserId == ownUserId).ToList();
            bool hasMine = mineRows.Count > 0;
            var mine = hasMine ? mineRows[0] : default;

            var scoped = new PaymentDashboardDto
            {
                ExpectedRevenue = null,   // teacher-wide — not applicable to an assistant
                RemainingRevenue = null,  // teacher-wide — not applicable to an assistant
                CollectedRevenue = hasMine ? mine.Collected : 0m,
                PerSessionBreakdown = null, // teacher-wide — not applicable to an assistant
                PerCollectorBreakdown = hasMine
                    ? new List<CollectorRevenueBreakdownDto>
                    {
                        new()
                        {
                            UserId = mine.UserId,
                            UserName = mine.UserName,
                            Collected = mine.Collected,
                            TransactionCount = mine.TransactionCount
                        }
                    }
                    : new List<CollectorRevenueBreakdownDto>()
            };

            return Result<PaymentDashboardDto>.Success(
                scoped, _localizer, PaymentConstants.Messages.DashboardLoaded);
        }

        var (expected, collected, remaining) = await _unitOfWork.PaymentsRepo
            .GetDashboardAggregatesAsync(
                teacherId, filter.SessionId, filter.SessionGroupId,
                filter.PaymentType, filter.StartDate, filter.EndDate);

        var perSession = await _unitOfWork.PaymentsRepo
            .GetDashboardPerSessionAsync(
                teacherId, filter.SessionGroupId, filter.PaymentType,
                filter.StartDate, filter.EndDate);

        var perCollector = await _unitOfWork.PaymentsRepo
            .GetDashboardPerCollectorAsync(teacherId, filter.StartDate, filter.EndDate);

        var dashboard = new PaymentDashboardDto
        {
            ExpectedRevenue = expected,
            CollectedRevenue = collected,
            RemainingRevenue = remaining,
            PerSessionBreakdown = perSession.Select(s => new SessionRevenueBreakdownDto
            {
                SessionId = s.SessionId,
                SessionName = s.SessionName,
                Expected = s.Expected,
                Collected = s.Collected,
                Remaining = s.Remaining
            }).ToList(),
            PerCollectorBreakdown = perCollector.Select(c => new CollectorRevenueBreakdownDto
            {
                UserId = c.UserId,
                UserName = c.UserName,
                Collected = c.Collected,
                TransactionCount = c.TransactionCount
            }).ToList()
        };

        return Result<PaymentDashboardDto>.Success(
            dashboard, _localizer, PaymentConstants.Messages.DashboardLoaded);
    }

    /// <inheritdoc />
    public async Task<Result<List<SessionCollectionSummaryDto>>> GetSessionsCollectionSummaryAsync(
        long teacherId)
    {
        var rows = await _unitOfWork.PaymentsRepo.GetActiveSessionsCollectionSummaryAsync(teacherId);

        var dtos = rows.Select(r => new SessionCollectionSummaryDto
        {
            SessionId = r.SessionId,
            SessionName = r.SessionName,
            ScheduleLabel = BuildSessionScheduleLabel(r),
            CollectedAmount = r.CollectedAmount,
            ExpectedAmount = r.ExpectedAmount,
            PaidStudentCount = r.PaidStudents,
            TotalStudentCount = r.TotalStudents,
            PercentCollected = r.ExpectedAmount > 0
                ? Math.Round(r.CollectedAmount / r.ExpectedAmount * 100, 0)
                : 0
        }).ToList();

        return Result<List<SessionCollectionSummaryDto>>.Success(
            dtos, _localizer, PaymentConstants.Messages.Success);
    }

    /// <summary>
    /// Day indices per Session.SelectedDays: "0,3,5" where 0=Saturday … 6=Friday
    /// (see Session.SelectedDays doc comment).
    /// </summary>
    private static readonly string[] WeekDayNames =
    {
        "Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday"
    };

    private static string BuildSessionScheduleLabel(ActiveSessionCollectionSummaryRow row)
    {
        var timeLabel = DateTime.MinValue.Add(row.StartTime).ToString("h:mm tt");

        string dayLabel;
        if (row.OccurrenceType == OccurrenceType.Monthly)
        {
            dayLabel = row.MonthlyDayOfMonth.HasValue
                ? $"Day {row.MonthlyDayOfMonth.Value}"
                : "Monthly";
        }
        else
        {
            var dayNames = (row.SelectedDays ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => int.TryParse(d, out var i) && i >= 0 && i < WeekDayNames.Length
                    ? WeekDayNames[i]
                    : null)
                .Where(name => name is not null);

            dayLabel = string.Join(", ", dayNames);
            if (string.IsNullOrEmpty(dayLabel))
                dayLabel = "Weekly";
        }

        return $"{dayLabel} - {timeLabel} - {row.SessionName}";
    }

    // ══════════════════════════════════════════════
    // DEPARTURE
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<DepartureSummaryDto>> GetDepartureSummaryAsync(
        long teacherId, long teacherStudentId)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
            teacherStudentId, teacherId);
        if (student is null || student.SessionId is null)
            return Result<DepartureSummaryDto>.Failure(
                _localizer, PaymentConstants.Messages.DepartureStudentNotAssigned, HttpStatusCode.BadRequest);

        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(
            student.SessionId.Value, teacherId);
        if (session is null)
            return Result<DepartureSummaryDto>.Failure(
                _localizer, PaymentConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        // Get current period
        var period = await _unitOfWork.PaymentsRepo
            .GetEarliestUnpaidPeriodAsync(teacherId, teacherStudentId, student.SessionId);

        // Get occurrence data for pro-rating
        var localDate = _timeZoneService.GetTeacherLocalDate(teacherId);
        DateTime periodStart, periodEnd;
        if (session.PaymentType == PaymentType.Monthly)
        {
            periodStart = new DateTime(localDate.Year, localDate.Month, 1);
            periodEnd = periodStart.AddMonths(1).AddDays(-1);
        }
        else
        {
            periodStart = period?.PeriodStart ?? localDate;
            periodEnd = period?.PeriodEnd ?? localDate;
        }

        var occurrences = await _unitOfWork.AttendanceRepo
            .GetOccurrencesBySessionAsync(student.SessionId.Value);
        var periodOccurrences = occurrences
            .Where(o => o.OccurrenceDate >= periodStart && o.OccurrenceDate <= periodEnd)
            .ToList();

        // Count attended occurrences (BR-PAY-007)
        // BR-PAY-007: "Unrecorded occurrences — where attendance was never taken —
        // shall be excluded from both the numerator and denominator."
        int totalRecordedOccurrences = 0;
        int attendedOccurrences = 0;
        foreach (var occ in periodOccurrences)
        {
            var records = await _unitOfWork.AttendanceRepo
                .GetExistingAttendanceByStudentSessionAndDateAsync(
                    teacherStudentId, student.SessionId.Value, occ.OccurrenceDate);
            if (records is not null)
            {
                // This occurrence has a recorded attendance — include in denominator
                totalRecordedOccurrences++;
                if (records.Status != AttendanceStatus.Absent)
                    attendedOccurrences++;
            }
            // If records is null, attendance was never taken — excluded per BR-PAY-007
        }
        int totalOccurrences = totalRecordedOccurrences;

        // REQ-PAY-068: Pro-rated calculation
        decimal fullAmount = period?.AmountDue ?? session.SessionAmount;
        decimal proRatedAmount = totalOccurrences > 0
            ? (attendedOccurrences / (decimal)totalOccurrences) * fullAmount
            : 0;

        var paymentStatus = period?.PaymentStatus ?? PaymentStatus.Paid;

        // The proration setting (AAM-FR-04.4) drives the refund basis.
        var teacherConfig = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        bool prorationEnabled = teacherConfig?.IsProratedPaymentEnabled ?? false;

        DepartureOutcome outcome;
        decimal finalAmount;

        if (!prorationEnabled)
        {
            // Proration OFF: refund the FULL amount the student paid for their CURRENT month (or the
            // most recent paid month if this month is unpaid) — not pro-rated, and NOT cumulative
            // since they joined. The tutor may still override with a specific amount at confirm time.
            var paidPeriod = await _unitOfWork.PaymentsRepo
                .GetLatestPaidPeriodAsync(teacherId, teacherStudentId, student.SessionId);
            finalAmount = paidPeriod?.AmountPaid ?? 0m;
            proRatedAmount = 0m;
            if (paidPeriod is not null) fullAmount = paidPeriod.AmountDue;
            outcome = finalAmount > 0m ? DepartureOutcome.RefundDue : DepartureOutcome.NoObligation;
        }
        else if (paymentStatus == PaymentStatus.Paid)
        {
            // REQ-PAY-069: Refund = Full - ProRated
            finalAmount = fullAmount - proRatedAmount;
            outcome = finalAmount > 0 ? DepartureOutcome.RefundDue : DepartureOutcome.NoObligation;
        }
        else if (attendedOccurrences == 0)
        {
            // REQ-PAY-071: No obligation
            finalAmount = 0;
            outcome = DepartureOutcome.NoObligation;
        }
        else
        {
            // REQ-PAY-070: Amount owed
            finalAmount = proRatedAmount;
            outcome = DepartureOutcome.AmountOwed;
        }

        return Result<DepartureSummaryDto>.Success(new DepartureSummaryDto
        {
            StudentName = student.StudentName,
            StudentCode = student.StudentCode,
            SessionName = session.SessionName,
            CurrentPeriodLabel = FormatPeriodLabel(periodStart, periodEnd),
            TotalOccurrencesInPeriod = totalOccurrences,
            AttendedOccurrences = attendedOccurrences,
            FullPeriodAmount = fullAmount,
            ProRatedAmount = proRatedAmount,
            PaymentStatusAtDeparture = paymentStatus,
            DepartureOutcome = outcome,
            FinalAmount = finalAmount,
            OutcomeLabel = outcome switch
            {
                DepartureOutcome.RefundDue => $"Amount to refund to student: {finalAmount:F2} EGP",
                DepartureOutcome.AmountOwed => $"Amount student still owes: {finalAmount:F2} EGP",
                _ => "No financial obligation"
            }
        }, _localizer, PaymentConstants.Messages.DepartureSummaryLoaded);
    }

    /// <inheritdoc />
    public async Task<Result<StudentDepartureDto>> ConfirmDepartureAsync(ConfirmDepartureDto dto)
    {
        var summaryResult = await GetDepartureSummaryAsync(dto.TeacherId, dto.TeacherStudentId);
        if (!summaryResult.IsSuccess)
            // PAY-8: preserve the stable Code (e.g. DepartureStudentNotAssigned) — the string overload drops it.
            return Result<StudentDepartureDto>.Failure(summaryResult);

        var summary = summaryResult.Data!;
        decimal finalAmount = dto.OverrideAmount ?? summary.FinalAmount;
        bool isTutorOverride = dto.OverrideAmount.HasValue;

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            var departure = new StudentDeparture
            {
                TeacherId = dto.TeacherId,
                TeacherStudentId = dto.TeacherStudentId,
                SessionId = dto.SessionId,
                SessionName = summary.SessionName,
                StudentName = summary.StudentName,
                StudentCode = summary.StudentCode,
                PaymentStatusAtDeparture = summary.PaymentStatusAtDeparture,
                TotalOccurrencesInPeriod = summary.TotalOccurrencesInPeriod,
                AttendedOccurrences = summary.AttendedOccurrences,
                FullPeriodAmount = summary.FullPeriodAmount,
                ProRatedAmount = summary.ProRatedAmount,
                FinalAmount = finalAmount,
                IsTutorOverride = isTutorOverride,
                OriginalCalculatedAmount = summary.FinalAmount,
                DepartureOutcome = summary.DepartureOutcome,
                ConfirmedByUserId = dto.ConfirmedByUserId,
                DepartedAt = DateTime.UtcNow,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.PaymentsRepo.AddStudentDepartureAsync(departure);

            // REQ-PAY-073: Unassign the student from their current session
            var studentToUnassign = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
                dto.TeacherStudentId, dto.TeacherId);
            if (studentToUnassign is not null)
            {
                studentToUnassign.SessionId = null;
                // Frontend choice: also soft-delete the student (recycle bin) on departure.
                if (dto.DeleteStudent)
                {
                    studentToUnassign.IsDeleted = true;
                    studentToUnassign.DeletedAt = DateTime.UtcNow;
                }
                await _unitOfWork.Students.UpdateAsync(studentToUnassign);
            }

            // REQ-PAY-073: Record refund/outstanding in payment history
            // If refund due, record as pending refund flagged for manual settlement
            // If amount owed, record as outstanding balance flagged for collection
            if (summary.DepartureOutcome == DepartureOutcome.RefundDue && finalAmount > 0)
            {
                // Auto-refund: the refunded cash is returned to the student, so deduct it from the
                // wallet of the assistant who collected it (held cash decreases). No-op when the
                // tutor collected (they have no wallet) — the refund is then just recorded.
                var collectorUserId = await _unitOfWork.PaymentsRepo
                    .GetLatestCollectorUserIdForStudentSessionAsync(
                        dto.TeacherId, dto.TeacherStudentId, dto.SessionId);
                await AdjustAssistantWalletAsync(dto.TeacherId, collectorUserId, -finalAmount);
            }
            else if (summary.DepartureOutcome == DepartureOutcome.AmountOwed && finalAmount > 0)
            {
                // Outstanding amount tracked via departure record and counter
                var counter = await _unitOfWork.PaymentsRepo
                    .GetPaymentCounterAsync(dto.TeacherId, dto.TeacherStudentId);
                if (counter is not null)
                {
                    counter.TotalOutstanding += finalAmount;
                    await _unitOfWork.PaymentsRepo.UpdatePaymentCounterAsync(counter);
                }
            }

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<StudentDepartureDto>.Success(new StudentDepartureDto
            {
                Id = departure.Id,
                SessionName = departure.SessionName,
                StudentName = departure.StudentName,
                PaymentStatusAtDeparture = departure.PaymentStatusAtDeparture,
                TotalOccurrencesInPeriod = departure.TotalOccurrencesInPeriod,
                AttendedOccurrences = departure.AttendedOccurrences,
                FullPeriodAmount = departure.FullPeriodAmount,
                ProRatedAmount = departure.ProRatedAmount,
                FinalAmount = departure.FinalAmount,
                IsTutorOverride = departure.IsTutorOverride,
                DepartureOutcome = departure.DepartureOutcome,
                DepartedAt = departure.DepartedAt
            }, _localizer, PaymentConstants.Messages.DepartureConfirmedSuccess);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════
    // SESSION TRANSFER
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<TransferSummaryDto>> GetTransferSummaryAsync(
        long teacherId, long teacherStudentId,
        long sourceSessionId, long destinationSessionId)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
            teacherStudentId, teacherId);
        if (student is null)
            return Result<TransferSummaryDto>.Failure(
                _localizer, PaymentConstants.Messages.StudentNotFound, HttpStatusCode.NotFound);

        var sourceSession = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sourceSessionId, teacherId);
        if (sourceSession is null)
            return Result<TransferSummaryDto>.Failure(
                _localizer, PaymentConstants.Messages.TransferSourceSessionNotFound, HttpStatusCode.NotFound);

        var destSession = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(destinationSessionId, teacherId);
        if (destSession is null)
            return Result<TransferSummaryDto>.Failure(
                _localizer, PaymentConstants.Messages.TransferDestinationSessionNotFound, HttpStatusCode.NotFound);

        var counter = await _unitOfWork.PaymentsRepo
            .GetPaymentCounterAsync(teacherId, teacherStudentId);

        var currentPeriod = await _unitOfWork.PaymentsRepo
            .GetEarliestUnpaidPeriodAsync(teacherId, teacherStudentId, sourceSessionId);

        // Balance carried to the new session = the student's arrears in the SOURCE session THROUGH
        // the current month (no proration for a monthly→monthly move: 3 overdue months stay 3, a
        // paid-through student carries nothing). The all-time counter would wrongly include future
        // pre-generated months.
        var today = _timeZoneService.GetTeacherLocalDate(teacherId);
        var monthEnd = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
        decimal outstanding = await _unitOfWork.PaymentsRepo
            .GetOverdueTotalThroughAsync(teacherId, teacherStudentId, sourceSessionId, monthEnd);
        decimal credit = 0;
        if (currentPeriod is not null && currentPeriod.AmountPaid > currentPeriod.AmountDue)
            credit = currentPeriod.AmountPaid - currentPeriod.AmountDue;

        // REQ-PAY-091: If source and destination have different payment types,
        // calculate the pro-rated departure amount for the source session
        bool requiresDepartureCalc = sourceSession.PaymentType != destSession.PaymentType;
        decimal departureAdjustment = 0;
        if (requiresDepartureCalc && currentPeriod is not null
            && currentPeriod.PaymentStatus != PaymentStatus.Paid)
        {
            var departureSummary = await GetDepartureSummaryAsync(teacherId, teacherStudentId);
            if (departureSummary.IsSuccess && departureSummary.Data is not null)
            {
                departureAdjustment = departureSummary.Data.FinalAmount;
                outstanding = departureAdjustment; // Override with pro-rated amount
            }
        }

        return Result<TransferSummaryDto>.Success(new TransferSummaryDto
        {
            StudentName = student.StudentName,
            StudentCode = student.StudentCode,
            SourceSessionName = sourceSession.SessionName,
            DestinationSessionName = destSession.SessionName,
            PaymentStatusInSource = currentPeriod?.PaymentStatus ?? PaymentStatus.Paid,
            OutstandingBalance = outstanding,
            CreditBalance = credit,
            DestinationPaymentType = destSession.PaymentType.ToString(),
            DestinationSessionAmount = destSession.SessionAmount
        }, _localizer, PaymentConstants.Messages.TransferSummaryLoaded);
    }

    /// <inheritdoc />
    public async Task<Result<SessionTransferEventDto>> ConfirmTransferAsync(ConfirmTransferDto dto)
    {
        var summaryResult = await GetTransferSummaryAsync(
            dto.TeacherId, dto.TeacherStudentId,
            dto.SourceSessionId, dto.DestinationSessionId);
        if (!summaryResult.IsSuccess)
            // PAY-8: preserve the stable Code (e.g. TransferDestinationSessionNotFound).
            return Result<SessionTransferEventDto>.Failure(summaryResult);

        var summary = summaryResult.Data!;

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Resolve source session payment type before initializer (avoid await in initializer)
            var sourceSessionForType = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(
                dto.SourceSessionId, dto.TeacherId);
            string sourcePaymentType = sourceSessionForType?.PaymentType.ToString() ?? "Unknown";

            var transferEvent = new SessionTransferEvent
            {
                TeacherId = dto.TeacherId,
                TeacherStudentId = dto.TeacherStudentId,
                SourceSessionId = dto.SourceSessionId,
                SourceSessionName = summary.SourceSessionName,
                DestinationSessionId = dto.DestinationSessionId,
                DestinationSessionName = summary.DestinationSessionName,
                PaymentStatusAtTransfer = summary.PaymentStatusInSource,
                OutstandingBalance = summary.OutstandingBalance,
                CreditBalance = summary.CreditBalance,
                SourcePaymentType = sourcePaymentType,
                DestinationPaymentType = summary.DestinationPaymentType,
                StudentName = summary.StudentName,
                StudentCode = summary.StudentCode,
                TransferredAt = DateTime.UtcNow,
                TransferredByUserId = dto.TransferredByUserId,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.PaymentsRepo.AddSessionTransferEventAsync(transferEvent);

            // Create carried-forward period if outstanding balance exists
            if (summary.OutstandingBalance > 0)
            {
                var carriedPeriod = new PaymentPeriod
                {
                    TeacherId = dto.TeacherId,
                    SessionId = dto.DestinationSessionId,
                    TeacherStudentId = dto.TeacherStudentId,
                    PeriodType = PeriodType.Monthly,
                    PeriodStart = DateTime.UtcNow.Date,
                    PeriodEnd = DateTime.UtcNow.Date,
                    AmountDue = summary.OutstandingBalance,
                    PaymentStatus = PaymentStatus.Unpaid,
                    PeriodSequence = 0, // Carried-forward appears before regular periods
                    IsCarriedForward = true,
                    OriginSessionName = summary.SourceSessionName,
                    SessionName = summary.DestinationSessionName,
                    CreateAt = DateTime.UtcNow
                };
                await _unitOfWork.PaymentsRepo.AddPaymentPeriodAsync(carriedPeriod);
            }

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<SessionTransferEventDto>.Success(new SessionTransferEventDto
            {
                Id = transferEvent.Id,
                SourceSessionName = transferEvent.SourceSessionName,
                DestinationSessionName = transferEvent.DestinationSessionName,
                PaymentStatusAtTransfer = transferEvent.PaymentStatusAtTransfer,
                OutstandingBalance = transferEvent.OutstandingBalance,
                CreditBalance = transferEvent.CreditBalance,
                TransferredAt = transferEvent.TransferredAt
            }, _localizer, PaymentConstants.Messages.TransferConfirmedSuccess);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════
    // OFFLINE SYNC
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaymentSyncResultDto>> SyncOfflinePaymentsAsync(OfflinePaymentSyncRequestDto dto)
    {
        var result = new PaymentSyncResultDto();

        foreach (var offlineRecord in dto.OfflineRecords)
        {
            offlineRecord.IsOfflineRecord = true;

            // Check for conflict: same student + same day + same session
            var existing = await _unitOfWork.PaymentsRepo
                .GetSameDayTransactionsAsync(dto.TeacherId, offlineRecord.TeacherStudentId,
                    offlineRecord.OfflineCollectedAt?.Date ?? DateTime.UtcNow.Date);

            if (existing.Any(e => e.OfflineDeviceId != offlineRecord.OfflineDeviceId))
            {
                result.ConflictCount++;
                result.Conflicts.Add(new PaymentConflictDto
                {
                    OfflineRecord = offlineRecord,
                    ExistingRecord = MapToTransactionDto(existing.First()),
                    ConflictReason = "Same student paid by different user/device on same day"
                });
                continue;
            }

            offlineRecord.DuplicateConfirmed = true;
            var collectResult = await CollectPaymentAsync(offlineRecord);
            if (collectResult.IsSuccess)
                result.SyncedCount++;
        }

        // PAY-7: payment-domain messages (SyncCompleted/SyncConflictsDetected are attendance-worded —
        // "Offline attendance records synced successfully" — a shared key that misdescribes a payment sync).
        var messageKey = result.ConflictCount > 0
            ? PaymentConstants.Messages.PaymentSyncConflictsDetected
            : PaymentConstants.Messages.PaymentSyncCompleted;

        return Result<PaymentSyncResultDto>.Success(result, _localizer, messageKey);
    }

    // ══════════════════════════════════════════════
    // STUDENT/PARENT VIEW
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<StudentPaymentViewDto>> GetStudentPaymentViewAsync(
        long teacherId, long teacherStudentId)
    {
        // Check visibility settings
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        if (config is null || (!config.StudentVisibilityPayment && !config.ParentVisibilityPayment))
            return Result<StudentPaymentViewDto>.Failure(
                _localizer, PaymentConstants.Messages.PaymentVisibilityDisabled, HttpStatusCode.Forbidden);

        var periods = await _unitOfWork.PaymentsRepo
            .GetAllPaymentPeriodsByStudentAsync(teacherId, teacherStudentId);

        // Current session = the student's active (session-linked) period. Carried-forward periods
        // (SessionId null, from a transfer) are still shown in the timeline below.
        var currentSession = periods.FirstOrDefault(p => p.SessionId.HasValue)?.SessionName ?? "Unknown";

        var counter = await _unitOfWork.PaymentsRepo
            .GetPaymentCounterAsync(teacherId, teacherStudentId);

        // Outstanding = arrears THROUGH the current month (not the all-time counter, which counts
        // future pre-generated months). AmountDue mirrors it (what the student owes right now).
        var today = _timeZoneService.GetTeacherLocalDate(teacherId);
        var monthEnd = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
        decimal overdue = await _unitOfWork.PaymentsRepo
            .GetOverdueTotalThroughAsync(teacherId, teacherStudentId, null, monthEnd);

        var periodDtos = periods.Select(MapToPeriodDto).ToList();
        // Resolve each period-transaction's collector display name (batch, no N+1).
        await EnrichCollectorNamesAsync(periodDtos);

        return Result<StudentPaymentViewDto>.Success(new StudentPaymentViewDto
        {
            SessionName = currentSession,
            CurrentStatus = overdue > 0m ? PaymentStatus.Unpaid : PaymentStatus.Paid,
            AmountDue = overdue,
            AmountPaid = counter?.TotalAmountPaid ?? 0,
            Outstanding = overdue,
            Periods = periodDtos
        }, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<StudentPaymentTrackingDto>> GetStudentPaymentTrackingAsync(
        long teacherId, long teacherStudentId, PaymentViewerType viewer)
    {
        // Visibility gate — caller-specific, fail-closed when the config row is missing.
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        if (!IsPaymentVisibleTo(config, viewer))
            return Result<StudentPaymentTrackingDto>.Failure(
                _localizer, PaymentConstants.Messages.PaymentVisibilityDisabled, HttpStatusCode.Forbidden);

        // Pivot every section on the teacher's LOCAL current month (Africa/Cairo), matching the
        // rest of the payment module's month scoping (§7.4). Future pre-generated months fall
        // into Upcoming, so they're excluded from Overdue by construction.
        var today = _timeZoneService.GetTeacherLocalDate(teacherId);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var periods = await _unitOfWork.PaymentsRepo
            .GetStudentPeriodsWithTransactionsAsync(teacherId, teacherStudentId);

        var paidRows = new List<StudentPaymentPeriodDto>();
        var overdueRows = new List<StudentPaymentPeriodDto>();
        StudentPaymentPeriodDto? upcoming = null;

        foreach (var p in periods)
        {
            decimal outstanding = p.AmountDue - p.AmountPaid;

            if (outstanding <= 0m)
            {
                // Fully settled (or overpaid) — Paid section, regardless of month.
                paidRows.Add(BuildTrackingRow(p, outstanding, LatestPaidOn(p), monthsOverdue: null));
            }
            else if (p.PeriodStart <= monthEnd)
            {
                // Still owed and due on/before the current month — Overdue section.
                overdueRows.Add(BuildTrackingRow(
                    p, outstanding, paidOn: null, monthsOverdue: MonthsBetween(p.PeriodStart, monthStart)));
            }
            else if (upcoming is null || p.PeriodStart < upcoming.PeriodStartDate)
            {
                // Not fully paid and scheduled in the future — keep the NEAREST one as Upcoming.
                upcoming = BuildTrackingRow(p, outstanding, paidOn: null, monthsOverdue: null);
            }
        }

        // Paid newest-first (screen lists recent months on top); overdue oldest-first.
        paidRows = paidRows.OrderByDescending(r => r.PeriodStartDate).ToList();
        overdueRows = overdueRows.OrderBy(r => r.PeriodStartDate).ToList();

        decimal totalPaid = paidRows.Sum(r => r.AmountPaid);
        decimal totalOverdue = overdueRows.Sum(r => r.OutstandingAmount);
        decimal denominator = totalPaid + totalOverdue;
        decimal paidRatio = denominator > 0m ? Math.Round(totalPaid / denominator, 4) : 1m;

        // Header session = the student's most-recent session-linked period.
        var headerPeriod = periods
            .Where(p => p.SessionId.HasValue)
            .OrderByDescending(p => p.PeriodStart)
            .FirstOrDefault();

        var dto = new StudentPaymentTrackingDto
        {
            TeacherId = teacherId,
            CurrentSessionId = headerPeriod?.SessionId,
            CurrentSessionName = headerPeriod?.SessionName,
            PaidProgressRatio = paidRatio,
            UpcomingPayment = upcoming,
            PaidSection = new PaymentSectionDto
            {
                TotalAmount = totalPaid,
                PeriodCount = paidRows.Count,
                Periods = paidRows
            },
            OverdueSection = new PaymentSectionDto
            {
                TotalAmount = totalOverdue,
                PeriodCount = overdueRows.Count,
                Periods = overdueRows
            }
        };

        return Result<StudentPaymentTrackingDto>.Success(
            dto, _localizer, PaymentConstants.Messages.Success);
    }

    /// <summary>
    /// Caller-specific payment visibility, fail-closed on missing config.
    /// Mirrors <c>AttendanceService.IsAttendanceVisibleTo</c>.
    /// </summary>
    private static bool IsPaymentVisibleTo(TeacherConfiguration? config, PaymentViewerType viewer)
    {
        if (config is null) return false;
        return viewer switch
        {
            PaymentViewerType.Student => config.StudentVisibilityPayment,
            PaymentViewerType.Parent => config.ParentVisibilityPayment,
            _ => false
        };
    }

    /// <summary>Maps a period to a tracking-screen row with explicitly-named amounts.</summary>
    private static StudentPaymentPeriodDto BuildTrackingRow(
        PaymentPeriod p, decimal outstanding, DateTime? paidOn, int? monthsOverdue) => new()
    {
        PeriodId = p.Id,
        PeriodStartDate = p.PeriodStart,
        SessionName = p.SessionName,
        PeriodType = p.PeriodType,
        Status = p.PaymentStatus,
        AmountDue = p.AmountDue,
        AmountPaid = p.AmountPaid,
        OutstandingAmount = outstanding,
        PaidOnDate = paidOn,
        MonthsOverdue = monthsOverdue,
        IsProRated = p.IsProRated,
        IsCarriedForward = p.IsCarriedForward
    };

    /// <summary>Latest non-deleted transaction's local collection date for a period, or null.</summary>
    private static DateTime? LatestPaidOn(PaymentPeriod p) =>
        p.PaymentTransactions
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.LocalCollectedAt)
            .Select(t => (DateTime?)t.LocalCollectedAt)
            .FirstOrDefault();

    /// <summary>Whole months from a period's start month to the current month (never negative).</summary>
    private static int MonthsBetween(DateTime periodStart, DateTime currentMonthStart)
    {
        int months = ((currentMonthStart.Year - periodStart.Year) * 12)
            + (currentMonthStart.Month - periodStart.Month);
        return months < 0 ? 0 : months;
    }

    // ══════════════════════════════════════════════
    // INTEGRATION HOOKS
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<bool>> OnStudentAssignedToSessionAsync(
        long teacherId, long teacherStudentId, long sessionId,
        string sessionName, DateTime assignedAt)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
            teacherStudentId, teacherId);
        if (student is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.StudentNotFound, HttpStatusCode.NotFound);

        // Create or retrieve counter
        var counter = await _unitOfWork.PaymentsRepo
            .GetPaymentCounterAsync(teacherId, teacherStudentId);
        if (counter is null)
        {
            counter = new StudentPaymentCounter
            {
                TeacherId = teacherId,
                TeacherStudentId = teacherStudentId,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.PaymentsRepo.AddPaymentCounterAsync(counter);
        }

        // Generate initial payment periods
        var periods = new List<PaymentPeriod>();
        int sequence = await _unitOfWork.PaymentsRepo
            .GetMaxPeriodSequenceAsync(teacherId, teacherStudentId, sessionId) + 1;

        if (session.PaymentType == PaymentType.Monthly)
        {
            // Generate monthly periods from assignment month to session end
            var startMonth = new DateTime(assignedAt.Year, assignedAt.Month, 1);
            var endMonth = new DateTime(session.EndDate.Year, session.EndDate.Month, 1);

            // Check pro-rating for first month
            bool applyProRate = false;
            decimal proRateFraction = 1.0m;
            var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);

            if (config?.IsProratedPaymentEnabled == true)
            {
                var tiers = await _unitOfWork.Users.GetProratedTiersByConfigIdAsync(config.Id);

                int joinDay = assignedAt.Day;
                var matchingTier = tiers
                    .OrderBy(t => t.TierNumber)
                    .FirstOrDefault(t => joinDay >= t.ThresholdDayStart && joinDay <= t.ThresholdDayEnd);

                if (matchingTier is not null && matchingTier.FractionRate < 1.0m)
                {
                    applyProRate = true;
                    proRateFraction = matchingTier.FractionRate;
                }
            }

            decimal baseAmount = counter.CustomPaymentAmount ?? session.SessionAmount;

            for (var month = startMonth; month <= endMonth; month = month.AddMonths(1))
            {
                bool isFirstPeriod = month == startMonth && applyProRate;
                decimal periodAmount = isFirstPeriod
                    ? Math.Round(baseAmount * proRateFraction, 2)
                    : baseAmount;

                periods.Add(new PaymentPeriod
                {
                    TeacherId = teacherId,
                    SessionId = sessionId,
                    TeacherStudentId = teacherStudentId,
                    PeriodType = PeriodType.Monthly,
                    PeriodStart = month,
                    PeriodEnd = month.AddMonths(1).AddDays(-1),
                    AmountDue = periodAmount,
                    PaymentStatus = PaymentStatus.Unpaid,
                    IsProRated = isFirstPeriod,
                    ProRatedFraction = isFirstPeriod ? proRateFraction : 1.0m,
                    PeriodSequence = sequence++,
                    SessionName = sessionName,
                    StudentName = student.StudentName,
                    StudentCode = student.StudentCode,
                    CreateAt = DateTime.UtcNow
                });
            }
        }
        else // PerSession
        {
            // Generate per-occurrence periods from assignment date
            var occurrences = await _unitOfWork.AttendanceRepo
                .GetOccurrencesBySessionAsync(sessionId);
            decimal baseAmount = counter.CustomPaymentAmount ?? session.SessionAmount;

            foreach (var occ in occurrences.Where(o => o.OccurrenceDate >= assignedAt.Date))
            {
                periods.Add(new PaymentPeriod
                {
                    TeacherId = teacherId,
                    SessionId = sessionId,
                    TeacherStudentId = teacherStudentId,
                    PeriodType = PeriodType.PerSession,
                    PeriodStart = occ.OccurrenceDate,
                    PeriodEnd = occ.OccurrenceDate,
                    AmountDue = baseAmount,
                    PaymentStatus = PaymentStatus.Unpaid,
                    PeriodSequence = sequence++,
                    SessionName = sessionName,
                    StudentName = student.StudentName,
                    StudentCode = student.StudentCode,
                    CreateAt = DateTime.UtcNow
                });
            }
        }

        if (periods.Count > 0)
        {
            await _unitOfWork.PaymentsRepo.AddPaymentPeriodsRangeAsync(periods);

            // Update counter aggregates
            counter.TotalOutstanding += periods.Sum(p => p.AmountDue);
            counter.TotalUnpaidPeriods += periods.Count;
            await _unitOfWork.PaymentsRepo.UpdatePaymentCounterAsync(counter);
        }

        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> OnStudentUnassignedFromSessionAsync(
        long teacherId, long teacherStudentId)
    {
        // Payment history is preserved — no deletion needed
        // Only mark current periods as historical
        return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> OnSessionDeletingAsync(long teacherId, long sessionId)
    {
        await _unitOfWork.PaymentsRepo.NullifySessionIdOnPaymentRecordsAsync(sessionId);
        await _unitOfWork.SaveChangesAsync();
        return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> OnStudentPermanentlyDeletedAsync(long teacherStudentId)
    {
        await _unitOfWork.PaymentsRepo.NullifyStudentReferencesOnPaymentRecordsAsync(teacherStudentId);
        await _unitOfWork.SaveChangesAsync();
        return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);
    }

    // ══════════════════════════════════════════════
    // ASSISTANT WALLET PROVISIONING
    // ══════════════════════════════════════════════

    /// <summary>
    /// Ensures an AssistantWallet record exists for the given assistant.
    /// Should be called when an assistant is created (from AssistantService)
    /// or as a safety check during payment collection.
    /// Creates the wallet if it doesn't already exist.
    ///
    /// INTEGRATION NOTE: Call this from AssistantService.CreateAssistantAsync
    /// after the Assistant entity is persisted and SaveChangesAsync is called.
    ///
    /// Example:
    ///   await _paymentService.EnsureAssistantWalletExistsAsync(teacherId, assistant.Id, assistant.UserId);
    /// </summary>
    public async Task<Result<bool>> EnsureAssistantWalletExistsAsync(
        long teacherId, long assistantId, long assistantUserId)
    {
        var existingWallet = await _unitOfWork.PaymentsRepo
            .GetAssistantWalletAsync(teacherId, assistantId);
        if (existingWallet is not null)
            return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);

        var wallet = new AssistantWallet
        {
            TeacherId = teacherId,
            AssistantId = assistantId,
            AssistantUserId = assistantUserId,
            CreateAt = DateTime.UtcNow
        };
        await _unitOfWork.PaymentsRepo.AddAssistantWalletAsync(wallet);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);
    }

    // ══════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════

    /// <summary>
    /// PAY-1: reverses <paramref name="amountToReverse"/> of a transaction's settled cash from the
    /// EXACT periods it cleared, newest period first (LIFO), keeping the per-period ledger and the
    /// period states coherent. Each reversed slice is deducted from its period's AmountPaid (clamped
    /// ≥ 0), the period status recomputed, and the allocation reduced — fully-consumed allocation
    /// rows are removed. For legacy transactions with no ledger (collected before PAY-1), falls back
    /// to reversing against the single denormalized <see cref="PaymentTransaction.PaymentPeriodId"/>.
    /// The caller owns counter/wallet reversal (driven by the cash amount, not the ledger).
    /// </summary>
    private async Task ReversePeriodAllocationsAsync(PaymentTransaction transaction, decimal amountToReverse)
    {
        if (amountToReverse <= 0m) return;

        var allocations = await _unitOfWork.PaymentsRepo.GetAllocationsByTransactionAsync(transaction.Id);
        if (allocations.Count == 0)
        {
            // Legacy fallback — the best achievable without per-period settlement data.
            await ReverseSinglePeriodAsync(transaction.PaymentPeriodId, amountToReverse);
            return;
        }

        decimal remaining = amountToReverse;
        var consumed = new List<PaymentTransactionAllocation>();
        foreach (var alloc in allocations
            .OrderByDescending(a => a.PaymentPeriod?.PeriodStart ?? DateTime.MinValue))
        {
            if (remaining <= 0m) break;
            decimal take = Math.Min(alloc.AmountApplied, remaining);
            if (take <= 0m) continue;

            var period = alloc.PaymentPeriod;
            if (period is not null)
            {
                period.AmountPaid -= take;
                if (period.AmountPaid < 0m) period.AmountPaid = 0m;
                period.PaymentStatus = RecomputePeriodStatus(period);
                await _unitOfWork.PaymentsRepo.UpdatePaymentPeriodAsync(period);
            }

            alloc.AmountApplied -= take;
            remaining -= take;
            if (alloc.AmountApplied <= 0m) consumed.Add(alloc);
        }

        if (consumed.Count > 0)
            await _unitOfWork.PaymentsRepo.RemovePaymentTransactionAllocationsAsync(consumed);
    }

    /// <summary>
    /// Legacy reversal path (no allocation ledger): deduct the amount from the transaction's single
    /// denormalized period and recompute its status.
    /// </summary>
    private async Task ReverseSinglePeriodAsync(long? paymentPeriodId, decimal amountToReverse)
    {
        if (paymentPeriodId is null || amountToReverse <= 0m) return;
        var period = await _unitOfWork.PaymentsRepo.GetPaymentPeriodByIdAsync(paymentPeriodId.Value);
        if (period is null) return;

        period.AmountPaid -= amountToReverse;
        if (period.AmountPaid < 0m) period.AmountPaid = 0m;
        period.PaymentStatus = RecomputePeriodStatus(period);
        await _unitOfWork.PaymentsRepo.UpdatePaymentPeriodAsync(period);
    }

    /// <summary>
    /// PAY-1: applies additional cash (an amount increase on edit) forward across the student's
    /// still-unpaid periods, oldest first — exactly like the collection cascade — extending the
    /// transaction's allocation ledger. A period the transaction already funds is topped up on its
    /// existing allocation row; a newly-reached period gets a fresh one. Monthly caps each month at
    /// its remaining due; per-session fills its single period. Any surplus beyond the payable window
    /// stays as unattributed cash (the transaction's AmountPaid still reflects it), matching the
    /// legacy edit's tolerance of an over-target amount. Falls back to topping up the single
    /// denormalized period when the student/session context is gone (purged/hard-deleted).
    /// </summary>
    private async Task ApplyForwardAllocationsAsync(
        PaymentTransaction transaction, decimal surplus, DateTime localDate)
    {
        if (surplus <= 0m) return;

        if (transaction.TeacherStudentId is null || transaction.SessionId is null)
        {
            await TopUpSinglePeriodAsync(transaction, surplus);
            return;
        }

        long teacherId = transaction.TeacherId;
        long studentId = transaction.TeacherStudentId.Value;
        long sessionId = transaction.SessionId.Value;

        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        bool isMonthly = session?.PaymentType == PaymentType.Monthly;

        List<PaymentPeriod> payablePeriods;
        if (isMonthly)
        {
            var currentMonthStart = new DateTime(localDate.Year, localDate.Month, 1);
            var advanceCapEnd = currentMonthStart.AddMonths(2).AddDays(-1);
            payablePeriods = await _unitOfWork.PaymentsRepo
                .GetUnpaidPeriodsThroughAsync(teacherId, studentId, sessionId, advanceCapEnd);
        }
        else
        {
            var earliest = await _unitOfWork.PaymentsRepo
                .GetEarliestUnpaidPeriodAsync(teacherId, studentId, sessionId);
            payablePeriods = earliest is null
                ? new List<PaymentPeriod>()
                : new List<PaymentPeriod> { earliest };
        }

        // Existing allocations keyed by period, so a top-up increments the same row instead of
        // inserting a duplicate (guarded by UX_PTA_Transaction_Period). Tracked fetches share EF's
        // identity map, so an overlapping period is the SAME instance as the payable one below.
        var existing = (await _unitOfWork.PaymentsRepo.GetAllocationsByTransactionAsync(transaction.Id))
            .Where(a => a.PaymentPeriodId.HasValue)
            .ToDictionary(a => a.PaymentPeriodId!.Value);

        decimal amountLeft = surplus;
        var newAllocations = new List<PaymentTransactionAllocation>();
        foreach (var p in payablePeriods)
        {
            if (amountLeft <= 0m) break;
            decimal remaining = p.AmountDue - p.AmountPaid;
            if (remaining <= 0m) continue;

            decimal apply = isMonthly ? Math.Min(amountLeft, remaining) : amountLeft;
            p.AmountPaid += apply;
            amountLeft -= apply;
            p.PaymentStatus = RecomputePeriodStatus(p);
            await _unitOfWork.PaymentsRepo.UpdatePaymentPeriodAsync(p);

            if (existing.TryGetValue(p.Id, out var alloc))
                alloc.AmountApplied += apply; // tracked → auto-detected on save
            else
                newAllocations.Add(new PaymentTransactionAllocation
                {
                    PaymentTransaction = transaction,
                    PaymentPeriodId = p.Id,
                    TeacherId = teacherId,
                    AmountApplied = apply,
                    CreateAt = DateTime.UtcNow
                });
        }

        if (newAllocations.Count > 0)
            await _unitOfWork.PaymentsRepo.AddPaymentTransactionAllocationsRangeAsync(newAllocations);
    }

    /// <summary>
    /// Forward-application fallback (no live student/session): add the surplus onto the transaction's
    /// single denormalized period and its allocation row (created if absent).
    /// </summary>
    private async Task TopUpSinglePeriodAsync(PaymentTransaction transaction, decimal surplus)
    {
        if (transaction.PaymentPeriodId is null || surplus <= 0m) return;
        var period = await _unitOfWork.PaymentsRepo
            .GetPaymentPeriodByIdAsync(transaction.PaymentPeriodId.Value);
        if (period is null) return;

        period.AmountPaid += surplus;
        period.PaymentStatus = RecomputePeriodStatus(period);
        await _unitOfWork.PaymentsRepo.UpdatePaymentPeriodAsync(period);

        var existing = (await _unitOfWork.PaymentsRepo.GetAllocationsByTransactionAsync(transaction.Id))
            .FirstOrDefault(a => a.PaymentPeriodId == period.Id);
        if (existing is not null)
            existing.AmountApplied += surplus;
        else
            await _unitOfWork.PaymentsRepo.AddPaymentTransactionAllocationsRangeAsync(
                new[]
                {
                    new PaymentTransactionAllocation
                    {
                        PaymentTransaction = transaction,
                        PaymentPeriodId = period.Id,
                        TeacherId = transaction.TeacherId,
                        AmountApplied = surplus,
                        CreateAt = DateTime.UtcNow
                    }
                });
    }

    /// <summary>
    /// Derives a period's status from its paid-vs-due amounts: fully covered → Paid, some cash →
    /// PartiallyPaid, none → Unpaid.
    /// </summary>
    private static PaymentStatus RecomputePeriodStatus(PaymentPeriod period) =>
        period.AmountPaid >= period.AmountDue
            ? PaymentStatus.Paid
            : period.AmountPaid > 0m
                ? PaymentStatus.PartiallyPaid
                : PaymentStatus.Unpaid;

    private async Task UpdatePaymentCounterAfterCollectionAsync(
        long teacherId, long teacherStudentId, decimal amount,
        string sessionName, int periodsNewlyPaid)
    {
        for (int retry = 0; retry < PaymentConstants.MaxConcurrencyRetries; retry++)
        {
            try
            {
                var counter = await _unitOfWork.PaymentsRepo
                    .GetPaymentCounterAsync(teacherId, teacherStudentId);
                if (counter is null) return;

                counter.TotalAmountPaid += amount;
                counter.TotalOutstanding -= amount;
                if (counter.TotalOutstanding < 0) counter.TotalOutstanding = 0;
                counter.LastPaymentDate = DateTime.UtcNow;
                counter.LastPaymentSessionName = sessionName;

                // A single collection can clear several months at once (cascade), so advance the
                // paid/unpaid period counters by the number of months this payment fully settled.
                if (periodsNewlyPaid > 0)
                {
                    counter.TotalPaidPeriods += periodsNewlyPaid;
                    counter.TotalUnpaidPeriods = Math.Max(0, counter.TotalUnpaidPeriods - periodsNewlyPaid);
                    counter.ConsecutiveUnpaid = 0; // BR-PAY-006: Reset on payment
                }

                await _unitOfWork.PaymentsRepo.UpdatePaymentCounterAsync(counter);
                return;
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException"
                && retry < PaymentConstants.MaxConcurrencyRetries - 1)
            {
                // Retry on concurrency conflict
            }
        }
    }

    private async Task UpdateAssistantWalletAfterCollectionAsync(
        long teacherId, long collectedByUserId, decimal amount)
    {
        var wallet = await _unitOfWork.PaymentsRepo
            .GetAssistantWalletByUserIdAsync(teacherId, collectedByUserId);
        if (wallet is null) return; // Not an assistant — no wallet to update

        for (int retry = 0; retry < PaymentConstants.MaxConcurrencyRetries; retry++)
        {
            try
            {
                wallet.CurrentBalance += amount;
                wallet.TotalCollected += amount;
                wallet.TransactionCount++;
                wallet.LastCollectionAt = DateTime.UtcNow;
                await _unitOfWork.PaymentsRepo.UpdateAssistantWalletAsync(wallet);
                return;
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException"
                && retry < PaymentConstants.MaxConcurrencyRetries - 1)
            {
                wallet = await _unitOfWork.PaymentsRepo
                    .GetAssistantWalletByUserIdAsync(teacherId, collectedByUserId);
                if (wallet is null) return;
            }
        }
    }

    /// <summary>
    /// Adjusts the collecting assistant's wallet by <paramref name="delta"/> (negative to reverse).
    /// Used when a collected payment is edited or refunded/deleted so the wallet's held-cash balance
    /// stays correct. No-op when the collector is not an assistant (e.g. the teacher) or delta is 0.
    /// Balances are clamped at 0. Mirrors <see cref="UpdateAssistantWalletAfterCollectionAsync"/>.
    /// </summary>
    private async Task AdjustAssistantWalletAsync(long teacherId, long? collectedByUserId, decimal delta)
    {
        if (collectedByUserId is null || delta == 0m) return;

        var wallet = await _unitOfWork.PaymentsRepo
            .GetAssistantWalletByUserIdAsync(teacherId, collectedByUserId.Value);
        if (wallet is null) return; // collector is not an assistant — no wallet to adjust

        for (int retry = 0; retry < PaymentConstants.MaxConcurrencyRetries; retry++)
        {
            try
            {
                wallet.CurrentBalance += delta;
                if (wallet.CurrentBalance < 0m) wallet.CurrentBalance = 0m;
                wallet.TotalCollected += delta;
                if (wallet.TotalCollected < 0m) wallet.TotalCollected = 0m;
                await _unitOfWork.PaymentsRepo.UpdateAssistantWalletAsync(wallet);
                return;
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException"
                && retry < PaymentConstants.MaxConcurrencyRetries - 1)
            {
                wallet = await _unitOfWork.PaymentsRepo
                    .GetAssistantWalletByUserIdAsync(teacherId, collectedByUserId.Value);
                if (wallet is null) return;
            }
        }
    }

    private async Task<long?> GetOccurrenceIdForPeriodAsync(PaymentPeriod period)
    {
        if (period.SessionId is null) return null;
        var occurrence = await _unitOfWork.AttendanceRepo
            .GetOccurrenceBySessionAndDateAsync(period.SessionId.Value, period.PeriodStart);
        return occurrence?.Id;
    }

    private static PaymentTransactionDto MapToTransactionDto(PaymentTransaction t) => new()
    {
        Id = t.Id,
        TeacherStudentId = t.TeacherStudentId,
        StudentName = t.StudentName,
        StudentCode = t.StudentCode,
        SessionName = t.SessionName,
        SessionId = t.SessionId,
        AmountDue = t.AmountDue,
        AmountPaid = t.AmountPaid,
        PaymentMethod = t.PaymentMethod,
        PaymentTransactionStatus = t.PaymentTransactionStatus,
        CollectedByUserId = t.CollectedByUserId,
        CollectedAt = t.CollectedAt,
        LocalCollectedAt = t.LocalCollectedAt,
        IsPartial = t.IsPartial,
        IsProRated = t.IsProRated,
        ProRatedTierLabel = t.ProRatedTierLabel,
        IsOnlinePayment = t.IsOnlinePayment,
        OnlineTransactionRef = t.OnlineTransactionRef
    };

    /// <summary>
    /// Fills <see cref="PaymentTransactionDto.CollectedByUserName"/> on every transaction across the
    /// given periods, batch-resolving collector user ids → full names in ONE query (no N+1). The
    /// collector id itself is already set by <see cref="MapToTransactionDto"/>; this only adds the
    /// display name so a per-period "Collected by" can render a name instead of a bare id.
    /// </summary>
    private async Task EnrichCollectorNamesAsync(IEnumerable<PaymentPeriodDto> periods)
    {
        var transactions = periods.SelectMany(p => p.Transactions).ToList();
        var collectorIds = transactions
            .Where(t => t.CollectedByUserId.HasValue)
            .Select(t => t.CollectedByUserId!.Value)
            .Distinct()
            .ToList();
        if (collectorIds.Count == 0) return;

        var names = await _unitOfWork.Users.GetUserFullNamesByUserIdsAsync(collectorIds);
        foreach (var t in transactions)
            if (t.CollectedByUserId.HasValue && names.TryGetValue(t.CollectedByUserId.Value, out var name))
                t.CollectedByUserName = name;
    }

    /// <summary>
    /// Fills <see cref="PaymentTransactionDto.CollectedByUserName"/> on a single transaction (e.g. the
    /// collect/edit receipt), resolving the collector id → full name. No-op when the collector is unset.
    /// </summary>
    private async Task EnrichCollectorNameAsync(PaymentTransactionDto? transaction)
    {
        if (transaction?.CollectedByUserId is not long collectorId) return;
        var names = await _unitOfWork.Users.GetUserFullNamesByUserIdsAsync(new List<long> { collectorId });
        if (names.TryGetValue(collectorId, out var name))
            transaction.CollectedByUserName = name;
    }

    private static PaymentPeriodDto MapToPeriodDto(PaymentPeriod p) => new()
    {
        Id = p.Id,
        SessionName = p.SessionName,
        PeriodType = p.PeriodType,
        PeriodStart = p.PeriodStart,
        PeriodEnd = p.PeriodEnd,
        AmountDue = p.AmountDue,
        AmountPaid = p.AmountPaid,
        PaymentStatus = p.PaymentStatus,
        IsProRated = p.IsProRated,
        ProRatedFraction = p.ProRatedFraction,
        PeriodSequence = p.PeriodSequence,
        IsCarriedForward = p.IsCarriedForward,
        OriginSessionName = p.OriginSessionName,
        // Surface the period's collection(s), including the collector id, so a per-period view can
        // show "Collected by". Requires the caller to have eager-loaded PaymentTransactions; a
        // period whose transactions were not loaded maps to an empty list (unchanged behaviour).
        // The collector NAME (CollectedByUserName) is resolved separately by EnrichCollectorNamesAsync.
        Transactions = (p.PaymentTransactions ?? new List<PaymentTransaction>())
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.CollectedAt)
            .Select(MapToTransactionDto)
            .ToList()
    };

    private static string FormatPeriodLabel(PaymentPeriod period) =>
        period.PeriodType == PeriodType.Monthly
            ? period.PeriodStart.ToString("MMMM yyyy")
            : period.PeriodStart.ToString("dd MMM yyyy");

    private static string FormatPeriodLabel(DateTime start, DateTime end) =>
        start == end
            ? start.ToString("dd MMM yyyy")
            : $"{start:MMMM yyyy}";

    // ══════════════════════════════════════════════
    // BATCH EDIT (UI: "Saved N changes" — D2)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<BatchEditResultDto>> BatchEditPaymentAsync(
        BatchEditPaymentDto dto, long teacherId, long editedByUserId)
    {
        if (dto.Items is null || dto.Items.Count == 0)
            return Result<BatchEditResultDto>.Failure(
                _localizer, PaymentConstants.Messages.PaymentBatchEmpty, HttpStatusCode.BadRequest);

        var results = new List<BatchEditItemResultDto>(dto.Items.Count);

        foreach (var item in dto.Items)
        {
            // Fan the per-item edit fields + JWT-resolved identity onto a single EditPaymentDto.
            // EditPaymentAsync is the single source of truth for edit business rules
            // (PaymentEditLog, period/counter reversal, transaction ownership).
            var itemDto = new EditPaymentDto
            {
                TeacherId = teacherId,             // JWT — never from body (BR-PAY-002)
                EditedByUserId = editedByUserId,   // JWT — never from body
                TransactionId = item.TransactionId,
                NewAmount = item.NewAmount,
                NewStatus = item.NewStatus,
                NewPaymentPeriodId = item.NewPaymentPeriodId,
                EditReason = item.EditReason
            };

            // Each item runs as its own owned transaction inside EditPaymentAsync
            // (ownsTransaction = true, since the batch opens no ambient transaction):
            // best-effort — one item's failure never rolls back the others.
            try
            {
                var itemResult = await EditPaymentAsync(itemDto);
                results.Add(MapBatchEditItemResult(item.TransactionId, itemResult));
            }
            catch (OperationCanceledException)
            {
                // Cancellation is not a per-item failure — never swallow it. Abort the whole batch.
                throw;
            }
            catch (Exception ex)
            {
                // Reaching here means a genuinely exceptional (non-business) fault for THIS item —
                // business failures come back as Result.Failure, not thrown. EditPaymentAsync has
                // already rolled back its own owned transaction. Log with context, then record a
                // per-item failure so one bad record can't abort the batch (best-effort contract).
                _logger.LogError(ex,
                    "Batch edit: unexpected fault editing transaction {TransactionId} for teacher {TeacherId}.",
                    item.TransactionId, teacherId);

                results.Add(new BatchEditItemResultDto
                {
                    TransactionId = item.TransactionId,
                    Status = BatchEditItemStatus.Failed,
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Message = _localizer["ServerError"]
                });
            }
        }

        var envelope = new BatchEditResultDto
        {
            TotalRequested = dto.Items.Count,
            SucceededCount = results.Count(r => r.Status == BatchEditItemStatus.Succeeded),
            FailedCount = results.Count(r => r.Status == BatchEditItemStatus.Failed),
            Results = results
        };

        return Result<BatchEditResultDto>.Success(
            envelope, _localizer, PaymentConstants.Messages.PaymentBatchEditSuccess);
    }

    /// <summary>
    /// Classifies a single EditPaymentAsync result into a batch item outcome.
    /// result.Message is already localized by EditPaymentAsync, so no localizer is needed here.
    /// </summary>
    private static BatchEditItemResultDto MapBatchEditItemResult(
        long transactionId, Result<PaymentTransactionDto> result)
    {
        if (!result.IsSuccess)
            return new BatchEditItemResultDto
            {
                TransactionId = transactionId,
                Status = BatchEditItemStatus.Failed,
                StatusCode = (int)result.StatusCode, // 400 = validation, 404 = not found/not owned
                Message = result.Message
            };

        return new BatchEditItemResultDto
        {
            TransactionId = transactionId,
            Status = BatchEditItemStatus.Succeeded,
            StatusCode = (int)result.StatusCode,
            Message = result.Message,
            Transaction = result.Data
        };
    }


    // ══════════════════════════════════════════════
    // BATCH REVERT (UI: "Revert (N students)" — D1)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<BatchRevertResultDto>> BatchRevertPaymentAsync(
        BatchRevertPaymentDto dto, long teacherId, long editedByUserId)
    {
        if (dto.TransactionIds is null || dto.TransactionIds.Count == 0)
            return Result<BatchRevertResultDto>.Failure(
                _localizer, PaymentConstants.Messages.PaymentBatchEmpty, HttpStatusCode.BadRequest);

        var results = new List<BatchRevertItemResultDto>(dto.TransactionIds.Count);

        foreach (var transactionId in dto.TransactionIds)
        {
            // A revert is an edit that zeroes the collected amount and marks the transaction
            // Unpaid — reusing EditPaymentAsync verbatim (single source of truth for PaymentEditLog,
            // period reversal, counter reversal, ownership, transaction ownership). NewAmount = 0
            // drives the amountDiff branch that reverses period + counter; NewStatus keeps the
            // reverted row coherent instead of leaving a stale Paid status. No new reversal logic.
            var itemDto = new EditPaymentDto
            {
                TeacherId = teacherId,              // JWT — never from body (BR-PAY-002)
                EditedByUserId = editedByUserId,    // JWT — never from body
                TransactionId = transactionId,
                NewAmount = 0m,
                NewStatus = PaymentStatus.Unpaid,
                EditReason = dto.Reason             // shared revert reason → recorded on each PaymentEditLog
            };

            try
            {
                var itemResult = await EditPaymentAsync(itemDto);
                results.Add(MapBatchRevertItemResult(transactionId, itemResult));
            }
            catch (Exception)
            {
                // EditPaymentAsync rolls back its own owned transaction then rethrows genuinely
                // exceptional (non-business) errors. Capture as a per-item failure so one bad
                // record can't abort the batch.
                // NOTE: no ILogger is injected into PaymentService; if one is added, log here.
                results.Add(new BatchRevertItemResultDto
                {
                    TransactionId = transactionId,
                    Status = BatchEditItemStatus.Failed,
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Message = _localizer["ServerError"] // existing global resource key
                });
            }
        }

        var envelope = new BatchRevertResultDto
        {
            TotalRequested = dto.TransactionIds.Count,
            RevertedCount = results.Count(r => r.Status == BatchEditItemStatus.Succeeded),
            FailedCount = results.Count(r => r.Status == BatchEditItemStatus.Failed),
            Results = results
        };

        return Result<BatchRevertResultDto>.Success(
            envelope, _localizer, PaymentConstants.Messages.PaymentBatchRevertSuccess);
    }

    /// <summary>
    /// Classifies a single EditPaymentAsync result into a batch revert item outcome.
    /// result.Message is already localized by EditPaymentAsync, so no localizer is needed here.
    /// </summary>
    private static BatchRevertItemResultDto MapBatchRevertItemResult(
        long transactionId, Result<PaymentTransactionDto> result)
    {
        if (!result.IsSuccess)
            return new BatchRevertItemResultDto
            {
                TransactionId = transactionId,
                Status = BatchEditItemStatus.Failed,
                StatusCode = (int)result.StatusCode, // 404 = not found/not owned
                Message = result.Message
            };

        return new BatchRevertItemResultDto
        {
            TransactionId = transactionId,
            Status = BatchEditItemStatus.Succeeded,
            StatusCode = (int)result.StatusCode,
            Message = result.Message,
            Transaction = result.Data
        };
    }
}