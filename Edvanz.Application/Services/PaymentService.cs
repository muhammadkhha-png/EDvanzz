using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Payment;
using Edvanz.Application.Extensions;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Globalization;
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

    // Shared roster teardown (unassign + END the student ACCOUNT link) used when a departure is
    // confirmed with DeleteStudent. Safe to inject: IStudentTeardownService depends only on
    // IUnitOfWork/IStudentLinkNotifier/IStringLocalizer, so there is NO cycle (TeacherStudentService
    // → IPaymentService already exists, which is why this must never be ITeacherStudentService).
    private readonly IStudentTeardownService _studentTeardown;

    public PaymentService(
        IUnitOfWork unitOfWork,
        IStringLocalizer<Domain.Resources.Messages> localizer,
        ITimeZoneService timeZoneService,
        IPaymentNotifier paymentNotifier, ILogger<PaymentService> logger,                            // ← Phase 4
        IStudentTeardownService studentTeardown)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _timeZoneService = timeZoneService;
        _paymentNotifier = paymentNotifier;
        _logger = logger;// ← Phase 4
        _studentTeardown = studentTeardown;
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
                // Forgiven amount is already settled (not owed), so it never needs cash.
                decimal remaining = p.AmountDue - p.AmountPaid - (p.ForgivenAmount ?? 0m);
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

                // A month is settled when paid + forgiven covers it (forgiveness reduces what's owed).
                p.PaymentStatus = p.AmountPaid + (p.ForgivenAmount ?? 0m) >= p.AmountDue
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
                CollectionNote = string.IsNullOrWhiteSpace(dto.CollectionNote) ? null : dto.CollectionNote.Trim(),
                IsOfflineRecord = dto.IsOfflineRecord,
                OfflineDeviceId = dto.OfflineDeviceId,
                ClientEntryId = dto.IsOfflineRecord ? dto.ClientEntryId : null,
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

        // Feature B: effective audit note — the new `note` field, else the legacy EditReason.
        string? effectiveNote = string.IsNullOrWhiteSpace(dto.Note) ? dto.EditReason : dto.Note;

        // Feature B (single-edit path only): a note is REQUIRED when editing to a partial/custom
        // amount — one that is NOT a whole-month multiple of the student's monthly rate. A whole-month
        // multiple (N × rate) is a plain "pay N months" and needs no note. Batch edit/revert do not set
        // EnforceNoteOnPartial, so their behaviour is unchanged.
        if (dto.EnforceNoteOnPartial && dto.NewAmount.HasValue)
        {
            decimal monthlyRate = transaction.TeacherStudentId.HasValue
                ? await _unitOfWork.PaymentsRepo
                    .GetStudentMonthlyRateAsync(dto.TeacherId, transaction.TeacherStudentId.Value)
                : 0m;
            if (!Extensions.PaymentAmountRules.IsWholeMonthMultiple(dto.NewAmount.Value, monthlyRate)
                && string.IsNullOrWhiteSpace(effectiveNote))
                return Result<PaymentTransactionDto>.Failure(
                    _localizer, PaymentConstants.Messages.EditNoteRequired, HttpStatusCode.BadRequest);
        }

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
                // Feature B: store the note (new `note` field, falling back to legacy EditReason) on the
                // EditReason column; returned per edit in the edit-logs endpoint.
                EditReason = effectiveNote,
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

                // Keep the collecting assistant's wallet in sync with the amount change. Reset-aware:
                // an edit-DOWN of cash already handed over (collected before the last reset) must not
                // drive the wallet negative — pass the reversed transaction's collection instant.
                await AdjustAssistantWalletAsync(
                    dto.TeacherId, transaction.CollectedByUserId, amountDiff, transaction.CollectedAt);
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
            // Reset-aware: deleting a payment collected before the last hand-over (already given to the
            // tutor) must NOT drive the wallet negative (the salma −2700 bug) — pass its collection instant.
            await AdjustAssistantWalletAsync(
                teacherId, transaction.CollectedByUserId, -transaction.AmountPaid, transaction.CollectedAt);

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

        // Forgiveness timeline (any status), newest first — surfaced as "Forgiven" entries. Resolve the
        // forgiving tutor's display name in ONE batch query (no N+1).
        var forgivenesses = await _unitOfWork.PaymentsRepo
            .GetForgivenessesByStudentAsync(teacherId, teacherStudentId);
        var forgiverIds = forgivenesses.Select(f => f.ForgivenByUserId).Distinct().ToList();
        var forgiverNames = forgiverIds.Count > 0
            ? await _unitOfWork.Users.GetUserFullNamesByUserIdsAsync(forgiverIds)
            : new Dictionary<long, string>();

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
            }).ToList(),
            Forgivenesses = forgivenesses.Select(f => new ForgivenessHistoryEntryDto
            {
                Id = f.Id,
                Type = "Forgiven",
                Amount = f.Amount,
                Note = f.Note,
                ByName = forgiverNames.TryGetValue(f.ForgivenByUserId, out var fn) ? fn : null,
                Date = f.ForgivenAt,
                Status = f.Status == ForgivenessStatus.Reversed ? "reversed" : "active"
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
        // PAY-4: a custom price must be non-negative. 0 is allowed and marks the student as
        // exempt / not paying (free/scholarship) — surfaced as the "Not paying" label. A NEGATIVE
        // amount would drive negative dues and negative expected revenue, so it is still rejected.
        // Null clears the override and reverts the student to the session default.
        if (dto.CustomAmount.HasValue && dto.CustomAmount.Value < 0m)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.PaymentCustomAmountInvalid,
                HttpStatusCode.UnprocessableEntity);

        var counter = await _unitOfWork.PaymentsRepo
            .GetPaymentCounterAsync(dto.TeacherId, dto.TeacherStudentId);
        if (counter is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.StudentNotFound, HttpStatusCode.NotFound);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();
        try
        {
            counter.CustomPaymentAmount = dto.CustomAmount;
            await _unitOfWork.PaymentsRepo.UpdatePaymentCounterAsync(counter);

            // Propagate to the student's CURRENT month AND all FUTURE still-owed periods so a price
            // change is reflected immediately across every screen — not only on upcoming generations.
            // The repo predicate only selects Unpaid/PartiallyPaid periods, so fully-paid/overpaid
            // months are never rewritten (user-confirmed scope: current month + future).
            var localDate = _timeZoneService.GetTeacherLocalDate(dto.TeacherId);
            var currentMonthStart = new DateTime(localDate.Year, localDate.Month, 1);

            var periods = await _unitOfWork.PaymentsRepo
                .GetRepriceableStudentPeriodsAsync(dto.TeacherId, dto.TeacherStudentId, currentMonthStart);

            if (periods.Count > 0)
            {
                var sessionAmountCache = new Dictionary<long, decimal>();
                decimal outstandingDelta = 0m;
                int paidDelta = 0, unpaidDelta = 0;

                foreach (var p in periods)
                {
                    // Custom set → that amount; cleared (null) → revert to the period's own session default.
                    decimal newBase;
                    if (dto.CustomAmount.HasValue)
                    {
                        newBase = dto.CustomAmount.Value;
                    }
                    else if (p.SessionId.HasValue)
                    {
                        if (!sessionAmountCache.TryGetValue(p.SessionId.Value, out newBase))
                        {
                            var session = await _unitOfWork.SessionsRepo
                                .GetByIdAndTeacherAsync(p.SessionId.Value, dto.TeacherId);
                            newBase = session?.SessionAmount ?? p.AmountDue; // no session → leave unchanged
                            sessionAmountCache[p.SessionId.Value] = newBase;
                        }
                    }
                    else
                    {
                        continue; // orphaned period with no session and no custom target — skip
                    }

                    var d = RepricePeriodInPlace(p, newBase);
                    await _unitOfWork.PaymentsRepo.UpdatePaymentPeriodAsync(p);
                    outstandingDelta += d.OutstandingDelta;
                    paidDelta += d.PaidPeriodsDelta;
                    unpaidDelta += d.UnpaidPeriodsDelta;
                }

                // Flush period rewrites before the consecutive-unpaid recompute reads them.
                await _unitOfWork.SaveChangesAsync();
                await ApplyRepriceCounterDeltasAsync(
                    dto.TeacherId, dto.TeacherStudentId, outstandingDelta, paidDelta, unpaidDelta);
            }

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

        return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.CustomAmountSetSuccess);
    }

    // ══════════════════════════════════════════════
    // PRICE-CHANGE PROPAGATION (session amount / custom amount → upcoming periods)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<bool>> OnSessionAmountChangedAsync(
        long teacherId, long sessionId, decimal newAmount)
    {
        // Future (next month onward, teacher-local) still-owed periods of the session, excluding
        // custom-priced students (BR-PAY-003). Runs on the CALLER's transaction (SessionService) —
        // it only mutates + SaveChanges, never opens/commits its own boundary (§5.2).
        var localDate = _timeZoneService.GetTeacherLocalDate(teacherId);
        var nextMonthStart = new DateTime(localDate.Year, localDate.Month, 1).AddMonths(1);

        var periods = await _unitOfWork.PaymentsRepo
            .GetRepriceableSessionDefaultPeriodsAsync(teacherId, sessionId, nextMonthStart);
        if (periods.Count == 0)
            return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);

        // Re-price in place, tallying counter deltas per affected student.
        var deltas = new Dictionary<long, (decimal Outstanding, int Paid, int Unpaid)>();
        foreach (var p in periods)
        {
            if (p.TeacherStudentId is null) continue;
            var d = RepricePeriodInPlace(p, newAmount);
            await _unitOfWork.PaymentsRepo.UpdatePaymentPeriodAsync(p);

            var acc = deltas.GetValueOrDefault(p.TeacherStudentId.Value);
            deltas[p.TeacherStudentId.Value] = (
                acc.Outstanding + d.OutstandingDelta,
                acc.Paid + d.PaidPeriodsDelta,
                acc.Unpaid + d.UnpaidPeriodsDelta);
        }

        // Flush period rewrites so the per-student consecutive-unpaid recompute reads fresh state.
        await _unitOfWork.SaveChangesAsync();

        foreach (var (studentId, d) in deltas)
            await ApplyRepriceCounterDeltasAsync(teacherId, studentId, d.Outstanding, d.Paid, d.Unpaid);

        await _unitOfWork.SaveChangesAsync();
        return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> BackfillSessionPeriodsThroughEndDateAsync(
        long teacherId, long sessionId)
    {
        var session = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId, teacherId);
        if (session is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        // Monthly only. A PerSession session bills per occurrence, and its occurrences are
        // (re)generated by the attendance module on a date change — extending the window there does
        // not leave the same monthly gap, so it is intentionally out of scope here.
        if (session.PaymentType != PaymentType.Monthly)
            return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);

        var endMonth = new DateTime(session.EndDate.Year, session.EndDate.Month, 1);
        var studentIds = await _unitOfWork.PaymentsRepo
            .GetStudentIdsBySessionAsync(teacherId, sessionId);
        if (studentIds.Count == 0)
            return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);

        foreach (var teacherStudentId in studentIds)
        {
            var existing = await _unitOfWork.PaymentsRepo
                .GetPaymentPeriodsByStudentAndSessionAsync(teacherId, teacherStudentId, sessionId);
            if (existing.Count == 0)
                continue; // never billed for this session at all — assignment owns that, not a backfill

            // Resume the month AFTER the student's latest period; never rewrite or duplicate one.
            var latest = existing.Max(p => p.PeriodStart);
            var nextMonth = new DateTime(latest.Year, latest.Month, 1).AddMonths(1);
            if (nextMonth > endMonth)
                continue; // already covered through the session's end

            var counter = await _unitOfWork.PaymentsRepo
                .GetPaymentCounterAsync(teacherId, teacherStudentId);
            decimal baseAmount = counter?.CustomPaymentAmount ?? session.SessionAmount;

            var student = await _unitOfWork.Students
                .GetActiveByIdAndTeacherAsync(teacherStudentId, teacherId);

            int sequence = await _unitOfWork.PaymentsRepo
                .GetMaxPeriodSequenceAsync(teacherId, teacherStudentId, sessionId) + 1;

            var periods = new List<PaymentPeriod>();
            for (var month = nextMonth; month <= endMonth; month = month.AddMonths(1))
            {
                periods.Add(new PaymentPeriod
                {
                    TeacherId = teacherId,
                    SessionId = sessionId,
                    TeacherStudentId = teacherStudentId,
                    PeriodType = PeriodType.Monthly,
                    PeriodStart = month,
                    PeriodEnd = month.AddMonths(1).AddDays(-1),
                    AmountDue = baseAmount,
                    PaymentStatus = PaymentStatus.Unpaid,
                    // Proration applies to a student's FIRST month only (join-day tiers); a
                    // backfilled month is never the first, so it is always billed in full.
                    IsProRated = false,
                    ProRatedFraction = 1.0m,
                    PeriodSequence = sequence++,
                    SessionName = session.SessionName,
                    StudentName = student?.StudentName ?? string.Empty,
                    StudentCode = student?.StudentCode ?? string.Empty,
                    CreateAt = DateTime.UtcNow
                });
            }

            if (periods.Count == 0)
                continue;

            await _unitOfWork.PaymentsRepo.AddPaymentPeriodsRangeAsync(periods);

            if (counter is not null)
            {
                counter.TotalOutstanding += periods.Sum(p => p.AmountDue);
                counter.TotalUnpaidPeriods += periods.Count;
                await _unitOfWork.PaymentsRepo.UpdatePaymentCounterAsync(counter);
            }
        }

        await _unitOfWork.SaveChangesAsync();
        return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);
    }

    /// <summary>
    /// Rewrites a still-owed period's <c>AmountDue</c> to the new base (re-applying its proration
    /// fraction), recomputes its status from what's already paid, and returns how the owning
    /// student's counter aggregates must move. Input periods are always Unpaid/PartiallyPaid, so
    /// their old outstanding contribution is <c>AmountDue - AmountPaid</c>.
    /// </summary>
    private (decimal OutstandingDelta, int PaidPeriodsDelta, int UnpaidPeriodsDelta)
        RepricePeriodInPlace(PaymentPeriod p, decimal newBase)
    {
        decimal oldOutstanding = p.AmountDue - p.AmountPaid;

        decimal newDue = p.IsProRated ? Math.Round(newBase * p.ProRatedFraction, 2) : newBase;
        p.AmountDue = newDue;
        p.PaymentStatus = RecomputePeriodStatus(p);

        bool nowPaid = p.PaymentStatus == PaymentStatus.Paid;
        decimal newOutstanding = nowPaid ? 0m : p.AmountDue - p.AmountPaid;

        return (newOutstanding - oldOutstanding, nowPaid ? 1 : 0, nowPaid ? -1 : 0);
    }

    /// <summary>
    /// Applies accumulated re-price deltas to a student's counter and refreshes the
    /// consecutive-unpaid count (recomputed from the now-flushed periods).
    /// </summary>
    private async Task ApplyRepriceCounterDeltasAsync(
        long teacherId, long teacherStudentId,
        decimal outstandingDelta, int paidPeriodsDelta, int unpaidPeriodsDelta)
    {
        var counter = await _unitOfWork.PaymentsRepo
            .GetPaymentCounterAsync(teacherId, teacherStudentId);
        if (counter is null) return;

        counter.TotalOutstanding += outstandingDelta;
        counter.TotalPaidPeriods += paidPeriodsDelta;
        counter.TotalUnpaidPeriods += unpaidPeriodsDelta;
        counter.ConsecutiveUnpaid = await _unitOfWork.PaymentsRepo
            .RecalculateConsecutiveUnpaidAsync(teacherId, teacherStudentId);
        await _unitOfWork.PaymentsRepo.UpdatePaymentCounterAsync(counter);
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
                UnpaidPeriodLabels = row.UnpaidPeriods.Select(PaymentLabelFormatter.FormatUnpaidPeriodLabel).ToList()
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
        AssistantId = w.AssistantId ?? w.CenterAssistantId ?? 0,
        AssistantName = w.Assistant?.User?.FullName ?? w.CenterAssistant?.User?.FullName ?? "Unknown",
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
        // Sort by the resolved collector name in memory (repo no longer orders in SQL — see its note).
        var dtos = wallets.Select(MapWalletDto)
            .OrderBy(d => d.AssistantName, StringComparer.OrdinalIgnoreCase)
            .ToList();

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
        // The route id is an Assistant.Id for a normal assistant wallet, OR a CenterAssistant.Id for a
        // center-assistant wallet (both are surfaced as `assistantId` in the wallets DTO). Resolve either.
        var wallet = await _unitOfWork.PaymentsRepo.GetAssistantWalletAsync(dto.TeacherId, dto.AssistantId)
            ?? await _unitOfWork.PaymentsRepo.GetAssistantWalletByCenterAssistantIdAsync(dto.TeacherId, dto.AssistantId);
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
                AssistantId = wallet.AssistantId,
                CenterAssistantId = wallet.CenterAssistantId,
                AssistantWalletId = wallet.Id,
                AmountReset = wallet.CurrentBalance,
                ResetByUserId = dto.ResetByUserId,
                ResetAt = DateTime.UtcNow,
                AssistantName = wallet.Assistant?.User?.FullName ?? wallet.CenterAssistant?.User?.FullName,
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
        // assistantId is an Assistant.Id OR a CenterAssistant.Id (see ResetWalletAsync note) — resolve either.
        var wallet = await _unitOfWork.PaymentsRepo.GetAssistantWalletAsync(teacherId, assistantId)
            ?? await _unitOfWork.PaymentsRepo.GetAssistantWalletByCenterAssistantIdAsync(teacherId, assistantId);
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
                AssistantId = wallet.AssistantId,
                CenterAssistantId = wallet.CenterAssistantId,
                AssistantWalletId = wallet.Id,
                AmountReset = withdrawAmount,
                ResetByUserId = withdrawnByUserId,
                ResetAt = DateTime.UtcNow,
                AssistantName = wallet.Assistant?.User?.FullName ?? wallet.CenterAssistant?.User?.FullName,
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
    public async Task<Result<DeparturesResponse>> GetDeparturesAsync(
        long teacherId, string? search, int page, int limit)
    {
        page = page < 1 ? 1 : page;
        limit = limit < 1 ? 20 : (limit > 100 ? 100 : limit);

        var (rows, total) = await _unitOfWork.PaymentsRepo
            .GetDeparturesPagedAsync(teacherId, search, page, limit);

        var response = new DeparturesResponse
        {
            Page = page,
            Limit = limit,
            TotalItems = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)limit),
            Departures = rows.Select(r => new DepartureListItemDto
            {
                Id = r.Id,
                StudentId = r.TeacherStudentId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StudentName = r.StudentName,
                StudentCode = r.StudentCode,
                SessionName = r.SessionName,
                DepartedAt = r.DepartedAt,
                DepartureOutcome = r.DepartureOutcome.ToString(),
                FinalAmount = r.FinalAmount,
                IsRefund = r.DepartureOutcome == DepartureOutcome.RefundDue,
                AttendedOccurrences = r.AttendedOccurrences,
                TotalOccurrencesInPeriod = r.TotalOccurrencesInPeriod,
                FullPeriodAmount = r.FullPeriodAmount,
                ProRatedAmount = r.ProRatedAmount,
                PaymentStatusAtDeparture = r.PaymentStatusAtDeparture.ToString(),
            }).ToList(),
        };

        return Result<DeparturesResponse>.Success(
            response, _localizer, PaymentConstants.Messages.Success);
    }

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

        // ── Anchor month: the month of the student's LATEST PAID period in this session ──
        // The refund is a correction on ONE month's money — the last month the student actually
        // paid for — never a cumulative since-they-joined figure. When they never paid anything in
        // this session there is no cash to give back, so we anchor on the teacher-local CURRENT
        // month and treat the paid amount as 0 (the outcome is then owed / nothing).
        var paidPeriod = await _unitOfWork.PaymentsRepo
            .GetLatestPaidPeriodAsync(teacherId, teacherStudentId, student.SessionId);

        DateTime monthStart;
        decimal paidAmount;
        if (paidPeriod is not null)
        {
            monthStart = new DateTime(paidPeriod.PeriodStart.Year, paidPeriod.PeriodStart.Month, 1);
            paidAmount = paidPeriod.AmountPaid;
        }
        else
        {
            var localDate = _timeZoneService.GetTeacherLocalDate(teacherId);
            monthStart = new DateTime(localDate.Year, localDate.Month, 1);
            paidAmount = 0m;
        }
        DateTime monthEnd = monthStart.AddMonths(1).AddDays(-1);

        // Reference period for the month's FULL price / status when nothing was paid yet.
        var unpaidPeriod = paidPeriod is null
            ? await _unitOfWork.PaymentsRepo
                .GetEarliestUnpaidPeriodAsync(teacherId, teacherStudentId, student.SessionId)
            : null;

        // ── Y (denominator) = ALL scheduled class days in the anchored month ──
        // DELIBERATE PRODUCT DECISION (overrides documented BR-PAY-007, which said unrecorded
        // occurrences are excluded from BOTH sides of the ratio): the student paid for the whole
        // month's schedule, so the month's whole schedule is what the refund is measured against.
        // A day the tutor simply never took attendance on must NOT shrink the denominator — that
        // used to inflate the attended ratio and silently shrink the refund. DO NOT "fix" this
        // back to recorded-occurrences-only.
        int totalOccurrences = await _unitOfWork.AttendanceRepo
            .CountOccurrencesBySessionAndDateRangeAsync(student.SessionId.Value, monthStart, monthEnd);

        // ── X (numerator) = days the student actually showed up in that month ──
        // ONE query for the whole month (the old code ran one query PER occurrence — an N+1).
        // Anything that is not an explicit Absent counts as attended (Present, CrossSessionPresent,
        // Held), matching how the attendance module reads a "showed up" record.
        var monthRecords = await _unitOfWork.AttendanceRepo
            .GetRecordsByStudentAndDateRangeAsync(teacherStudentId, monthStart, monthEnd);
        int attendedOccurrences = monthRecords.Count(r =>
            r.SessionId == student.SessionId.Value && r.Status != AttendanceStatus.Absent);

        // ── Money ──
        // Refund base = the cash actually paid for the anchored month (NOT the price list amount).
        // ALWAYS attendance-based: the teacher's IsProratedPaymentEnabled flag (AAM-FR-04.4) is
        // deliberately NOT consulted here — it is off by default and it used to discard this whole
        // calculation and hand back the full paid amount. That flag governs period GENERATION
        // (OnStudentAssignedToSessionAsync) only; it must never disable the departure math again.
        decimal fullAmount = paidPeriod?.AmountDue ?? unpaidPeriod?.AmountDue ?? session.SessionAmount;
        decimal basisAmount = paidAmount > 0m ? paidAmount : fullAmount;
        decimal proRatedAmount = totalOccurrences > 0
            ? Math.Round((attendedOccurrences / (decimal)totalOccurrences) * basisAmount, 2,
                MidpointRounding.AwayFromZero)
            : 0m;

        var paymentStatus = paidPeriod?.PaymentStatus ?? unpaidPeriod?.PaymentStatus ?? PaymentStatus.Paid;

        DepartureOutcome outcome;
        decimal finalAmount;

        if (paidAmount > 0m)
        {
            // REQ-PAY-069: refund = what they paid − what they consumed (attended share).
            finalAmount = Math.Round(paidAmount - proRatedAmount, 2, MidpointRounding.AwayFromZero);
            if (finalAmount < 0m) finalAmount = 0m;
            outcome = finalAmount > 0m ? DepartureOutcome.RefundDue : DepartureOutcome.NoObligation;
        }
        else if (attendedOccurrences > 0)
        {
            // REQ-PAY-070 (preserved): nothing was paid for the anchored month yet but the student
            // did attend — they OWE the attended share of that month's price. Kept so a departing
            // debtor still shows the arrears the tutor is expected to collect.
            finalAmount = proRatedAmount;
            outcome = DepartureOutcome.AmountOwed;
        }
        else
        {
            // REQ-PAY-071: nothing paid, nothing attended — no obligation either way.
            finalAmount = 0m;
            outcome = DepartureOutcome.NoObligation;
        }

        return Result<DepartureSummaryDto>.Success(new DepartureSummaryDto
        {
            StudentName = student.StudentName,
            StudentCode = student.StudentCode,
            SessionName = session.SessionName,
            CurrentPeriodLabel = FormatPeriodLabel(monthStart, monthEnd),
            TotalOccurrencesInPeriod = totalOccurrences,
            AttendedOccurrences = attendedOccurrences,
            FullPeriodAmount = fullAmount,
            ProRatedAmount = proRatedAmount,
            PaymentStatusAtDeparture = paymentStatus,
            DepartureOutcome = outcome,
            FinalAmount = finalAmount,
            PeriodStart = monthStart,
            MonthLabel = monthStart.ToString("MMMM yyyy"),
            PaymentPeriodId = paidPeriod?.Id,
            PaidAmount = paidAmount,
            OutcomeLabel = outcome switch
            {
                DepartureOutcome.RefundDue => _localizer[
                    PaymentConstants.Messages.DepartureOutcomeRefundDueLabel,
                    finalAmount.ToString("F2")].Value,
                DepartureOutcome.AmountOwed => _localizer[
                    PaymentConstants.Messages.DepartureOutcomeAmountOwedLabel,
                    finalAmount.ToString("F2")].Value,
                _ => _localizer[PaymentConstants.Messages.DepartureOutcomeNoObligationLabel].Value
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

        // REQ-PAY-075: the tutor may override the calculated figure — but only DOWNWARD and never
        // below zero. An unvalidated override was free money: a negative value flipped the wallet
        // deduction into a credit, and an oversized one refunded cash that was never collected.
        // Ceiling: a refund can never exceed the cash actually paid for the anchored month; an
        // owed amount can never exceed that month's full price.
        if (dto.OverrideAmount.HasValue)
        {
            decimal maxAllowedOverride = summary.DepartureOutcome == DepartureOutcome.AmountOwed
                ? summary.FullPeriodAmount
                : summary.PaidAmount;

            if (dto.OverrideAmount.Value < 0m || dto.OverrideAmount.Value > maxAllowedOverride)
                return Result<StudentDepartureDto>.Failure(
                    _localizer, PaymentConstants.Messages.DepartureOverrideAmountInvalid,
                    HttpStatusCode.UnprocessableEntity);
        }

        decimal finalAmount = dto.OverrideAmount ?? summary.FinalAmount;
        bool isTutorOverride = dto.OverrideAmount.HasValue;

        // Set inside the transaction when DeleteStudent tears the roster record down; the link
        // notifications are fanned out AFTER the commit (best-effort, §5.1).
        StudentTeardownOutcome? teardownOutcome = null;

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
                    // Route through the shared teardown so a departure-delete cleans up exactly
                    // what a normal recycle-bin delete does — critically it ENDS the student
                    // ACCOUNT link (RemovedByTeacher + audit + binding cleared). Without it the
                    // student app would keep listing this teacher forever and the filtered unique
                    // index would block a future link request. Runs inside THIS transaction; the
                    // notification fires after the commit below.
                    teardownOutcome = await _studentTeardown.UnassignAndUnlinkAsync(
                        dto.TeacherId, dto.TeacherStudentId, dto.ConfirmedByUserId);
                    studentToUnassign.IsDeleted = true;
                    studentToUnassign.DeletedAt = DateTime.UtcNow;
                }
                await _unitOfWork.Students.UpdateAsync(studentToUnassign);
            }

            // Nulling TeacherStudent.SessionId alone left the StudentSessionAssignment row ACTIVE,
            // so the departed student kept generating attendance obligations (and auto-absents) for
            // a session they had left. Close the assignment period too — same two named repo calls
            // AttendanceService.OnStudentUnassignedFromSessionAsync makes (PaymentService does not
            // inject IAttendanceService; calling it would create a circular service dependency, and
            // DeactivateAssignmentsByStudentAsync is the PURGE variant that also NULLs the student
            // FK, which would orphan the attendance history — see BUG-8).
            var activeAssignment = await _unitOfWork.AttendanceRepo
                .GetActiveAssignmentAsync(dto.TeacherStudentId);
            if (activeAssignment is not null)
            {
                activeAssignment.IsActive = false;
                activeAssignment.UnassignedAt = DateTime.UtcNow;
                await _unitOfWork.AttendanceRepo.UpdateAssignmentAsync(activeAssignment);
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
                // Collection instant of that same latest payment — for the reset-aware wallet reversal:
                // if the refunded cash was collected before the collector's last hand-over it was already
                // given to the tutor, so it must not drive the wallet negative.
                var latestCollectionAt = await _unitOfWork.PaymentsRepo
                    .GetLatestCollectionInstantForStudentSessionAsync(
                        dto.TeacherId, dto.TeacherStudentId, dto.SessionId);

                // Attribute the refund so it can be surfaced as a negative against this collector
                // (an assistant OR the tutor) in the anchored month — across the collections ledger
                // and the per-collector totals. The entity is change-tracked, so this persists on save.
                departure.CollectedByUserId = collectorUserId;
                departure.RefundPeriodStart = summary.PeriodStart;

                await AdjustAssistantWalletAsync(
                    dto.TeacherId, collectorUserId, -finalAmount, latestCollectionAt);

                // Reverse the refunded cash on the ANCHORED period as well. Without this the money
                // left the wallet but the month still read Paid with its full AmountPaid, so the
                // student's payment screen, arrears and dashboards all kept counting refunded cash.
                await ReverseDeparturePeriodAsync(dto, summary, finalAmount);
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

            // Post-commit, best-effort: tell the student their link to this teacher ended.
            // Never fails the departure (§5.1) — the service swallows its own errors.
            if (teardownOutcome is not null)
                await _studentTeardown.NotifyStudentUnlinkedAsync(dto.TeacherId, teardownOutcome);

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

    /// <summary>
    /// Reverses a departure refund on the anchored <c>PaymentPeriod</c> (the latest PAID month the
    /// summary calculated against) and on the student's payment counter, then writes a
    /// <c>PaymentEditLog</c> audit row — mirroring how <see cref="EditPaymentAsync"/> /
    /// <see cref="DeletePaymentAsync"/> reverse money.
    ///
    /// Runs INSIDE the caller's transaction (ConfirmDepartureAsync owns the commit boundary — §5.2:
    /// no SaveChangesAsync here). No-ops when the student never paid (no anchored period id).
    /// REQ-PAY-073.
    /// </summary>
    private async Task ReverseDeparturePeriodAsync(
        ConfirmDepartureDto dto, DepartureSummaryDto summary, decimal refundAmount)
    {
        if (summary.PaymentPeriodId is null || refundAmount <= 0m) return;

        var period = await _unitOfWork.PaymentsRepo
            .GetPaymentPeriodByIdAsync(summary.PaymentPeriodId.Value);
        // Tenant guard: the period must belong to this teacher AND this student.
        if (period is null
            || period.TeacherId != dto.TeacherId
            || period.TeacherStudentId != dto.TeacherStudentId)
            return;

        // Never reverse more than the month actually holds (a tutor override is already capped at
        // the paid amount, but the period may have moved since the summary was computed).
        decimal reversal = Math.Min(refundAmount, period.AmountPaid);
        if (reversal <= 0m) return;

        decimal previousAmountPaid = period.AmountPaid;
        var previousStatus = period.PaymentStatus;

        period.AmountPaid -= reversal;
        if (period.AmountPaid < 0m) period.AmountPaid = 0m;
        period.PaymentStatus = RecomputePeriodStatus(period);
        await _unitOfWork.PaymentsRepo.UpdatePaymentPeriodAsync(period);

        // Counter: the refunded cash is no longer paid, and it goes back to outstanding.
        var counter = await _unitOfWork.PaymentsRepo
            .GetPaymentCounterAsync(dto.TeacherId, dto.TeacherStudentId);
        if (counter is not null)
        {
            counter.TotalAmountPaid -= reversal;
            if (counter.TotalAmountPaid < 0m) counter.TotalAmountPaid = 0m;
            counter.TotalOutstanding += reversal;
            if (counter.TotalOutstanding < 0m) counter.TotalOutstanding = 0m;
            counter.ConsecutiveUnpaid = await _unitOfWork.PaymentsRepo
                .RecalculateConsecutiveUnpaidAsync(dto.TeacherId, dto.TeacherStudentId);
            await _unitOfWork.PaymentsRepo.UpdatePaymentCounterAsync(counter);
        }

        // Audit: attach the log to the most recent transaction that settled this period when there
        // is one (the FK is nullable + SET NULL, so a period with no surviving transaction still
        // gets an auditable row).
        var periodTransactions = await _unitOfWork.PaymentsRepo
            .GetTransactionsByPeriodAsync(period.Id);
        var latestTransaction = periodTransactions
            .OrderByDescending(t => t.CollectedAt)
            .ThenByDescending(t => t.Id)
            .FirstOrDefault();

        await _unitOfWork.PaymentsRepo.AddPaymentEditLogAsync(new PaymentEditLog
        {
            PaymentTransactionId = latestTransaction?.Id,
            EditAction = PaymentEditAction.Reversed,
            PreviousAmount = previousAmountPaid,
            NewAmount = period.AmountPaid,
            PreviousStatus = previousStatus,
            NewStatus = period.PaymentStatus,
            EditedByUserId = dto.ConfirmedByUserId,
            EditedAt = DateTime.UtcNow,
            EditReason = _localizer[PaymentConstants.Messages.DepartureRefundReversalReason].Value,
            CreateAt = DateTime.UtcNow
        });
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
            var entry = new PaymentSyncEntryResultDto
            {
                ClientEntryId = offlineRecord.ClientEntryId
            };
            result.EntryResults.Add(entry);

            // 1. Exactly-once replay guard: a record whose ClientEntryId was
            // already persisted (an earlier sync whose response was lost) is
            // acknowledged WITHOUT recording again.
            if (!string.IsNullOrWhiteSpace(offlineRecord.ClientEntryId))
            {
                var alreadySynced = await _unitOfWork.PaymentsRepo
                    .GetByClientEntryIdAsync(dto.TeacherId, offlineRecord.ClientEntryId);
                if (alreadySynced is not null)
                {
                    result.SyncedCount++;
                    entry.Success = true;
                    entry.AlreadySynced = true;
                    entry.ExistingRecord = MapToTransactionDto(alreadySynced);
                    continue;
                }
            }

            // 2. Cross-device conflict: same student + same day recorded by a
            // DIFFERENT device/user needs human resolution (REQ-PAY-082).
            var existing = await _unitOfWork.PaymentsRepo
                .GetSameDayTransactionsAsync(dto.TeacherId, offlineRecord.TeacherStudentId,
                    offlineRecord.OfflineCollectedAt?.Date ?? DateTime.UtcNow.Date);

            if (existing.Any(e => e.OfflineDeviceId != offlineRecord.OfflineDeviceId))
            {
                result.ConflictCount++;
                var conflictRecord = MapToTransactionDto(existing.First());
                result.Conflicts.Add(new PaymentConflictDto
                {
                    OfflineRecord = offlineRecord,
                    ExistingRecord = conflictRecord,
                    ConflictReason = "Same student paid by different user/device on same day"
                });
                entry.IsConflict = true;
                entry.ErrorMessage = "Same student paid by different user/device on same day";
                entry.ExistingRecord = conflictRecord;
                continue;
            }

            // 3. Resolve the student's active session when the client did not
            // send one (offline clients don't know session ids — same
            // resolution the v1 collect path uses).
            if (offlineRecord.SessionId <= 0)
            {
                var syncStudent = await _unitOfWork.Students
                    .GetActiveByIdAndTeacherAsync(offlineRecord.TeacherStudentId, dto.TeacherId);
                if (syncStudent?.SessionId is null)
                {
                    result.FailedCount++;
                    entry.ErrorMessage = syncStudent is null
                        ? "Student not found."
                        : "Student is not assigned to a session.";
                    continue;
                }
                offlineRecord.SessionId = syncStudent.SessionId.Value;
            }

            // 4. Record through the normal money path.
            offlineRecord.DuplicateConfirmed = true;
            Result<CollectPaymentResultDto> collectResult;
            try
            {
                collectResult = await CollectPaymentAsync(offlineRecord);
            }
            catch (Exception)
            {
                // Unique-index race: a concurrent replay of this same record
                // may have won the insert between the dedup check and now.
                var winner = string.IsNullOrWhiteSpace(offlineRecord.ClientEntryId)
                    ? null
                    : await _unitOfWork.PaymentsRepo
                        .GetByClientEntryIdAsync(dto.TeacherId, offlineRecord.ClientEntryId);
                if (winner is null) throw;
                result.SyncedCount++;
                entry.Success = true;
                entry.AlreadySynced = true;
                entry.ExistingRecord = MapToTransactionDto(winner);
                continue;
            }

            if (collectResult.IsSuccess && collectResult.Data?.Transaction is not null)
            {
                result.SyncedCount++;
                entry.Success = true;
                continue;
            }

            // Recorded nothing: business rejection (already paid, student not
            // assigned, amount cap...). Surfaced per record — previously these
            // were silently uncounted.
            result.FailedCount++;
            entry.IsConflict = collectResult.Data?.IsAlreadyPaid == true;
            entry.ErrorMessage = collectResult.Message;
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
        var currentSessionPeriod = periods.FirstOrDefault(p => p.SessionId.HasValue);
        var currentSession = currentSessionPeriod is null
            ? "Unknown"
            : DisplaySessionName(currentSessionPeriod);

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
            // Live name when the session still exists (see DisplaySessionName).
            CurrentSessionName = headerPeriod is null ? null : DisplaySessionName(headerPeriod),
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

    /// <summary>
    /// The name to SHOW for a period's session: the live <c>Session.SessionName</c> when the session
    /// still exists, falling back to the period's denormalized snapshot only when it does not.
    /// The snapshot is written once, when the period is generated, so a session renamed afterwards
    /// kept displaying its OLD name on the payment screens while the sessions screen showed the new
    /// one. Keeping the snapshot as the fallback preserves the name for deleted sessions, which is
    /// exactly what it is denormalized for.
    /// </summary>
    private static string DisplaySessionName(PaymentPeriod p) =>
        p.Session?.SessionName ?? p.SessionName;

    /// <summary>Maps a period to a tracking-screen row with explicitly-named amounts.</summary>
    private static StudentPaymentPeriodDto BuildTrackingRow(
        PaymentPeriod p, decimal outstanding, DateTime? paidOn, int? monthsOverdue) => new()
    {
        PeriodId = p.Id,
        PeriodStartDate = p.PeriodStart,
        SessionName = DisplaySessionName(p),
        PeriodType = p.PeriodType,
        Status = p.PaymentStatus,
        AmountDue = p.AmountDue,
        AmountPaid = p.AmountPaid,
        OutstandingAmount = outstanding,
        PaidOnDate = paidOn,
        MonthsOverdue = monthsOverdue,
        IsProRated = p.IsProRated,
        IsCarriedForward = p.IsCarriedForward,
        MovedFromSessionId = p.MovedFromSessionId,
        MovedFromSessionName = p.MovedFromSessionName
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

        // Months / dates the student has ALREADY paid (any session) — skip them below so a paid or
        // pre-paid period "reflects" under the session it was paid on and is never double-billed
        // when moving to a new session. No-op on a first assignment (the student has no periods).
        var alreadyPaidPeriods = (await _unitOfWork.PaymentsRepo
                .GetAllPaymentPeriodsByStudentAsync(teacherId, teacherStudentId))
            .Where(p => p.AmountPaid > 0m
                || p.PaymentStatus == PaymentStatus.Paid
                || p.PaymentStatus == PaymentStatus.Overpaid)
            .ToList();
        var paidMonths = alreadyPaidPeriods
            .Select(p => new DateTime(p.PeriodStart.Year, p.PeriodStart.Month, 1))
            .ToHashSet();
        var paidDates = alreadyPaidPeriods.Select(p => p.PeriodStart.Date).ToHashSet();

        // Generate initial payment periods. Extracted into BuildSessionPeriodsAsync so the
        // session-move path (OnStudentMovedBetweenSessionsAsync) reuses the SAME proration / custom
        // amount / sequencing logic with an extended skip-set. Sequenced after any existing periods.
        int sequence = await _unitOfWork.PaymentsRepo
            .GetMaxPeriodSequenceAsync(teacherId, teacherStudentId, sessionId) + 1;
        var periods = await BuildSessionPeriodsAsync(
            teacherId, teacherStudentId, session, sessionName, assignedAt, counter, student,
            sequence, paidMonths, paidDates);

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
        // Void the student's still-UNPAID current + future payment periods (they are leaving the
        // session). PAID / partially-paid / overpaid periods AND all PAST periods are preserved as
        // history. Called on reassignment BEFORE the new session's schedule is generated, so a
        // student is never billed by two sessions for the same forward months.
        var nowUtc = DateTime.UtcNow;
        var currentMonthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1);

        var allPeriods = await _unitOfWork.PaymentsRepo
            .GetAllPaymentPeriodsByStudentAsync(teacherId, teacherStudentId);
        var toRemove = allPeriods
            .Where(p => p.PeriodStart >= currentMonthStart
                && p.PaymentStatus == PaymentStatus.Unpaid
                && p.AmountPaid <= 0m)
            .ToList();

        if (toRemove.Count == 0)
            return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);

        var periodRepo = _unitOfWork.GetRepository<PaymentPeriod, long>();
        await periodRepo.DeleteRangeAsync(toRemove);

        // Keep the denormalized counter aggregates in step with the removed periods (mirrors the
        // increment in OnStudentAssignedToSessionAsync).
        var counter = await _unitOfWork.PaymentsRepo
            .GetPaymentCounterAsync(teacherId, teacherStudentId);
        if (counter is not null)
        {
            counter.TotalOutstanding =
                Math.Max(0m, counter.TotalOutstanding - toRemove.Sum(p => p.AmountDue));
            counter.TotalUnpaidPeriods = Math.Max(0, counter.TotalUnpaidPeriods - toRemove.Count);
            await _unitOfWork.PaymentsRepo.UpdatePaymentCounterAsync(counter);
        }

        await _unitOfWork.SaveChangesAsync();
        return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);
    }

    // ══════════════════════════════════════════════
    // SESSION MOVE (A → B) — BILLING CARRY-OVER
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<bool>> OnStudentMovedBetweenSessionsAsync(
        long teacherId, long teacherStudentId,
        long fromSessionId, string fromSessionName,
        long toSessionId, string toSessionName,
        DateTime movedAt)
    {
        // A "move" to the SAME session is a no-op. UpdateStudentAsync only calls this on a genuine
        // session change, but keep it safe for the ops/cleanup caller.
        if (fromSessionId == toSessionId)
            return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);

        var toSession = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(toSessionId, teacherId);
        if (toSession is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.SessionNotFound, HttpStatusCode.NotFound);

        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(teacherStudentId, teacherId);
        if (student is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.StudentNotFound, HttpStatusCode.NotFound);

        var counter = await _unitOfWork.PaymentsRepo.GetPaymentCounterAsync(teacherId, teacherStudentId);
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

        // The source session's periods for this student, ordered. Also lets us fall back to a snapshot
        // name when the source session was hard-deleted (fromSessionName empty).
        var fromPeriods = (await _unitOfWork.PaymentsRepo
                .GetPaymentPeriodsByStudentAndSessionAsync(teacherId, teacherStudentId, fromSessionId))
            .OrderBy(p => p.PeriodSequence)
            .ToList();
        string effectiveFromName = !string.IsNullOrWhiteSpace(fromSessionName)
            ? fromSessionName
            : (fromPeriods.FirstOrDefault()?.SessionName ?? string.Empty);

        // PerSession is NOT carried over here (per-occurrence semantics differ from monthly arrears).
        // Fall back to the EXISTING behavior — void the source's unpaid current+future, regenerate the
        // destination — so nothing is guessed. Monthly is handled with the full arrears-move logic below.
        bool anyPerSessionSource = fromPeriods.Any(p => p.PeriodType != PeriodType.Monthly);
        if (toSession.PaymentType != PaymentType.Monthly || anyPerSessionSource)
        {
            bool ownsTxFallback = !_unitOfWork.HasActiveTransaction;
            if (ownsTxFallback) await _unitOfWork.BeginTransactionAsync();
            try
            {
                var unassigned = await OnStudentUnassignedFromSessionAsync(teacherId, teacherStudentId);
                if (!unassigned.IsSuccess)
                {
                    if (ownsTxFallback) await _unitOfWork.RollbackAsync();
                    return unassigned;
                }
                var assigned = await OnStudentAssignedToSessionAsync(
                    teacherId, teacherStudentId, toSessionId, toSessionName, movedAt);
                if (!assigned.IsSuccess)
                {
                    if (ownsTxFallback) await _unitOfWork.RollbackAsync();
                    return assigned;
                }
                if (ownsTxFallback) await _unitOfWork.CommitAsync();
                return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);
            }
            catch
            {
                if (ownsTxFallback) await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Teacher-local current-month end (mirrors §7.4 month scoping used across the module).
            var today = _timeZoneService.GetTeacherLocalDate(teacherId);
            var currentMonthEnd = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);

            int destSequence = await _unitOfWork.PaymentsRepo
                .GetMaxPeriodSequenceAsync(teacherId, teacherStudentId, toSessionId) + 1;

            // Classify the source periods (paid stay, unpaid-due move, future cancel, partials split).
            // Live move: no dest-overlap guard — the destination is (re)generated afterwards and the
            // generation skip-set below prevents any double-bill.
            var plan = BuildCarryOverPlan(fromPeriods, currentMonthEnd, destExistingMonths: null);

            // Capture the transfer snapshot BEFORE apply mutates the partials' AmountDue.
            decimal outstandingCarried = plan.OutstandingCarried;
            var statusAtTransfer = plan.StatusAtTransfer;

            destSequence = await ApplyCarryOverPlanAsync(
                plan, teacherId, teacherStudentId, student,
                fromSessionId, effectiveFromName, toSessionId, toSessionName, destSequence);

            // Generate the destination's OWN schedule (movedAt → end), skipping every month already
            // covered so nothing is double-billed: paid months (any session) ∪ months just moved/carried
            // in ∪ the destination's OWN pre-existing period months (e.g. a prior stint in this session).
            var studentPeriodsPreMove = await _unitOfWork.PaymentsRepo
                .GetAllPaymentPeriodsByStudentAsync(teacherId, teacherStudentId);
            var paidPeriods = studentPeriodsPreMove
                .Where(p => p.AmountPaid > 0m
                    || p.PaymentStatus == PaymentStatus.Paid
                    || p.PaymentStatus == PaymentStatus.Overpaid)
                .ToList();
            var skipMonths = paidPeriods
                .Select(p => new DateTime(p.PeriodStart.Year, p.PeriodStart.Month, 1))
                .ToHashSet();
            skipMonths.UnionWith(plan.MovedMonths);
            skipMonths.UnionWith(studentPeriodsPreMove
                .Where(p => p.SessionId == toSessionId)
                .Select(p => new DateTime(p.PeriodStart.Year, p.PeriodStart.Month, 1)));
            var skipDates = paidPeriods.Select(p => p.PeriodStart.Date).ToHashSet();

            var generated = await BuildSessionPeriodsAsync(
                teacherId, teacherStudentId, toSession, toSessionName, movedAt, counter, student,
                destSequence, skipMonths, skipDates);
            if (generated.Count > 0)
                await _unitOfWork.PaymentsRepo.AddPaymentPeriodsRangeAsync(generated);

            // Audit: one SessionTransferEvent documenting the move (REQ-PAY-089 — never deleted).
            await _unitOfWork.PaymentsRepo.AddSessionTransferEventAsync(new SessionTransferEvent
            {
                TeacherId = teacherId,
                TeacherStudentId = teacherStudentId,
                SourceSessionId = fromSessionId,
                SourceSessionName = effectiveFromName,
                DestinationSessionId = toSessionId,
                DestinationSessionName = toSessionName,
                PaymentStatusAtTransfer = statusAtTransfer,
                OutstandingBalance = outstandingCarried,
                CreditBalance = 0m,
                SourcePaymentType = PaymentType.Monthly.ToString(),
                DestinationPaymentType = toSession.PaymentType.ToString(),
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                TransferredAt = DateTime.UtcNow,
                TransferredByUserId = null, // automatic move (roster reassignment) — no explicit actor
                CreateAt = DateTime.UtcNow
            });

            // Flush the moves/deletes/adds, THEN recompute the counter from the resulting records so it
            // can never drift (recompute-from-records pattern used elsewhere in the module).
            await _unitOfWork.SaveChangesAsync();
            await RecomputeStudentPaymentCounterAsync(teacherId, teacherStudentId, counter);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction) await _unitOfWork.CommitAsync();
            return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.Success);
        }
        catch
        {
            if (ownsTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Immutable classification of what happens to a source session's PaymentPeriods when a student is
    /// moved OUT of it (A → B). Pure data (no DB writes) so the live move and the cleanup dry-run share
    /// ONE source of truth for the money logic. Monthly periods only.
    /// </summary>
    private sealed class CarryOverPlan
    {
        /// <summary>Fully-unpaid periods due on/before the current month → MOVE whole to destination.</summary>
        public List<PaymentPeriod> UnpaidDueToMove { get; } = new();
        /// <summary>Fully-unpaid periods after the current month → CANCEL (student left the source).</summary>
        public List<PaymentPeriod> FutureToCancel { get; } = new();
        /// <summary>Partially-paid periods → settle the paid part in source, carry the REMAINDER to dest.</summary>
        public List<PaymentPeriod> PartialsToSplit { get; } = new();
        /// <summary>Partially-paid periods whose month the destination ALREADY bills — real cash, so LEFT
        /// in place and flagged for manual review (cleanup guard; never auto-touched).</summary>
        public List<PaymentPeriod> OverlapSkipped { get; } = new();
        /// <summary>Fully-UNPAID periods whose month the destination ALREADY bills → a redundant duplicate
        /// of an obligation the current session already carries → DELETE (cleanup only; touches no cash and
        /// clears the stranded-period misclassification).</summary>
        public List<PaymentPeriod> OverlapRedundantToDelete { get; } = new();
        /// <summary>First-of-month keys carried into the destination (moved + partial-remainder).</summary>
        public HashSet<DateTime> MovedMonths { get; } = new();

        public decimal RedundantDeletedAmount => OverlapRedundantToDelete.Sum(p => p.AmountDue - p.AmountPaid);
        public decimal MovedAmount => UnpaidDueToMove.Sum(p => p.AmountDue - p.AmountPaid);
        public decimal SettledRemainderAmount => PartialsToSplit.Sum(p => p.AmountDue - p.AmountPaid);
        public decimal SettledInSourceAmount => PartialsToSplit.Sum(p => p.AmountPaid);
        public decimal CancelledAmount => FutureToCancel.Sum(p => p.AmountDue - p.AmountPaid);
        public decimal OutstandingCarried => MovedAmount + SettledRemainderAmount;
        public PaymentStatus StatusAtTransfer =>
            PartialsToSplit.Count > 0 ? PaymentStatus.PartiallyPaid
            : UnpaidDueToMove.Count > 0 ? PaymentStatus.Unpaid
            : PaymentStatus.Paid;
    }

    /// <summary>
    /// Classifies a source session's periods for a move (pure; see <see cref="CarryOverPlan"/>).
    /// <paramref name="destExistingMonths"/> is the cleanup-only overlap guard: a source period whose
    /// month the destination ALREADY bills is left untouched (OverlapSkipped) so an old arrear can never
    /// double-bill a month. Pass null in the live path (the destination is (re)generated afterwards and
    /// its generation skip-set prevents overlap).
    /// </summary>
    private static CarryOverPlan BuildCarryOverPlan(
        IEnumerable<PaymentPeriod> fromPeriods, DateTime currentMonthEnd,
        IReadOnlySet<DateTime>? destExistingMonths)
    {
        var plan = new CarryOverPlan();
        foreach (var p in fromPeriods.OrderBy(p => p.PeriodSequence))
        {
            decimal remaining = p.AmountDue - p.AmountPaid;
            if (remaining <= 0m)
                continue; // fully paid / overpaid → stays in the source as history (untouched)

            var monthKey = new DateTime(p.PeriodStart.Year, p.PeriodStart.Month, 1);
            if (destExistingMonths is not null && destExistingMonths.Contains(monthKey))
            {
                // Destination already bills this month. A fully-UNPAID source period here is a redundant
                // duplicate of an obligation the current session already carries → DELETE it (no cash;
                // this is what actually clears the stranded-period misclassification). A partially-paid
                // one holds real cash, so leave it flagged for manual review rather than guess.
                if (p.AmountPaid <= 0m)
                    plan.OverlapRedundantToDelete.Add(p);
                else
                    plan.OverlapSkipped.Add(p);
                continue;
            }

            if (p.AmountPaid > 0m)
            {
                // PARTIAL: paid part becomes source history; remainder is re-billed in the destination.
                plan.PartialsToSplit.Add(p);
                plan.MovedMonths.Add(monthKey);
            }
            else if (p.PeriodStart <= currentMonthEnd)
            {
                // UNPAID + DUE (past arrears or the current month) → move the whole obligation.
                plan.UnpaidDueToMove.Add(p);
                plan.MovedMonths.Add(monthKey);
            }
            else
            {
                // UNPAID + FUTURE → cancelled (the student left the source session).
                plan.FutureToCancel.Add(p);
            }
        }
        return plan;
    }

    /// <summary>
    /// Applies a <see cref="CarryOverPlan"/> to the database (no save; caller owns the commit): moves
    /// unpaid-due periods to the destination, settles partials in the source + creates the tagged
    /// remainder in the destination, and deletes cancelled future periods. Returns the next free
    /// destination PeriodSequence. Every carried row is tagged MovedFrom* + IsCarriedForward.
    /// </summary>
    private async Task<int> ApplyCarryOverPlanAsync(
        CarryOverPlan plan, long teacherId, long teacherStudentId, TeacherStudent student,
        long fromSessionId, string fromSessionName, long toSessionId, string toSessionName,
        int startDestSequence)
    {
        int destSequence = startDestSequence;

        foreach (var p in plan.UnpaidDueToMove)
        {
            p.SessionId = toSessionId;
            p.SessionName = toSessionName;
            p.MovedFromSessionId = fromSessionId;
            p.MovedFromSessionName = fromSessionName;
            p.OriginSessionName ??= fromSessionName; // keep the legacy display field populated too
            p.IsCarriedForward = true;
            p.PeriodSequence = destSequence++;
            // AmountDue / AmountPaid / PeriodStart / PeriodEnd / IsProRated preserved.
            await _unitOfWork.PaymentsRepo.UpdatePaymentPeriodAsync(p);
        }

        var remainders = new List<PaymentPeriod>();
        foreach (var p in plan.PartialsToSplit)
        {
            decimal remaining = p.AmountDue - p.AmountPaid;

            // Source: settle to exactly what was paid → becomes fully-paid history.
            p.AmountDue = p.AmountPaid;
            p.PaymentStatus = PaymentStatus.Paid;
            await _unitOfWork.PaymentsRepo.UpdatePaymentPeriodAsync(p);

            // Destination: the remaining balance as a new tagged, carried-forward bill (same month).
            remainders.Add(new PaymentPeriod
            {
                TeacherId = teacherId,
                SessionId = toSessionId,
                TeacherStudentId = teacherStudentId,
                PeriodType = PeriodType.Monthly,
                PeriodStart = p.PeriodStart,
                PeriodEnd = p.PeriodEnd,
                AmountDue = remaining,
                AmountPaid = 0m,
                PaymentStatus = PaymentStatus.Unpaid,
                IsProRated = false,
                ProRatedFraction = 1.0m,
                PeriodSequence = destSequence++,
                IsCarriedForward = true,
                MovedFromSessionId = fromSessionId,
                MovedFromSessionName = fromSessionName,
                OriginSessionName = fromSessionName,
                SessionName = toSessionName,
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                CreateAt = DateTime.UtcNow
            });
        }
        if (remainders.Count > 0)
            await _unitOfWork.PaymentsRepo.AddPaymentPeriodsRangeAsync(remainders);

        // Deletes: future obligations the student no longer has (left the source) + redundant unpaid
        // duplicates of months the destination already bills (cleanup overlap). Both are fully-unpaid
        // (no cash) — see BuildCarryOverPlan.
        var toDelete = new List<PaymentPeriod>(plan.FutureToCancel);
        toDelete.AddRange(plan.OverlapRedundantToDelete);
        if (toDelete.Count > 0)
        {
            var periodRepo = _unitOfWork.GetRepository<PaymentPeriod, long>();
            await periodRepo.DeleteRangeAsync(toDelete);
        }

        return destSequence;
    }

    /// <summary>
    /// Recomputes a student's <see cref="StudentPaymentCounter"/> period-derived aggregates
    /// (TotalOutstanding / TotalUnpaidPeriods / TotalPaidPeriods / ConsecutiveUnpaid) from the CURRENT
    /// PaymentPeriods, so a move/reconcile can never leave the counter drifted. Cash-derived fields
    /// (TotalAmountPaid, LastPaymentDate) are NOT touched — a move shifts obligations, not cash. Requires
    /// the prior period changes to be flushed (SaveChangesAsync) so the read sees them. Marks the counter
    /// modified; the caller owns the commit.
    /// </summary>
    private async Task RecomputeStudentPaymentCounterAsync(
        long teacherId, long teacherStudentId, StudentPaymentCounter counter)
    {
        var all = await _unitOfWork.PaymentsRepo
            .GetAllPaymentPeriodsByStudentAsync(teacherId, teacherStudentId);
        counter.TotalOutstanding = all.Sum(p => Math.Max(0m, p.AmountDue - p.AmountPaid));
        counter.TotalUnpaidPeriods = all.Count(p => p.AmountPaid < p.AmountDue);
        counter.TotalPaidPeriods = all.Count(p => p.AmountPaid >= p.AmountDue);
        counter.ConsecutiveUnpaid = await _unitOfWork.PaymentsRepo
            .RecalculateConsecutiveUnpaidAsync(teacherId, teacherStudentId);
        await _unitOfWork.PaymentsRepo.UpdatePaymentCounterAsync(counter);
    }

    /// <summary>
    /// Pure generation of a session's payment periods for a student from <paramref name="assignedAt"/>
    /// to the session end — Monthly: one row per month (first month pro-rated per the teacher's tiers);
    /// PerSession: one row per occurrence — at the student's effective price (custom amount else session
    /// default). SKIPS any month (<paramref name="skipMonths"/>) or occurrence date
    /// (<paramref name="skipDates"/>) already covered so nothing is double-billed. Adds NOTHING to the
    /// context and touches NO counter (returns the list; the caller persists). Shared by the fresh-assign
    /// hook and the session-move path so proration / custom-amount / sequencing never diverge.
    /// </summary>
    private async Task<List<PaymentPeriod>> BuildSessionPeriodsAsync(
        long teacherId, long teacherStudentId, Session session, string sessionName,
        DateTime assignedAt, StudentPaymentCounter counter, TeacherStudent student,
        int startSequence, IReadOnlySet<DateTime> skipMonths, IReadOnlySet<DateTime> skipDates)
    {
        var periods = new List<PaymentPeriod>();
        int sequence = startSequence;

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

                // GAP FIX (see OnStudentAssignedToSessionAsync history): clamp a join day that falls
                // outside every configured tier to the nearest tier instead of silently billing full
                // price — past every tier's end uses the max-ThresholdDayEnd tier, before every tier's
                // start uses the min-ThresholdDayStart tier. Exact matches are unchanged.
                if (matchingTier is null && tiers.Any())
                {
                    if (joinDay > tiers.Max(t => t.ThresholdDayEnd))
                        matchingTier = tiers.OrderByDescending(t => t.ThresholdDayEnd).First();
                    else if (joinDay < tiers.Min(t => t.ThresholdDayStart))
                        matchingTier = tiers.OrderBy(t => t.ThresholdDayStart).First();
                }

                if (matchingTier is not null && matchingTier.FractionRate < 1.0m)
                {
                    applyProRate = true;
                    proRateFraction = matchingTier.FractionRate;
                }
            }

            decimal baseAmount = counter.CustomPaymentAmount ?? session.SessionAmount;

            for (var month = startMonth; month <= endMonth; month = month.AddMonths(1))
            {
                if (skipMonths.Contains(month))
                    continue; // already paid / moved / existing — don't double-bill
                bool isFirstPeriod = month == startMonth && applyProRate;
                decimal periodAmount = isFirstPeriod
                    ? Math.Round(baseAmount * proRateFraction, 2)
                    : baseAmount;

                periods.Add(new PaymentPeriod
                {
                    TeacherId = teacherId,
                    SessionId = session.Id,
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
                .GetOccurrencesBySessionAsync(session.Id);
            decimal baseAmount = counter.CustomPaymentAmount ?? session.SessionAmount;

            foreach (var occ in occurrences.Where(o => o.OccurrenceDate >= assignedAt.Date))
            {
                if (skipDates.Contains(occ.OccurrenceDate.Date))
                    continue; // already paid / existing — don't double-bill
                periods.Add(new PaymentPeriod
                {
                    TeacherId = teacherId,
                    SessionId = session.Id,
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

        return periods;
    }

    /// <inheritdoc />
    public async Task<Result<MovedStudentsReconcileReport>> ReconcileMovedStudentsAsync(bool dryRun)
    {
        // One-time cleanup for arrears that the OLD reassign flow stranded in a student's previous
        // session (it deleted future unpaid periods but LEFT past unpaid periods under the old session).
        // For every currently-assigned student that still owes under a DIFFERENT session, apply the SAME
        // carry-over as a live move — move stranded unpaid-due → current session, cancel stranded future,
        // settle partials (paid part stays as history, remainder re-billed), keep paid — WITHOUT
        // regenerating the current session's schedule (it already exists; the overlap guard prevents any
        // double-bill). dryRun=true writes NOTHING and only reports what WOULD change.
        var report = new MovedStudentsReconcileReport { DryRun = dryRun };

        var candidates = await _unitOfWork.PaymentsRepo.GetStudentsWithStrandedUnpaidPeriodsAsync();

        foreach (var candidate in candidates)
        {
            var toSession = await _unitOfWork.SessionsRepo
                .GetByIdAndTeacherAsync(candidate.CurrentSessionId, candidate.TeacherId);
            if (toSession is null)
            {
                report.Skipped.Add(new MovedStudentReconcileSkip
                {
                    TeacherId = candidate.TeacherId,
                    TeacherStudentId = candidate.TeacherStudentId,
                    StudentName = candidate.StudentName,
                    Reason = "CurrentSessionNotFound"
                });
                continue;
            }

            var allPeriods = (await _unitOfWork.PaymentsRepo
                .GetAllPaymentPeriodsByStudentAsync(candidate.TeacherId, candidate.TeacherStudentId))
                .ToList();

            // Distinct OLD sessions the student still owes under (excludes the current session).
            var strandedSessionIds = allPeriods
                .Where(p => p.SessionId.HasValue
                    && p.SessionId.Value != candidate.CurrentSessionId
                    && (p.AmountDue - p.AmountPaid) > 0m)
                .Select(p => p.SessionId!.Value)
                .Distinct()
                .ToList();
            if (strandedSessionIds.Count == 0)
                continue; // race: paid/cleaned between the candidate scan and now

            // PerSession is out of scope for the automated monthly carry-over → report + skip.
            bool anyStrandedPerSession = allPeriods.Any(p =>
                p.SessionId.HasValue
                && strandedSessionIds.Contains(p.SessionId.Value)
                && p.PeriodType != PeriodType.Monthly);
            if (toSession.PaymentType != PaymentType.Monthly || anyStrandedPerSession)
            {
                report.Skipped.Add(new MovedStudentReconcileSkip
                {
                    TeacherId = candidate.TeacherId,
                    TeacherStudentId = candidate.TeacherStudentId,
                    StudentName = candidate.StudentName,
                    Reason = "PerSessionNotHandled"
                });
                continue;
            }

            // Overlap guard: months the CURRENT session already bills — a stranded arrear for such a
            // month is left in place (never double-bill), surfaced as OverlapSkipped.
            var destExistingMonths = allPeriods
                .Where(p => p.SessionId == candidate.CurrentSessionId)
                .Select(p => new DateTime(p.PeriodStart.Year, p.PeriodStart.Month, 1))
                .ToHashSet();

            var today = _timeZoneService.GetTeacherLocalDate(candidate.TeacherId);
            var currentMonthEnd = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);

            var item = new MovedStudentReconcileItem
            {
                TeacherId = candidate.TeacherId,
                TeacherStudentId = candidate.TeacherStudentId,
                StudentName = candidate.StudentName,
                StudentCode = candidate.StudentCode,
                CurrentSessionId = candidate.CurrentSessionId,
                CurrentSessionName = toSession.SessionName
            };

            bool execute = !dryRun;
            if (execute) await _unitOfWork.BeginTransactionAsync();
            try
            {
                StudentPaymentCounter? counter = null;
                TeacherStudent? student = null;
                int destSequence = 0;
                if (execute)
                {
                    student = await _unitOfWork.Students
                        .GetActiveByIdAndTeacherAsync(candidate.TeacherStudentId, candidate.TeacherId);
                    if (student is null)
                    {
                        // Purged/deactivated between scan and now — nothing safe to write.
                        await _unitOfWork.RollbackAsync();
                        report.Skipped.Add(new MovedStudentReconcileSkip
                        {
                            TeacherId = candidate.TeacherId,
                            TeacherStudentId = candidate.TeacherStudentId,
                            StudentName = candidate.StudentName,
                            Reason = "StudentNotFound"
                        });
                        continue;
                    }

                    counter = await _unitOfWork.PaymentsRepo
                        .GetPaymentCounterAsync(candidate.TeacherId, candidate.TeacherStudentId);
                    if (counter is null)
                    {
                        counter = new StudentPaymentCounter
                        {
                            TeacherId = candidate.TeacherId,
                            TeacherStudentId = candidate.TeacherStudentId,
                            CreateAt = DateTime.UtcNow
                        };
                        await _unitOfWork.PaymentsRepo.AddPaymentCounterAsync(counter);
                    }

                    destSequence = await _unitOfWork.PaymentsRepo.GetMaxPeriodSequenceAsync(
                        candidate.TeacherId, candidate.TeacherStudentId, candidate.CurrentSessionId) + 1;
                }

                foreach (var fromSessionId in strandedSessionIds)
                {
                    // Include-free periods for classify + apply (matches the live move path).
                    var fromPeriods = await _unitOfWork.PaymentsRepo
                        .GetPaymentPeriodsByStudentAndSessionAsync(
                            candidate.TeacherId, candidate.TeacherStudentId, fromSessionId);

                    var fromSession = await _unitOfWork.SessionsRepo
                        .GetByIdAndTeacherAsync(fromSessionId, candidate.TeacherId);
                    string fromName = fromSession?.SessionName
                        ?? fromPeriods.FirstOrDefault()?.SessionName ?? string.Empty;

                    var plan = BuildCarryOverPlan(fromPeriods, currentMonthEnd, destExistingMonths);

                    var detail = new MovedStudentReconcileDetail
                    {
                        FromSessionId = fromSessionId,
                        FromSessionName = fromName,
                        MovedMonths = plan.UnpaidDueToMove.Select(FormatPeriodLabel).ToList(),
                        CancelledMonths = plan.FutureToCancel.Select(FormatPeriodLabel).ToList(),
                        SettledMonths = plan.PartialsToSplit.Select(FormatPeriodLabel).ToList(),
                        RedundantDeletedMonths = plan.OverlapRedundantToDelete.Select(FormatPeriodLabel).ToList(),
                        OverlapSkippedMonths = plan.OverlapSkipped.Select(FormatPeriodLabel).ToList(),
                        MovedAmount = plan.MovedAmount,
                        CancelledAmount = plan.CancelledAmount,
                        RedundantDeletedAmount = plan.RedundantDeletedAmount,
                        SettledAmount = plan.SettledInSourceAmount,
                        RemainderBilledAmount = plan.SettledRemainderAmount
                    };
                    item.Details.Add(detail);

                    item.PeriodsMoved += plan.UnpaidDueToMove.Count;
                    item.PeriodsCancelled += plan.FutureToCancel.Count;
                    item.PartialsSettled += plan.PartialsToSplit.Count;
                    item.RedundantDeleted += plan.OverlapRedundantToDelete.Count;
                    item.OverlapSkipped += plan.OverlapSkipped.Count;
                    item.MovedAmount += plan.MovedAmount;
                    item.CancelledAmount += plan.CancelledAmount;
                    item.RedundantDeletedAmount += plan.RedundantDeletedAmount;
                    item.SettledAmount += plan.SettledInSourceAmount;
                    item.RemainderBilledAmount += plan.SettledRemainderAmount;

                    if (execute)
                    {
                        // Capture the transfer snapshot BEFORE apply mutates the partials' AmountDue.
                        decimal outstandingCarried = plan.OutstandingCarried;
                        var statusAtTransfer = plan.StatusAtTransfer;

                        destSequence = await ApplyCarryOverPlanAsync(
                            plan, candidate.TeacherId, candidate.TeacherStudentId, student!,
                            fromSessionId, fromName, candidate.CurrentSessionId, toSession.SessionName,
                            destSequence);

                        // Only write an audit row when something actually moved/settled.
                        if (plan.UnpaidDueToMove.Count > 0 || plan.PartialsToSplit.Count > 0
                            || plan.FutureToCancel.Count > 0)
                        {
                            await _unitOfWork.PaymentsRepo.AddSessionTransferEventAsync(new SessionTransferEvent
                            {
                                TeacherId = candidate.TeacherId,
                                TeacherStudentId = candidate.TeacherStudentId,
                                SourceSessionId = fromSessionId,
                                SourceSessionName = fromName,
                                DestinationSessionId = candidate.CurrentSessionId,
                                DestinationSessionName = toSession.SessionName,
                                PaymentStatusAtTransfer = statusAtTransfer,
                                OutstandingBalance = outstandingCarried,
                                CreditBalance = 0m,
                                SourcePaymentType = PaymentType.Monthly.ToString(),
                                DestinationPaymentType = toSession.PaymentType.ToString(),
                                StudentName = student!.StudentName,
                                StudentCode = student.StudentCode,
                                TransferredAt = DateTime.UtcNow,
                                TransferredByUserId = null, // ops reconcile — no explicit actor
                                CreateAt = DateTime.UtcNow
                            });
                        }
                    }
                }

                if (execute)
                {
                    await _unitOfWork.SaveChangesAsync();
                    await RecomputeStudentPaymentCounterAsync(
                        candidate.TeacherId, candidate.TeacherStudentId, counter!);
                    await _unitOfWork.SaveChangesAsync();
                    await _unitOfWork.CommitAsync();
                }
            }
            catch
            {
                if (execute) await _unitOfWork.RollbackAsync();
                throw;
            }

            report.Students.Add(item);
            report.StudentsAffected++;
            report.TotalMovedAmount += item.MovedAmount;
            report.TotalCancelledAmount += item.CancelledAmount;
            report.TotalRedundantDeletedAmount += item.RedundantDeletedAmount;
            report.TotalSettledAmount += item.SettledAmount;
            report.TotalRemainderBilledAmount += item.RemainderBilledAmount;
        }

        report.StudentsSkipped = report.Skipped.Count;
        return Result<MovedStudentsReconcileReport>.Success(
            report, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<BackfillPaidMonthReport>> BackfillPaidMonthAsync(
        long teacherStudentId, string targetMonth, string fromAdvanceMonth, bool dryRun)
    {
        // ── Parse the two "YYYY-MM" months (first-of-month anchors). ──
        if (!TryParseYearMonthToStart(targetMonth, out var targetStart)
            || !TryParseYearMonthToStart(fromAdvanceMonth, out var fromStart))
            return Result<BackfillPaidMonthReport>.Failure(
                _localizer, PaymentConstants.Messages.BackfillInvalidMonthFormat,
                HttpStatusCode.UnprocessableEntity);
        var targetEnd = targetStart.AddMonths(1).AddDays(-1);

        // ── Load the whole (tracked) period timeline for this student. TeacherStudentId is global. ──
        var periods = await _unitOfWork.PaymentsRepo
            .GetTrackedPaymentPeriodsByStudentAsync(teacherStudentId);
        if (periods.Count == 0)
            return Result<BackfillPaidMonthReport>.Failure(
                _localizer, PaymentConstants.Messages.BackfillStudentHasNoPeriods,
                HttpStatusCode.NotFound);

        long teacherId = periods[0].TeacherId;

        // ── Guard: NO period may already exist at the target month. ──
        bool targetExists = periods.Any(p =>
            p.PeriodStart.Year == targetStart.Year && p.PeriodStart.Month == targetStart.Month);
        if (targetExists)
            return Result<BackfillPaidMonthReport>.Failure(
                _localizer, PaymentConstants.Messages.BackfillTargetMonthExists,
                HttpStatusCode.UnprocessableEntity);

        // ── Guard: the advance month period must EXIST, be Monthly, and be fully cash-paid. ──
        var fromPeriod = periods.FirstOrDefault(p =>
            p.PeriodStart.Year == fromStart.Year && p.PeriodStart.Month == fromStart.Month);
        if (fromPeriod is null)
            return Result<BackfillPaidMonthReport>.Failure(
                _localizer, PaymentConstants.Messages.BackfillAdvanceMonthNotFound,
                HttpStatusCode.UnprocessableEntity);
        if (fromPeriod.PeriodType != PeriodType.Monthly)
            return Result<BackfillPaidMonthReport>.Failure(
                _localizer, PaymentConstants.Messages.BackfillAdvanceMonthNotMonthly,
                HttpStatusCode.UnprocessableEntity);
        // Fully paid BY CASH: status Paid AND cash actually covers it (a forgiven-only "Paid" month has
        // no cash to move, so it is rejected rather than silently moving 0).
        if (fromPeriod.PaymentStatus != PaymentStatus.Paid
            || fromPeriod.AmountPaid < fromPeriod.AmountDue)
            return Result<BackfillPaidMonthReport>.Failure(
                _localizer, PaymentConstants.Messages.BackfillAdvanceMonthNotPaid,
                HttpStatusCode.UnprocessableEntity);

        // ── Compute what the move produces (identical for preview + apply). ──
        decimal monthlyRate = await _unitOfWork.PaymentsRepo
            .GetStudentMonthlyRateAsync(teacherId, teacherStudentId);
        decimal movedAmountPaid = fromPeriod.AmountPaid;
        int minSequence = periods.Min(p => p.PeriodSequence);
        int targetSequence = minSequence - 1; // sorts before the earliest existing period
        decimal totalPaidBefore = periods.Sum(p => p.AmountPaid);

        var allocations = await _unitOfWork.PaymentsRepo
            .GetAllocationsByPeriodAsync(fromPeriod.Id);

        var report = new BackfillPaidMonthReport
        {
            DryRun = dryRun,
            TeacherId = teacherId,
            TeacherStudentId = teacherStudentId,
            StudentName = fromPeriod.StudentName,
            StudentCode = fromPeriod.StudentCode,
            SessionId = fromPeriod.SessionId,
            SessionName = fromPeriod.SessionName,
            TargetMonth = $"{targetStart.Year:D4}-{targetStart.Month:D2}",
            TargetMonthLabel = targetStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            TargetAmountDue = monthlyRate,
            TargetAmountPaid = movedAmountPaid,
            TargetPeriodSequence = targetSequence,
            FromAdvanceMonth = $"{fromStart.Year:D4}-{fromStart.Month:D2}",
            FromAdvanceMonthLabel = fromStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            FromAdvancePeriodId = fromPeriod.Id,
            FromAdvancePreviousAmountPaid = fromPeriod.AmountPaid,
            AllocationsMoved = allocations.Count,
            AllocationsMovedAmount = allocations.Sum(a => a.AmountApplied),
            // Set below: preview counts the non-deleted transactions currently pointing at the advance
            // period; apply reports the actual repoint count (same predicate).
            TransactionsRepointed = 0,
            TotalPaidBefore = totalPaidBefore,
            TotalPaidAfter = totalPaidBefore, // invariant — cash only moves months, never changes
        };

        if (dryRun)
        {
            // Count (read-only) how many transactions WOULD be repointed, for the preview.
            var wouldRepoint = await _unitOfWork.PaymentsRepo
                .GetTransactionsByPeriodAsync(fromPeriod.Id);
            report.TransactionsRepointed = wouldRepoint.Count;
            return Result<BackfillPaidMonthReport>.Success(
                report, _localizer, PaymentConstants.Messages.BackfillSuccess);
        }

        // ── APPLY (one transaction). ──
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync();
        try
        {
            // (1) Create the target month period (Paid), sequenced before the earliest existing period.
            var targetPeriod = new PaymentPeriod
            {
                TeacherId = teacherId,
                SessionId = fromPeriod.SessionId,
                TeacherStudentId = teacherStudentId,
                StudentSessionAssignmentId = fromPeriod.StudentSessionAssignmentId,
                PeriodType = PeriodType.Monthly,
                PeriodStart = targetStart,
                PeriodEnd = targetEnd,
                AmountDue = monthlyRate,
                AmountPaid = movedAmountPaid,
                PaymentStatus = PaymentStatus.Paid,
                IsProRated = false,
                ProRatedFraction = 1.0m,
                PeriodSequence = targetSequence,
                SessionName = fromPeriod.SessionName,
                StudentName = fromPeriod.StudentName,
                StudentCode = fromPeriod.StudentCode,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.PaymentsRepo.AddPaymentPeriodAsync(targetPeriod);
            await _unitOfWork.SaveChangesAsync(); // materialize targetPeriod.Id for the FK repointing

            // (2) Move the advance period's settlement allocations onto the new target period.
            foreach (var alloc in allocations)
                alloc.PaymentPeriodId = targetPeriod.Id;

            // (4) Repoint the denormalized transaction → period FK for any tx pointing at the advance period.
            report.TransactionsRepointed = await _unitOfWork.PaymentsRepo
                .RepointTransactionsToPeriodAsync(fromPeriod.Id, targetPeriod.Id);

            // (3) Un-settle the advance month.
            fromPeriod.AmountPaid = 0m;
            fromPeriod.PaymentStatus = RecomputePeriodStatus(fromPeriod);
            await _unitOfWork.PaymentsRepo.UpdatePaymentPeriodAsync(fromPeriod);

            await _unitOfWork.SaveChangesAsync();

            // (5) Recompute the student's counter from the new period picture.
            var counter = await _unitOfWork.PaymentsRepo
                .GetPaymentCounterAsync(teacherId, teacherStudentId);
            if (counter is not null)
            {
                await RecomputeStudentPaymentCounterAsync(teacherId, teacherStudentId, counter);
                await _unitOfWork.SaveChangesAsync();
            }

            if (ownsTransaction) await _unitOfWork.CommitAsync();

            report.TargetPeriodId = targetPeriod.Id;
            // Re-affirm the invariant from persisted state.
            var after = await _unitOfWork.PaymentsRepo
                .GetTrackedPaymentPeriodsByStudentAsync(teacherStudentId);
            report.TotalPaidAfter = after.Sum(p => p.AmountPaid);

            return Result<BackfillPaidMonthReport>.Success(
                report, _localizer, PaymentConstants.Messages.BackfillSuccess);
        }
        catch
        {
            if (ownsTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <summary>Parses a "YYYY-MM" string to the first day of that month (UTC-agnostic date). </summary>
    private static bool TryParseYearMonthToStart(string? yearMonth, out DateTime monthStart)
    {
        monthStart = default;
        if (string.IsNullOrWhiteSpace(yearMonth)) return false;
        if (!DateTime.TryParseExact(
                yearMonth.Trim(), "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
            return false;
        monthStart = new DateTime(parsed.Year, parsed.Month, 1);
        return true;
    }

    /// <inheritdoc />
    public async Task<Result<RecomputeAssistantWalletReport>> RecomputeAssistantWalletAsync(
        long assistantId, bool dryRun)
    {
        var wallet = await _unitOfWork.PaymentsRepo.GetAssistantWalletByAssistantIdAsync(assistantId);
        if (wallet is null)
            return Result<RecomputeAssistantWalletReport>.Failure(
                _localizer, PaymentConstants.Messages.WalletNotFound, HttpStatusCode.NotFound);

        long teacherId = wallet.TeacherId;
        long userId = wallet.AssistantUserId;
        DateTime now = DateTime.UtcNow;

        // Full signed history for this collector (same sources the wallet SCREEN uses, so the recompute
        // stays consistent with it):
        //  • collections — IgnoreQueryFilters so soft-deleted (fully-refunded) rows are included with
        //    their preserved AmountPaid; needed so the anchor sees cash that WAS held at hand-over time.
        //  • allRefunds — every refund (Deleted + Reversed), each carrying its underlying collection's
        //    CollectedAt AND its own RefundedAt; used to reconstruct the balance AT each reset (a refund
        //    already taken by then reduces the balance the reset handed over).
        //  • reversedRefunds — includeDeleted:false → only Reversed (partial departure) refunds, whose
        //    collection is still non-deleted; used for the post-anchor held-cash calc (a fully-deleted
        //    collection is excluded from held cash, so its Deleted refund must NOT be subtracted again).
        //  • hand-overs — full resets AND partial withdrawals (both are WalletResetLog rows).
        var txns = await _unitOfWork.PaymentsRepo
            .GetCollectorTransactionsInRangeAsync(teacherId, userId, DateTime.MinValue, now);
        var allRefunds = await _unitOfWork.PaymentsRepo
            .GetCollectorRefundsInRangeAsync(teacherId, userId, DateTime.MinValue, now, includeDeleted: true);
        var reversedRefunds = await _unitOfWork.PaymentsRepo
            .GetCollectorRefundsInRangeAsync(teacherId, userId, DateTime.MinValue, now, includeDeleted: false);
        var resets = wallet.AssistantId.HasValue
            ? await _unitOfWork.PaymentsRepo.GetWalletResetLogsAsync(teacherId, wallet.AssistantId.Value)
            : (wallet.CenterAssistantId.HasValue
                ? await _unitOfWork.PaymentsRepo.GetWalletResetLogsForCenterAssistantAsync(teacherId, wallet.CenterAssistantId.Value)
                : (IReadOnlyList<WalletResetLog>)Array.Empty<WalletResetLog>());

        // ── Anchor: the last FULL cash hand-over — a reset that left the collector holding NOTHING.
        // A gross collections-vs-handovers replay is fragile: pre-reset refunds shift the sums so the
        // running balance may never cross exactly 0 at the true hand-over (salma). Instead, evaluate the
        // RECONSTRUCTED balance right after EACH reset and take the LATEST one that is exactly 0:
        //     balanceAfter(R) = Σ collections collected by R (incl. later-deleted — they were held then)
        //                     − Σ refunds taken by R (Deleted + Reversed — cash already given back)
        //                     − Σ cash handed over by R (every reset/withdrawal up to and incl. R).
        // A full "take everything" reset (ResetWalletAsync, or a WithdrawFromWalletAsync that empties the
        // wallet) makes this 0; a PARTIAL withdrawal leaves it > 0 and is skipped. There is no full/partial
        // flag on WalletResetLog, so this reconstruction is how a full reset is identified. For salma the
        // phantom DELETE refunds happened AFTER her last full reset, so they don't affect balanceAfter at
        // that reset → it correctly anchors there; the deletes are the corruption this repairs.
        DateTime? anchor = null;
        foreach (var r in resets.OrderBy(x => x.ResetAt))
        {
            decimal collectedBy = txns.Where(t => t.CollectedAt <= r.ResetAt).Sum(t => t.AmountPaid);
            decimal refundedBy = allRefunds.Where(f => f.RefundedAt <= r.ResetAt).Sum(f => f.RefundAmount);
            decimal handedOverBy = resets.Where(x => x.ResetAt <= r.ResetAt).Sum(x => x.AmountReset);
            if (collectedBy - refundedBy - handedOverBy == 0m)
                anchor = r.ResetAt; // a full hand-over; keep the latest one
        }
        DateTime anchorAt = anchor ?? DateTime.MinValue;

        // ── Held cash after the anchor = still-held collections − their partial (departure) reversals −
        // partial withdrawals recorded after the anchor. Fully-deleted collections are excluded (IsDeleted);
        // their Deleted refunds are not in reversedRefunds (includeDeleted:false), so no double count. When
        // no full reset was ever taken (anchor == MinValue) this is the plain net held cash all-time.
        var postCollections = txns.Where(t => !t.IsDeleted && t.CollectedAt > anchorAt).ToList();
        decimal postCollectionsSum = postCollections.Sum(t => t.AmountPaid);
        decimal postReversalsSum = reversedRefunds.Where(r => r.CollectedAt > anchorAt).Sum(r => r.RefundAmount);
        decimal postWithdrawalsSum = resets.Where(r => r.ResetAt > anchorAt).Sum(r => r.AmountReset);
        decimal newBalance = postCollectionsSum - postReversalsSum - postWithdrawalsSum;

        var report = new RecomputeAssistantWalletReport
        {
            DryRun = dryRun,
            TeacherId = teacherId,
            AssistantId = assistantId,
            AssistantUserId = userId,
            AssistantName = wallet.Assistant?.User?.FullName ?? wallet.CenterAssistant?.User?.FullName,
            OldBalance = wallet.CurrentBalance,
            NewBalance = newBalance,
            Delta = newBalance - wallet.CurrentBalance,
            AnchorHandoverAt = anchor,
            PostHandoverCollections = postCollectionsSum,
            PostHandoverCollectionCount = postCollections.Count,
            PostHandoverReversals = postReversalsSum,
            PostHandoverWithdrawals = postWithdrawalsSum
        };

        if (dryRun)
            return Result<RecomputeAssistantWalletReport>.Success(
                report, _localizer, PaymentConstants.Messages.RecomputeWalletSuccess);

        // ── APPLY: set CurrentBalance only. TotalCollected (lifetime) is intentionally untouched.
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync();
        try
        {
            wallet.CurrentBalance = newBalance;
            await _unitOfWork.PaymentsRepo.UpdateAssistantWalletAsync(wallet);
            await _unitOfWork.SaveChangesAsync();
            if (ownsTransaction) await _unitOfWork.CommitAsync();
            return Result<RecomputeAssistantWalletReport>.Success(
                report, _localizer, PaymentConstants.Messages.RecomputeWalletSuccess);
        }
        catch
        {
            if (ownsTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
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
            // Forgiven amount is already settled — cash tops up only the still-owed remainder.
            decimal remaining = p.AmountDue - p.AmountPaid - (p.ForgivenAmount ?? 0m);
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
    private static PaymentStatus RecomputePeriodStatus(PaymentPeriod period)
    {
        // Forgiven amount counts toward settlement (it reduces what's owed), just like paid cash.
        decimal settled = period.AmountPaid + (period.ForgivenAmount ?? 0m);
        return settled >= period.AmountDue
            ? PaymentStatus.Paid
            : settled > 0m
                ? PaymentStatus.PartiallyPaid
                : PaymentStatus.Unpaid;
    }

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
        if (wallet is null)
        {
            // A CenterAssistant collector has no permission-grant flow that pre-creates a wallet, so
            // create it lazily on first collect (keyed by TeacherId + their userId). A teacher-owner
            // collector has no wallet by design → skip.
            var centerAssistant = await _unitOfWork.Centers.GetCenterAssistantByUserIdAsync(collectedByUserId);
            if (centerAssistant is null) return;

            wallet = new AssistantWallet
            {
                TeacherId = teacherId,
                CenterAssistantId = centerAssistant.Id,
                AssistantUserId = collectedByUserId,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.PaymentsRepo.AddAssistantWalletAsync(wallet);
            await _unitOfWork.SaveChangesAsync();
        }

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
    ///
    /// RESET-AWARE (salma −2700 bug): a REVERSAL (<paramref name="delta"/> &lt; 0) of cash the collector
    /// already handed to the tutor — i.e. the reversed transaction was collected ON/BEFORE the wallet's
    /// most recent hand-over (<c>WalletResetLog.ResetAt</c>) — must NOT move <c>CurrentBalance</c>: that
    /// cash is no longer in the assistant's holding (it left via the reset), so subtracting it drives the
    /// wallet falsely negative. Such pre-reset reversals STILL move <c>TotalCollected</c> (lifetime) and
    /// the caller still updates the student counter + writes the audit row — only the held-cash balance
    /// is left alone. A POST-reset reversal decrements <c>CurrentBalance</c> normally, and a positive
    /// delta (an edit-up = new cash now held) always moves it. When
    /// <paramref name="reversedCollectionAt"/> is null the comparison is skipped (legacy behaviour).
    /// Mirrors <see cref="UpdateAssistantWalletAfterCollectionAsync"/>.
    /// </summary>
    private async Task AdjustAssistantWalletAsync(
        long teacherId, long? collectedByUserId, decimal delta, DateTime? reversedCollectionAt = null)
    {
        if (collectedByUserId is null || delta == 0m) return;

        var wallet = await _unitOfWork.PaymentsRepo
            .GetAssistantWalletByUserIdAsync(teacherId, collectedByUserId.Value);
        if (wallet is null) return; // collector is not an assistant — no wallet to adjust

        // Decide ONCE whether this reversal touches held cash. Only a negative delta (a reversal) whose
        // reversed collection predates the wallet's last hand-over is "already handed over".
        bool affectsCurrentBalance = true;
        if (delta < 0m && reversedCollectionAt.HasValue)
        {
            var lastResetAt = await _unitOfWork.PaymentsRepo
                .GetLastWalletResetAtByWalletIdAsync(teacherId, wallet.Id);
            if (lastResetAt.HasValue && reversedCollectionAt.Value <= lastResetAt.Value)
                affectsCurrentBalance = false; // pre-reset cash — the tutor holds it now, not the wallet
        }

        for (int retry = 0; retry < PaymentConstants.MaxConcurrencyRetries; retry++)
        {
            try
            {
                // CurrentBalance is signed: a POST-reset refund/reversal that exceeds the cash the
                // assistant still holds drives it negative — money the assistant owes back, kept visible.
                // A PRE-reset reversal leaves CurrentBalance untouched (see method summary).
                // TotalCollected (lifetime) always moves so a reversal never silently loses money.
                if (affectsCurrentBalance)
                    wallet.CurrentBalance += delta;
                wallet.TotalCollected += delta;
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
        OnlineTransactionRef = t.OnlineTransactionRef,
        Note = t.CollectionNote
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
        SessionName = DisplaySessionName(p),
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
        MovedFromSessionId = p.MovedFromSessionId,
        MovedFromSessionName = p.MovedFromSessionName,
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