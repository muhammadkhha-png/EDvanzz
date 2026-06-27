using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Payment;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements all Event-Based Payment Module (Module 5) operations.
///
/// REQ-EVT-001 through REQ-EVT-029 coverage:
/// - Creation: CreateEventAsync (REQ-EVT-001-008)
/// - Collection: CollectEventPaymentAsync (REQ-EVT-009-013)
/// - Tracking: GetEventsAsync, GetEventTrackingAsync (REQ-EVT-014-015)
/// - Management: UpdateEventAsync, DeleteEventAsync (REQ-EVT-020-022)
/// - Reporting: GenerateEventReportAsync, ExportEventReportAsync (REQ-EVT-023-026)
///
/// TRANSACTION SAFETY: All write operations use the ownsTransaction pattern.
/// </summary>
public class EventPaymentService : IEventPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentReportExportService _exportService;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;
    private readonly IPaymentNotifier paymanetNotifier;

    public EventPaymentService(
        IUnitOfWork unitOfWork,
        IPaymentReportExportService exportService,
        IStringLocalizer<Domain.Resources.Messages> localizer,IPaymentNotifier paymanetNotifier)
    {
        _unitOfWork = unitOfWork;
        _exportService = exportService;
        _localizer = localizer;
        this.paymanetNotifier = paymanetNotifier;
    }

    /// <inheritdoc />
    public async Task<Result<EventDto>> CreateEventAsync(CreateEventDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.EventName))
            return Result<EventDto>.Failure(
                _localizer, PaymentConstants.Messages.EventNameRequired, HttpStatusCode.BadRequest);

        if (dto.EventAmount <= 0)
            return Result<EventDto>.Failure(
                _localizer, PaymentConstants.Messages.EventAmountInvalid, HttpStatusCode.BadRequest);

        if (dto.TargetScopes.Count == 0)
            return Result<EventDto>.Failure(
                _localizer, PaymentConstants.Messages.EventTargetScopeEmpty, HttpStatusCode.BadRequest);

        // REQ-EVT-004: Resolve and deduplicate students across all combined scopes
        var studentIds = await ResolveAndDeduplicateTargetStudentsAsync(dto.TeacherId, dto.TargetScopes);
        if (studentIds.Count == 0)
            return Result<EventDto>.Failure(
                _localizer, PaymentConstants.Messages.EventTargetScopeEmpty, HttpStatusCode.BadRequest);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Store the primary scope type (first entry) for display, and all scope IDs for reference
            var primaryScope = dto.TargetScopes.First();

            var paymentEvent = new PaymentEvent
            {
                TeacherId = dto.TeacherId,
                EventName = dto.EventName,
                EventAmount = dto.EventAmount,
                Notes = dto.Notes,
                TargetScopeType = dto.TargetScopes.Count == 1
                    ? primaryScope.ScopeType : EventTargetScopeType.IndividualStudents,
                TargetScopeIds = string.Join(",", studentIds),
                EventDate = dto.EventDate,
                TotalStudents = studentIds.Count,
                TotalExpectedRevenue = dto.EventAmount * studentIds.Count,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.PaymentsRepo.AddPaymentEventAsync(paymentEvent);
            await _unitOfWork.SaveChangesAsync(); // Get the event ID

            // BR-EVT-001: Create obligations for all students in scope at creation time
            var obligations = new List<EventStudentObligation>();
            foreach (var studentId in studentIds)
            {
                var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(studentId, dto.TeacherId);
                obligations.Add(new EventStudentObligation
                {
                    TeacherId = dto.TeacherId,
                    PaymentEventId = paymentEvent.Id,
                    TeacherStudentId = studentId,
                    StudentName = student?.StudentName,
                    StudentCode = student?.StudentCode,
                    AmountDue = dto.EventAmount,
                    PaymentStatus = PaymentStatus.Unpaid,
                    CreateAt = DateTime.UtcNow
                });
            }
            await _unitOfWork.PaymentsRepo.AddEventObligationsRangeAsync(obligations);

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<EventDto>.Success(MapToEventDto(paymentEvent), _localizer, PaymentConstants.Messages.EventCreatedSuccess);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<EventDto>>>> GetEventsAsync(
        long teacherId, EventListFilterDto filter)
    {
        var (items, totalCount) = await _unitOfWork.PaymentsRepo
            .GetPaymentEventsFilteredPagedAsync(teacherId,
                filter.SearchName, filter.ScopeTypeFilter, filter.CompletionStatus,
                filter.Page, filter.PageSize);

        var dtos = items.Select(MapToEventDto).ToList();

        var response = new PaginatedResponse<List<EventDto>>
        {
            totalCount = totalCount,
            page = filter.Page,
            pageSize = filter.PageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize),
            data = dtos
        };

        return Result<PaginatedResponse<List<EventDto>>>.Success(
            response, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<EventTrackingDto>> GetEventTrackingAsync(
        long teacherId, long eventId, string? search, int page, int pageSize)
    {
        var paymentEvent = await _unitOfWork.PaymentsRepo
            .GetPaymentEventByIdAndTeacherAsync(eventId, teacherId);
        if (paymentEvent is null)
            return Result<EventTrackingDto>.Failure(
                _localizer, PaymentConstants.Messages.EventNotFound, HttpStatusCode.NotFound);

        // REQ-EVT-016: Search by student name or student code within paid/unpaid lists
        var (paidItems, _) = await _unitOfWork.PaymentsRepo
            .GetEventObligationsPagedAsync(eventId, teacherId, PaymentStatus.Paid, search, page, pageSize);

        var (unpaidItems, _) = await _unitOfWork.PaymentsRepo
            .GetEventObligationsPagedAsync(eventId, teacherId, PaymentStatus.Unpaid, search, page, pageSize);

        var tracking = new EventTrackingDto
        {
            Event = MapToEventDto(paymentEvent),
            PaidStudents = paidItems.Select(MapToObligationDto).ToList(),
            UnpaidStudents = unpaidItems.Select(MapToObligationDto).ToList()
        };

        return Result<EventTrackingDto>.Success(tracking, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<EventDto>> UpdateEventAsync(UpdateEventDto dto)
    {
        var paymentEvent = await _unitOfWork.PaymentsRepo
            .GetPaymentEventByIdAndTeacherAsync(dto.EventId, dto.TeacherId);
        if (paymentEvent is null)
            return Result<EventDto>.Failure(
                _localizer, PaymentConstants.Messages.EventNotFound, HttpStatusCode.NotFound);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            if (dto.EventName is not null) paymentEvent.EventName = dto.EventName;
            if (dto.Notes is not null) paymentEvent.Notes = dto.Notes;

            // REQ-EVT-019: If amount changes, update only unpaid obligations
            if (dto.EventAmount.HasValue && dto.EventAmount.Value != paymentEvent.EventAmount)
            {
                decimal oldAmount = paymentEvent.EventAmount;
                paymentEvent.EventAmount = dto.EventAmount.Value;

                // Update unpaid obligations to the new amount
                var (unpaidObligations, _) = await _unitOfWork.PaymentsRepo
                    .GetEventObligationsPagedAsync(dto.EventId, dto.TeacherId, PaymentStatus.Unpaid, null, 1, 50000);
                foreach (var obligation in unpaidObligations)
                {
                    obligation.AmountDue = dto.EventAmount.Value;
                    await _unitOfWork.PaymentsRepo.UpdateEventObligationAsync(obligation);
                }

                // Recalculate expected revenue
                var (allObligations, totalCount) = await _unitOfWork.PaymentsRepo
                    .GetEventObligationsPagedAsync(dto.EventId, dto.TeacherId, null, null, 1, 50000);
                paymentEvent.TotalExpectedRevenue = allObligations.Sum(o => o.AmountDue);
            }

            // REQ-EVT-021: Remove students (only if not paid)
            if (dto.StudentIdsToRemove?.Count > 0)
            {
                foreach (var studentId in dto.StudentIdsToRemove)
                {
                    var obligation = await _unitOfWork.PaymentsRepo
                        .GetEventObligationAsync(dto.EventId, studentId);
                    if (obligation is null) continue;

                    // BR-EVT-003: Cannot remove if student has paid
                    if (obligation.AmountPaid > 0) continue;

                    await _unitOfWork.PaymentsRepo.DeleteEventObligationAsync(obligation);
                    paymentEvent.TotalStudents--;
                    paymentEvent.TotalExpectedRevenue -= obligation.AmountDue;
                }
            }

            // REQ-EVT-020: Add new students
            if (dto.StudentIdsToAdd?.Count > 0)
            {
                var newObligations = new List<EventStudentObligation>();
                foreach (var studentId in dto.StudentIdsToAdd)
                {
                    var existing = await _unitOfWork.PaymentsRepo
                        .GetEventObligationAsync(dto.EventId, studentId);
                    if (existing is not null) continue;

                    var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(studentId, dto.TeacherId);
                    newObligations.Add(new EventStudentObligation
                    {
                        TeacherId = dto.TeacherId,
                        PaymentEventId = dto.EventId,
                        TeacherStudentId = studentId,
                        StudentName = student?.StudentName,
                        StudentCode = student?.StudentCode,
                        AmountDue = paymentEvent.EventAmount,
                        PaymentStatus = PaymentStatus.Unpaid,
                        CreateAt = DateTime.UtcNow
                    });
                }
                if (newObligations.Count > 0)
                {
                    await _unitOfWork.PaymentsRepo.AddEventObligationsRangeAsync(newObligations);
                    paymentEvent.TotalStudents += newObligations.Count;
                    paymentEvent.TotalExpectedRevenue += paymentEvent.EventAmount * newObligations.Count;
                }
            }

            await _unitOfWork.PaymentsRepo.UpdatePaymentEventAsync(paymentEvent);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<EventDto>.Success(MapToEventDto(paymentEvent), _localizer, PaymentConstants.Messages.EventUpdatedSuccess);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> SetEventStudentCustomAmountAsync(SetEventStudentCustomAmountDto dto)
    {
        var obligation = await _unitOfWork.PaymentsRepo
            .GetEventObligationAsync(dto.EventId, dto.TeacherStudentId);
        if (obligation is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.EventObligationNotFound, HttpStatusCode.NotFound);

        obligation.AmountDue = dto.CustomAmount;
        await _unitOfWork.PaymentsRepo.UpdateEventObligationAsync(obligation);

        // Recalculate event expected revenue
        var paymentEvent = await _unitOfWork.PaymentsRepo
            .GetPaymentEventByIdAndTeacherAsync(dto.EventId, dto.TeacherId);
        if (paymentEvent is not null)
        {
            var (allObligations, _) = await _unitOfWork.PaymentsRepo
                .GetEventObligationsPagedAsync(dto.EventId, dto.TeacherId, null, null, 1, 50000);
            paymentEvent.TotalExpectedRevenue = allObligations.Sum(o => o.AmountDue);
            await _unitOfWork.PaymentsRepo.UpdatePaymentEventAsync(paymentEvent);
        }

        await _unitOfWork.SaveChangesAsync();
        return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.EventUpdatedSuccess);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteEventAsync(long teacherId, long eventId)
    {
        var paymentEvent = await _unitOfWork.PaymentsRepo
            .GetPaymentEventByIdAndTeacherAsync(eventId, teacherId);
        if (paymentEvent is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.EventNotFound, HttpStatusCode.NotFound);

        // REQ-EVT-022: Soft-delete; collected EventPaymentTransactions survive via SET NULL FK
        paymentEvent.IsDeleted = true;
        paymentEvent.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.PaymentsRepo.UpdatePaymentEventAsync(paymentEvent);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, PaymentConstants.Messages.EventDeletedSuccess);
    }

    /// <inheritdoc />
    public async Task<Result<EventPaymentResultDto>> CollectEventPaymentAsync(CollectEventPaymentDto dto)
    {
        dto.CollectedByUserId ??= dto.TeacherId;

        var paymentEvent = await _unitOfWork.PaymentsRepo
            .GetPaymentEventByIdAndTeacherAsync(dto.EventId, dto.TeacherId);
        if (paymentEvent is null)
            return Result<EventPaymentResultDto>.Failure(
                _localizer, PaymentConstants.Messages.EventNotFound, HttpStatusCode.NotFound);

        var obligation = await _unitOfWork.PaymentsRepo
            .GetEventObligationAsync(dto.EventId, dto.TeacherStudentId);
        if (obligation is null)
            return Result<EventPaymentResultDto>.Failure(
                _localizer, PaymentConstants.Messages.EventObligationNotFound, HttpStatusCode.NotFound);

        // REQ-EVT-012: Already-paid warning
        if (obligation.PaymentStatus == PaymentStatus.Paid && !dto.AlreadyPaidConfirmed)
            return Result<EventPaymentResultDto>.Success(new EventPaymentResultDto
            {
                Transaction = null,
                IsAlreadyPaid = true,
                PreviouslyPaidAmount = obligation.AmountPaid
            }, _localizer, PaymentConstants.Messages.EventPaymentAlreadyPaid);

        if (dto.Amount <= 0)
            return Result<EventPaymentResultDto>.Failure(
                _localizer, PaymentConstants.Messages.PaymentAmountInvalid, HttpStatusCode.BadRequest);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(
                dto.TeacherStudentId, dto.TeacherId);

            var transaction = new EventPaymentTransaction
            {
                TeacherId = dto.TeacherId,
                PaymentEventId = dto.EventId,
                EventStudentObligationId = obligation.Id,
                TeacherStudentId = dto.TeacherStudentId,
                AmountPaid = dto.Amount,
                PaymentMethod = dto.PaymentMethod,
                CollectedByUserId = dto.CollectedByUserId,
                StudentName = student?.StudentName ?? obligation.StudentName,
                StudentCode = student?.StudentCode ?? obligation.StudentCode,
                EventName = paymentEvent.EventName,
                CollectedAt = DateTime.UtcNow,
                IsOnlinePayment = dto.PaymentMethod == PaymentCollectionMethod.OnlinePhoneCash
                    || dto.PaymentMethod == PaymentCollectionMethod.OnlineInstaPay,
                OnlineTransactionRef = dto.OnlineTransactionRef,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.PaymentsRepo.AddEventPaymentTransactionAsync(transaction);

            // Update obligation
            obligation.AmountPaid += dto.Amount;
            obligation.PaymentStatus = obligation.AmountPaid >= obligation.AmountDue
                ? PaymentStatus.Paid
                : obligation.AmountPaid > 0
                    ? PaymentStatus.PartiallyPaid
                    : PaymentStatus.Unpaid;
            await _unitOfWork.PaymentsRepo.UpdateEventObligationAsync(obligation);

            // Update event aggregates
            paymentEvent.TotalCollectedRevenue += dto.Amount;
            await _unitOfWork.PaymentsRepo.UpdatePaymentEventAsync(paymentEvent);

            // REQ-EVT-013: Update assistant wallet
            var paymentService = new PaymentService(_unitOfWork, _localizer, null!, paymanetNotifier);
            // Use repo directly for wallet update instead
            var wallet = await _unitOfWork.PaymentsRepo
                .GetAssistantWalletByUserIdAsync(dto.TeacherId, dto.CollectedByUserId!.Value);
            if (wallet is not null)
            {
                wallet.CurrentBalance += dto.Amount;
                wallet.TotalCollected += dto.Amount;
                wallet.TransactionCount++;
                wallet.LastCollectionAt = DateTime.UtcNow;
                await _unitOfWork.PaymentsRepo.UpdateAssistantWalletAsync(wallet);
            }

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<EventPaymentResultDto>.Success(new EventPaymentResultDto
            {
                Transaction = new EventPaymentTransactionDto
                {
                    Id = transaction.Id,
                    EventName = transaction.EventName,
                    StudentName = transaction.StudentName,
                    StudentCode = transaction.StudentCode,
                    AmountPaid = transaction.AmountPaid,
                    PaymentMethod = transaction.PaymentMethod,
                    CollectedByUserId = transaction.CollectedByUserId,
                    CollectedAt = transaction.CollectedAt,
                    IsOnlinePayment = transaction.IsOnlinePayment
                }
            }, _localizer, PaymentConstants.Messages.EventPaymentCollectedSuccess);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<object>> GenerateEventReportAsync(
        long teacherId, EventReportRequestDto request)
    {
        if (request.ReportType == EventReportType.SingleEvent)
        {
            if (!request.EventId.HasValue)
                return Result<object>.Failure(
                    _localizer, PaymentConstants.Messages.EventNotFound, HttpStatusCode.BadRequest);

            var paymentEvent = await _unitOfWork.PaymentsRepo
                .GetPaymentEventByIdAndTeacherAsync(request.EventId.Value, teacherId);
            if (paymentEvent is null)
                return Result<object>.Failure(
                    _localizer, PaymentConstants.Messages.EventNotFound, HttpStatusCode.NotFound);

            var (paidItems, _) = await _unitOfWork.PaymentsRepo
                .GetEventObligationsPagedAsync(request.EventId.Value, teacherId, PaymentStatus.Paid, null, 1, 50000);
            var (unpaidItems, _) = await _unitOfWork.PaymentsRepo
                .GetEventObligationsPagedAsync(request.EventId.Value, teacherId, PaymentStatus.Unpaid, null, 1, 50000);

            var report = new SingleEventReportDto
            {
                Event = MapToEventDto(paymentEvent),
                PaidStudents = paidItems.Select(MapToObligationDto).ToList(),
                UnpaidStudents = unpaidItems.Select(MapToObligationDto).ToList()
            };
            return Result<object>.Success(report, _localizer, PaymentConstants.Messages.EventReportGenerated);
        }
        else // AllEventsSummary
        {
            var (events, _) = await _unitOfWork.PaymentsRepo
                .GetPaymentEventsPagedAsync(teacherId, 1, 50000);

            var report = new AllEventsSummaryReportDto
            {
                Events = events.Select(e => new EventSummaryRowDto
                {
                    EventId = e.Id,
                    EventName = e.EventName,
                    EventDate = e.EventDate,
                    TotalStudents = e.TotalStudents,
                    TotalExpected = e.TotalExpectedRevenue,
                    TotalCollected = e.TotalCollectedRevenue,
                    Outstanding = e.TotalExpectedRevenue - e.TotalCollectedRevenue,
                    CompletionPercentage = e.TotalExpectedRevenue > 0
                        ? Math.Round(e.TotalCollectedRevenue / e.TotalExpectedRevenue * 100, 1) : 0
                }).ToList(),
                GrandTotalExpected = events.Sum(e => e.TotalExpectedRevenue),
                GrandTotalCollected = events.Sum(e => e.TotalCollectedRevenue),
                GrandTotalOutstanding = events.Sum(e => e.TotalExpectedRevenue - e.TotalCollectedRevenue)
            };
            return Result<object>.Success(report, _localizer, PaymentConstants.Messages.EventReportGenerated);
        }
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> ExportEventReportAsync(
        long teacherId, EventReportRequestDto request, string format)
    {
        if (format != "pdf" && format != "xlsx")
            return Result<byte[]>.Failure(
                _localizer, PaymentConstants.Messages.InvalidExportFormat, HttpStatusCode.BadRequest);

        var reportResult = await GenerateEventReportAsync(teacherId, request);
        if (!reportResult.IsSuccess)
            return Result<byte[]>.Failure(reportResult.Message!, reportResult.StatusCode);

        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        var header = new PaymentReportHeaderDto
        {
            TutorAccountName = teacher?.User?.FullName ?? "Unknown",
            ReportType = request.ReportType.ToString(),
            ReportTitle = request.ReportType == EventReportType.SingleEvent
                ? "Single Event Payment Report" : "All Events Summary Report",
            GeneratedAt = DateTime.UtcNow
        };

        var fileBytes = await _exportService.ExportEventReportAsync(header, reportResult.Data!, format);
        return Result<byte[]>.Success(fileBytes, _localizer, PaymentConstants.Messages.ExportCompleted);
    }

    // ══════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════

    /// <summary>
    /// REQ-EVT-004: Resolves all scope entries and deduplicates students across combined scopes.
    /// "The system shall automatically deduplicate any students who appear in multiple selected scopes
    /// so no student receives a duplicate payment obligation for the same event."
    /// </summary>
    private async Task<List<long>> ResolveAndDeduplicateTargetStudentsAsync(
        long teacherId, List<EventScopeEntry> scopes)
    {
        var allStudentIds = new HashSet<long>();

        foreach (var scope in scopes)
        {
            List<long> resolved = scope.ScopeType switch
            {
                EventTargetScopeType.IndividualStudents => scope.ScopeIds,
                EventTargetScopeType.Session => await _unitOfWork.PaymentsRepo
                    .GetStudentIdsBySessionAsync(teacherId, scope.ScopeIds.FirstOrDefault()),
                EventTargetScopeType.SessionGroup => await _unitOfWork.PaymentsRepo
                    .GetStudentIdsByGroupAsync(teacherId, scope.ScopeIds.FirstOrDefault()),
                EventTargetScopeType.AllStudents => await _unitOfWork.PaymentsRepo
                    .GetAllStudentIdsAsync(teacherId),
                _ => new List<long>()
            };

            foreach (var id in resolved)
                allStudentIds.Add(id); // HashSet automatically deduplicates
        }

        return allStudentIds.ToList();
    }

    private static EventDto MapToEventDto(PaymentEvent e) => new()
    {
        Id = e.Id,
        EventName = e.EventName,
        EventAmount = e.EventAmount,
        TargetScopeType = e.TargetScopeType,
        EventDate = e.EventDate,
        CreateAt = e.CreateAt,
        Notes = e.Notes,
        TotalStudents = e.TotalStudents,
        TotalExpectedRevenue = e.TotalExpectedRevenue,
        TotalCollectedRevenue = e.TotalCollectedRevenue,
        RemainingRevenue = e.TotalExpectedRevenue - e.TotalCollectedRevenue
    };

    private static EventStudentObligationDto MapToObligationDto(EventStudentObligation o) => new()
    {
        Id = o.Id,
        TeacherStudentId = o.TeacherStudentId,
        StudentName = o.StudentName,
        StudentCode = o.StudentCode,
        AmountDue = o.AmountDue,
        AmountPaid = o.AmountPaid,
        Outstanding = o.AmountDue - o.AmountPaid,
        PaymentStatus = o.PaymentStatus
    };
}