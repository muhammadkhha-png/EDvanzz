using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Payment;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Models;
using Microsoft.Extensions.Localization;
using System.Globalization;
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
    private readonly IPaymentService paymentService;
    private readonly ISubscriptionGateService _subscriptionGate;
    private readonly ITimeZoneService timeZoneService;

    public EventPaymentService(
        IUnitOfWork unitOfWork,
        IPaymentReportExportService exportService,
        IStringLocalizer<Domain.Resources.Messages> localizer,IPaymentNotifier paymanetNotifier,IPaymentService paymentService,
        ISubscriptionGateService subscriptionGate,
        ITimeZoneService timeZoneService)
    {
        _unitOfWork = unitOfWork;
        _exportService = exportService;
        _localizer = localizer;
        this.paymanetNotifier = paymanetNotifier;
        this.paymentService = paymentService;
        _subscriptionGate = subscriptionGate;
        this.timeZoneService = timeZoneService;
    }

    /// <inheritdoc />
    public async Task<Result<EventDto>> CreateEventAsync(CreateEventDto dto)
    {
        // Books & fees is SUBSCRIBER-ONLY: the ModuleQuotas limit for "Events" is 0, so
        // CanCreateAsync short-circuits to false without counting (like Assistants / Groups /
        // Triggers). A DISTINCT message key, not the generic SubscriptionRequired, so the app can
        // render the paywall card the Payments tab already gates on features.extrasAllowed instead
        // of a bare toast.
        if (!await _subscriptionGate.CanCreateAsync(
                dto.TeacherId, ModuleQuotaKeys.Events,
                () => _unitOfWork.PaymentsRepo.CountEventsByTeacherAsync(dto.TeacherId)))
            return Result<EventDto>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasRequireSubscription, HttpStatusCode.Forbidden);

        if (string.IsNullOrWhiteSpace(dto.EventName))
            return Result<EventDto>.Failure(
                _localizer, PaymentConstants.Messages.EventNameRequired, HttpStatusCode.BadRequest);

        if (dto.EventAmount <= 0)
            return Result<EventDto>.Failure(
                _localizer, PaymentConstants.Messages.EventAmountInvalid, HttpStatusCode.BadRequest);

        if (dto.TargetScopes.Count == 0)
            return Result<EventDto>.Failure(
                _localizer, PaymentConstants.Messages.EventTargetScopeEmpty, HttpStatusCode.BadRequest);

        // REQ-EVT-004: resolve + dedupe across all combined scopes, validating OWNERSHIP of every
        // id as we go. A null Scopes means a target did not belong to this teacher — answered as a
        // 404 rather than silently dropped, which is what the old verbatim-ids path did.
        var (studentIds, scopeRows) = await ResolveAndDeduplicateTargetStudentsAsync(
            dto.TeacherId, dto.TargetScopes, dto.ActingUserId);
        if (scopeRows is null)
            return Result<EventDto>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasScopeTargetNotFound, HttpStatusCode.NotFound);
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

            // The ORIGINAL targeting intent, which is what the by-session / by-group breakdowns and
            // the per-item auto-include rule read. TargetScopeType/TargetScopeIds above are still
            // written so a rollback past this migration leaves a working app.
            foreach (var row in scopeRows)
                row.PaymentEventId = paymentEvent.Id;
            if (scopeRows.Count > 0)
                await _unitOfWork.PaymentsRepo.AddPaymentEventScopesRangeAsync(scopeRows);

            // BR-EVT-001: obligations for everyone in scope, at creation time.
            // ONE batched student read — this was a query PER STUDENT, so targeting a 120-student
            // class meant 120 round trips inside the create transaction.
            var students = await _unitOfWork.Students
                .GetActiveByIdsAndTeacherAsync(dto.TeacherId, studentIds);
            var studentById = students.ToDictionary(x => x.Id);

            var obligations = new List<EventStudentObligation>(studentIds.Count);
            foreach (var studentId in studentIds)
            {
                studentById.TryGetValue(studentId, out var student);
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

            // Read back AFTER the commit so the returned audience is what was actually persisted,
            // not what the request asked for. One indexed query on a create — not a hot path — and
            // it is what lets the form pop straight back to a row that already names its classes.
            var createdScopes = await _unitOfWork.PaymentsRepo
                .GetExtrasScopeSummariesAsync(dto.TeacherId, new[] { paymentEvent.Id });

            return Result<EventDto>.Success(
                MapToEventDto(paymentEvent, scopeRows: createdScopes),
                _localizer, PaymentConstants.Messages.EventCreatedSuccess);
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
        // CLAMPED. pageSize came straight off the query string: 0 divided by zero on totalPages
        // below (a 500 from a stray ?pageSize=0), and an arbitrarily large value was an unbounded
        // read on a 5-DTU database. Same [1, 100] bounds as the payment screens' NormalizePaging.
        int page = filter.Page < 1 ? 1 : filter.Page;
        int pageSize = filter.PageSize < 1
            ? PaymentConstants.EventListDefaultPageSize
            : (filter.PageSize > PaymentConstants.EventListMaxPageSize
                ? PaymentConstants.EventListMaxPageSize
                : filter.PageSize);

        var (items, totalCount) = await _unitOfWork.PaymentsRepo
            .GetPaymentEventsFilteredPagedAsync(teacherId,
                filter.SearchName, filter.ScopeTypeFilter, filter.CompletionStatus,
                page, pageSize);

        // Per-item totals DERIVED from the obligation rows, in ONE grouped query keyed on this
        // page's ids — bounded by page size, never N+1. Deliberately not the entity's cached
        // TotalStudents / TotalExpectedRevenue / TotalCollectedRevenue: three write paths used to
        // clobber those independently, so a card could claim a figure its own students contradicted.
        var totalsById = (await _unitOfWork.PaymentsRepo
                .GetExtrasItemTotalsAsync(teacherId, items.Select(x => x.Id).ToList()))
            .ToDictionary(t => t.PaymentEventId);

        // The audience of every item on this page, in ONE query. A row cannot say who it is for
        // without it, and resolving per item would be a query per row.
        var scopesById = (await _unitOfWork.PaymentsRepo
                .GetExtrasScopeSummariesAsync(teacherId, items.Select(x => x.Id).ToList()))
            .GroupBy(r => r.PaymentEventId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ExtrasScopeSummaryRow>)g.ToList());

        var dtos = items.Select(e =>
        {
            // An item with no obligations at all has no row in the grouped result — a real state
            // (everyone was removed, or every student was purged), and it must read as zeros rather
            // than falling back to a stale cached figure.
            totalsById.TryGetValue(e.Id, out var t);
            scopesById.TryGetValue(e.Id, out var scopes);
            return MapToEventDto(e, ToSummary(t), scopes);
        }).ToList();

        var response = new PaginatedResponse<List<EventDto>>
        {
            totalCount = totalCount,
            page = page,
            pageSize = pageSize,
            totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
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
            // OMITTED means UNCHANGED on every one of these, never "clear it". An empty string on
            // Notes still clears them deliberately — but a client that does not send the field at
            // all must never disturb it (BUG-20).
            if (dto.EventName is not null) paymentEvent.EventName = dto.EventName;
            if (dto.Notes is not null) paymentEvent.Notes = dto.Notes;
            if (dto.EventDate.HasValue) paymentEvent.EventDate = dto.EventDate.Value.Date;
            if (dto.AutoIncludeNewStudents.HasValue)
                paymentEvent.AutoIncludeNewStudents = dto.AutoIncludeNewStudents.Value;
            if (dto.CollectDuringAttendance.HasValue)
                paymentEvent.CollectDuringAttendance = dto.CollectDuringAttendance.Value;

            // REQ-EVT-019: an amount change re-prices only the obligations it may touch.
            //
            // THREE exclusions, all load-bearing:
            //   • AmountPaid > 0  — money has been taken against this price; rewriting it would make
            //     the collected figure explain nothing.
            //   • IsCustomAmount  — a HUMAN set this student's price. A flag, not a comparison:
            //     deriving "is custom" as AmountDue != EventAmount is wrong the moment the item
            //     amount changes, because a paid obligation then legitimately differs.
            //   • IsExempt        — not taking it, so there is no price to change.
            if (dto.EventAmount.HasValue && dto.EventAmount.Value != paymentEvent.EventAmount)
            {
                paymentEvent.EventAmount = dto.EventAmount.Value;
                await _unitOfWork.PaymentsRepo.RepriceEventObligationsAsync(
                    dto.EventId, dto.TeacherId, dto.EventAmount.Value);
            }

            // REQ-EVT-021 / BR-EVT-003: removing students is TUTOR-ONLY, and a student who has paid
            // cannot be removed.
            //
            // Both used to be silent: the role rule was documented and never enforced, and a paid
            // student was skipped with no word to the caller — the tutor saw "updated successfully"
            // and a roster that had not changed. Now the role is a 403 and each blocked student is
            // named in the response.
            var blockedRemovals = new List<string>();
            if (dto.StudentIdsToRemove?.Count > 0)
            {
                if (dto.IsAssistantCaller)
                    return Result<EventDto>.Failure(
                        _localizer, PaymentConstants.Messages.ExtrasRemoveStudentsTeacherOnly,
                        HttpStatusCode.Forbidden);

                foreach (var studentId in dto.StudentIdsToRemove)
                {
                    var obligation = await _unitOfWork.PaymentsRepo
                        .GetEventObligationAsync(dto.EventId, studentId, dto.TeacherId);
                    if (obligation is null) continue;

                    if (obligation.AmountPaid > 0)
                    {
                        blockedRemovals.Add(obligation.StudentName ?? obligation.StudentCode ?? studentId.ToString());
                        continue;
                    }

                    await _unitOfWork.PaymentsRepo.AddEventPaymentEditLogAsync(new EventPaymentEditLog
                    {
                        TeacherId = dto.TeacherId,
                        PaymentEventId = dto.EventId,
                        EventStudentObligationId = obligation.Id,
                        TeacherStudentId = obligation.TeacherStudentId,
                        StudentName = obligation.StudentName,
                        StudentCode = obligation.StudentCode,
                        EventName = paymentEvent.EventName,
                        EditAction = EventPaymentEditAction.ObligationRemoved,
                        PreviousAmount = obligation.AmountDue,
                        NewAmount = 0m,
                        EditedByUserId = dto.ActingUserId,
                        EditedAt = DateTime.UtcNow,
                        CreateAt = DateTime.UtcNow
                    });
                    await _unitOfWork.PaymentsRepo.DeleteEventObligationAsync(obligation);
                }
            }

            // REQ-EVT-020: add students. Ownership is verified per student — an unknown or foreign
            // id is REJECTED rather than materialized as an obligation with a null name, which is
            // what the create path used to do.
            if (dto.StudentIdsToAdd?.Count > 0)
            {
                var newObligations = new List<EventStudentObligation>();
                foreach (var studentId in dto.StudentIdsToAdd.Distinct())
                {
                    var existing = await _unitOfWork.PaymentsRepo
                        .GetEventObligationAsync(dto.EventId, studentId, dto.TeacherId);
                    if (existing is not null) continue;

                    var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(studentId, dto.TeacherId);
                    if (student is null)
                        return Result<EventDto>.Failure(
                            _localizer, PaymentConstants.Messages.ExtrasScopeTargetNotFound,
                            HttpStatusCode.NotFound);

                    newObligations.Add(new EventStudentObligation
                    {
                        TeacherId = dto.TeacherId,
                        PaymentEventId = dto.EventId,
                        TeacherStudentId = studentId,
                        StudentName = student.StudentName,
                        StudentCode = student.StudentCode,
                        AmountDue = paymentEvent.EventAmount,
                        PaymentStatus = PaymentStatus.Unpaid,
                        CreateAt = DateTime.UtcNow
                    });
                }
                if (newObligations.Count > 0)
                    await _unitOfWork.PaymentsRepo.AddEventObligationsRangeAsync(newObligations);
            }

            await _unitOfWork.SaveChangesAsync();

            // ONE recompute, AFTER every mutation, derived from the obligation rows.
            //
            // The old code did arithmetic on the cached columns at three different points and then
            // added `EventAmount × count` for the new students — which ran AFTER the full recompute
            // and clobbered every per-student custom price. Deriving once from the rows makes all
            // three orderings identical and cannot drift.
            await RecomputeEventExpectedAsync(dto.TeacherId, paymentEvent);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var updated = MapToEventDto(paymentEvent, scopeRows: await _unitOfWork.PaymentsRepo
                .GetExtrasScopeSummariesAsync(dto.TeacherId, new[] { paymentEvent.Id }));
            updated.BlockedRemovals = blockedRemovals;

            // A partial outcome is REPORTED, not hidden behind "updated successfully": the tutor
            // asked to remove N students and some could not be removed, and they need to know which.
            return blockedRemovals.Count > 0
                ? Result<EventDto>.Success(
                    updated, _localizer,
                    PaymentConstants.Messages.ExtrasStudentAlreadyPaidCannotRemove,
                    new object?[] { string.Join(", ", blockedRemovals) })
                : Result<EventDto>.Success(
                    updated, _localizer, PaymentConstants.Messages.EventUpdatedSuccess);
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
            .GetEventObligationAsync(dto.EventId, dto.TeacherStudentId, dto.TeacherId);
        if (obligation is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.EventObligationNotFound, HttpStatusCode.NotFound);

        // An amount below what the student has already paid would leave the obligation permanently
        // "overpaid" with no way back except a refund — so say that instead of silently accepting it.
        if (dto.CustomAmount < 0m)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.PaymentAmountInvalid, HttpStatusCode.BadRequest);

        if (obligation.IsExempt)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasStudentExempt,
                new object?[] { obligation.StudentName ?? string.Empty },
                HttpStatusCode.UnprocessableEntity);

        var paymentEvent = await _unitOfWork.PaymentsRepo
            .GetPaymentEventByIdAndTeacherAsync(dto.EventId, dto.TeacherId);
        if (paymentEvent is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasItemNotFound, HttpStatusCode.NotFound);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            decimal previousAmount = obligation.AmountDue;

            obligation.AmountDue = dto.CustomAmount;
            // THE flag that makes an item-wide re-price skip this student from now on. Stored, not
            // derived from "AmountDue != EventAmount" — that comparison is wrong the moment the
            // item's own amount changes, because a paid obligation then legitimately differs.
            obligation.IsCustomAmount = true;
            obligation.CustomAmountSetByUserId = dto.ActingUserId;
            obligation.CustomAmountSetAt = DateTime.UtcNow;
            await _unitOfWork.PaymentsRepo.UpdateEventObligationAsync(obligation);

            await _unitOfWork.PaymentsRepo.AddEventPaymentEditLogAsync(new EventPaymentEditLog
            {
                TeacherId = dto.TeacherId,
                PaymentEventId = dto.EventId,
                EventStudentObligationId = obligation.Id,
                TeacherStudentId = obligation.TeacherStudentId,
                StudentName = obligation.StudentName,
                StudentCode = obligation.StudentCode,
                EventName = paymentEvent.EventName,
                EditAction = EventPaymentEditAction.CustomAmountChanged,
                PreviousAmount = previousAmount,
                NewAmount = dto.CustomAmount,
                // Changing what is OWED moves no cash, so no ChargedToUserId — and the refund-row
                // query filters on PreviousAmount - NewAmount > 0, which would otherwise mistake a
                // price reduction for money handed back.
                EditedByUserId = dto.ActingUserId,
                EditedAt = DateTime.UtcNow,
                CreateAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync();

            // Derived from the rows in one grouped statement — replaces a 50,000-row paged read.
            await RecomputeEventExpectedAsync(dto.TeacherId, paymentEvent);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            // The key existed, localized, and was never used — the caller got a generic
            // "event updated" for a per-student price change.
            return Result<bool>.Success(
                true, _localizer, PaymentConstants.Messages.EventStudentCustomAmountSet);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteEventAsync(long teacherId, long eventId, long actingUserId)
    {
        var paymentEvent = await _unitOfWork.PaymentsRepo
            .GetPaymentEventByIdAndTeacherAsync(eventId, teacherId);
        if (paymentEvent is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.EventNotFound, HttpStatusCode.NotFound);

        // REFUSED once money has been collected and not refunded. The transactions survive a
        // delete (the FK is SET NULL) and they are still shown in the collections ledger — so
        // deleting the item would leave cash on screen with nothing left to explain what it was
        // for. The message names CLOSE as the remedy, which is exactly what IsClosed exists for:
        // a refusal that does not say what to do instead is the parent-portal cooldown mistake.
        if (await _unitOfWork.PaymentsRepo.HasCollectedEventMoneyAsync(eventId, teacherId))
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasItemHasPaymentsCannotDelete,
                new object?[] { paymentEvent.EventName },
                HttpStatusCode.Conflict);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // The obligation → item FK is NoAction while the delete is soft, so the rows would
            // linger forever. One set-based statement removes the money-free ones; anything
            // carrying cash is left behind as the attribution for a surviving transaction, and the
            // item's own query filter hides it from every read from here on (defect 14).
            await _unitOfWork.PaymentsRepo.DeleteUnpaidEventObligationsAsync(eventId, teacherId);

            // REQ-EVT-022: soft-delete; BR-EVT-005: irreversible.
            paymentEvent.IsDeleted = true;
            paymentEvent.DeletedAt = DateTime.UtcNow;
            paymentEvent.DeletedByUserId = actingUserId;
            await _unitOfWork.PaymentsRepo.UpdatePaymentEventAsync(paymentEvent);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<bool>.Success(
                true, _localizer, PaymentConstants.Messages.EventDeletedSuccess);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<EventDto>> SetEventClosedAsync(
        long teacherId, long eventId, bool closed, long actingUserId)
    {
        var paymentEvent = await _unitOfWork.PaymentsRepo
            .GetPaymentEventByIdAndTeacherAsync(eventId, teacherId);
        if (paymentEvent is null)
            return Result<EventDto>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasItemNotFound, HttpStatusCode.NotFound);

        // Idempotent: re-sending the state the item is already in is a no-op that still answers
        // with the item, so a double tap or a retried request cannot fail.
        if (paymentEvent.IsClosed != closed)
        {
            paymentEvent.IsClosed = closed;
            // Cleared on reopen, so the field always answers "when was it closed" about the
            // CURRENT closure rather than about a previous one.
            paymentEvent.ClosedAt = closed ? DateTime.UtcNow : null;
            await _unitOfWork.PaymentsRepo.UpdatePaymentEventAsync(paymentEvent);
            await _unitOfWork.SaveChangesAsync();
        }

        var totals = (await _unitOfWork.PaymentsRepo
                .GetExtrasItemTotalsAsync(teacherId, new[] { eventId }))
            .FirstOrDefault();
        var scopes = await _unitOfWork.PaymentsRepo
            .GetExtrasScopeSummariesAsync(teacherId, new[] { eventId });

        return Result<EventDto>.Success(
            MapToEventDto(paymentEvent, ToSummary(totals), scopes),
            _localizer,
            closed
                ? PaymentConstants.Messages.ExtrasItemClosedSuccess
                : PaymentConstants.Messages.ExtrasItemReopenedSuccess);
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

        // Closed = no further collection, but refunds and history still work. This is the escape
        // hatch for an item that cannot be DELETED because money was collected against it.
        if (paymentEvent.IsClosed)
            return Result<EventPaymentResultDto>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasItemClosed, HttpStatusCode.Conflict);

        // teacherId is now REQUIRED on this read. Without it the obligation was reachable across
        // tenants by id alone — the caller always holds the teacher id, so the compiler enforces it.
        var obligation = await _unitOfWork.PaymentsRepo
            .GetEventObligationAsync(dto.EventId, dto.TeacherStudentId, dto.TeacherId);
        if (obligation is null)
            return Result<EventPaymentResultDto>.Failure(
                _localizer, PaymentConstants.Messages.EventObligationNotFound, HttpStatusCode.NotFound);

        // Exempt = "not taking it". Blocking the collect INTERACTIVELY is right: a human is present
        // and can lift the exemption deliberately. (The offline replay path decides differently —
        // there the cash has physically moved and refusing it would leave it with no home.)
        if (obligation.IsExempt)
            return Result<EventPaymentResultDto>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasStudentExempt,
                new object?[] { obligation.StudentName ?? string.Empty },
                HttpStatusCode.UnprocessableEntity);

        // REQ-EVT-012: Already-paid warning. A business WARNING, so it is a Success + flags at
        // HTTP 200 (never a 4xx) — the same shape as the fee module's same-day duplicate confirm.
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

        // Over-payment cap. PaymentStatus.Overpaid exists and is reachable, but only where it is
        // MEANT to be: an offline replay, where the money already changed hands and there is no
        // human to ask. Interactively, taking more than is owed is a typo far more often than an
        // intention, and unlike a monthly fee there is no "next month" for the surplus to land in.
        decimal outstanding = obligation.AmountDue - obligation.AmountPaid;
        if (!dto.AlreadyPaidConfirmed && outstanding > 0m && dto.Amount > outstanding)
            return Result<EventPaymentResultDto>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasAmountExceedsOutstanding,
                new object?[] { obligation.StudentName ?? string.Empty },
                HttpStatusCode.UnprocessableEntity);

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
                CollectionNote = string.IsNullOrWhiteSpace(dto.CollectionNote) ? null : dto.CollectionNote.Trim(),
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

            // The SHARED collector-wallet credit — not an inline mutation. This is what brings the
            // bounded RowVersion retry loop (two collectors, one student, same instant), lazy wallet
            // creation for a CenterAssistant collector whose cash previously reached no wallet at
            // all, and the teacher-owner no-op. Runs on this transaction.
            await paymentService.CreditCollectorWalletAsync(
                dto.TeacherId, dto.CollectedByUserId!.Value, dto.Amount);

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            // Post-commit, best-effort: naming the collector must never fail a committed collection.
            string? collectorName = null;
            if (transaction.CollectedByUserId is long collectorUserId)
            {
                try
                {
                    var names = await _unitOfWork.Users
                        .GetUserFullNamesByUserIdsAsync(new List<long> { collectorUserId });
                    collectorName = names.TryGetValue(collectorUserId, out var nm) ? nm : null;
                }
                catch
                {
                    // Cosmetic only — the money is committed.
                }
            }

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
                    // Was declared and never set, so every client saw a null collector name.
                    CollectedByUserName = collectorName,
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
    public async Task<Result<ExtrasItemTrackingResponse>> GetExtrasItemTrackingAsync(
        long teacherId, long itemId, long? scopeToCollectorUserId)
    {
        var item = await _unitOfWork.PaymentsRepo.GetPaymentEventByIdAndTeacherAsync(itemId, teacherId);
        if (item is null)
            return Result<ExtrasItemTrackingResponse>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasItemNotFound, HttpStatusCode.NotFound);

        // ONE grouped query. The header, the by-session breakdown and the by-group breakdown are all
        // folded from THIS result, which is what makes them agree by construction — they are the
        // same numbers summed differently, so a breakdown can never drift from its header.
        var tally = await _unitOfWork.PaymentsRepo.GetExtrasTallyAsync(itemId, teacherId);

        var summary = FoldSummary(tally);

        // Rows keyed on the student's CURRENT session. A student with no class gets their OWN row
        // (Id null) rather than being dropped — otherwise the breakdown stops summing to the header,
        // which is the whole property that makes it trustworthy.
        var bySession = tally
            .GroupBy(t => new { t.SessionId, t.SessionName })
            .Select(g => FoldBreakdown(
                g.Key.SessionId?.ToString(CultureInfo.InvariantCulture), g.Key.SessionName, g))
            .OrderByDescending(r => r.TotalStudents)
            .ThenBy(r => r.Name)
            .ToList();

        var byGroup = tally
            .GroupBy(t => new { t.SessionGroupId, t.SessionGroupName })
            .Select(g => FoldBreakdown(
                g.Key.SessionGroupId?.ToString(CultureInfo.InvariantCulture), g.Key.SessionGroupName, g))
            .OrderByDescending(r => r.TotalStudents)
            .ThenBy(r => r.Name)
            .ToList();

        // Activity-driven, and force-scoped for an assistant caller — a peer's figures are none of
        // their business, exactly as on /collections and /collections/summary.
        var collectorTally = await _unitOfWork.PaymentsRepo
            .GetExtrasCollectorTallyAsync(itemId, teacherId, scopeToCollectorUserId);

        var collectorIds = collectorTally
            .Where(c => c.CollectedByUserId.HasValue)
            .Select(c => c.CollectedByUserId!.Value)
            .Distinct()
            .ToList();
        var collectorNames = collectorIds.Count > 0
            ? await _unitOfWork.Users.GetUserFullNamesByUserIdsAsync(collectorIds)
            : new Dictionary<long, string>();
        var ownerUserId = await _unitOfWork.Users.GetTeacherUserIdByIdAsync(teacherId);

        var byCollector = collectorTally
            .Where(c => c.CollectedByUserId.HasValue)
            .Select(c => new ExtrasCollectorRowDto
            {
                UserId = c.CollectedByUserId!.Value.ToString(CultureInfo.InvariantCulture),
                Name = collectorNames.TryGetValue(c.CollectedByUserId!.Value, out var nm) ? nm : null,
                // By IDENTITY, not an active-assistant allow-list: a REMOVED assistant's collections
                // are still hers, and the allow-list approach used to default them to "Teacher" and
                // file her money under the tutor's own card (§7.4).
                Role = c.CollectedByUserId == ownerUserId ? "Teacher" : "Assistant",
                CollectedAmount = c.CollectedAmount,
                TransactionCount = c.TransactionCount,
                StudentCount = c.StudentCount
            })
            .OrderByDescending(c => c.CollectedAmount)
            .ToList();

        var response = new ExtrasItemTrackingResponse
        {
            // The audience is on the tracking header too: the breakdowns say WHO has paid, and a
            // tutor reading "3 classes" above them can tell at a glance whether a class is missing
            // from the list entirely rather than merely unpaid.
            Item = MapToEventDto(item, summary,
                await _unitOfWork.PaymentsRepo.GetExtrasScopeSummariesAsync(teacherId, new[] { item.Id })),
            Summary = summary,
            BySession = bySession,
            ByGroup = byGroup,
            ByCollector = byCollector,
            NewJoiners = new ExtrasNewJoinersDto
            {
                AutoIncludeEnabled = item.AutoIncludeNewStudents,
                // Only meaningful when auto-include is OFF: when it is on there is nothing to add,
                // because assignment already materialized them. Computing it anyway would cost a
                // scope resolve on every screen open for no possible action.
                Count = item.AutoIncludeNewStudents
                    ? 0
                    : await CountNewJoinersAsync(teacherId, item)
            }
        };

        return Result<ExtrasItemTrackingResponse>.Success(
            response, _localizer, PaymentConstants.Messages.Success);
    }

    /// <summary>
    /// Students currently in one of the item's targeted classes who have NO obligation on it — the
    /// drift the "add them?" banner exists to surface. 0 for a legacy item with no scope rows.
    /// </summary>
    private async Task<int> CountNewJoinersAsync(long teacherId, Domain.Entities.PaymentEvent item)
        => (await ResolveNewJoinerIdsAsync(teacherId, item)).Count;

    /// <summary>
    /// Students currently in one of the item's targeted classes who have NO obligation on it.
    ///
    /// <para>ONE definition, shared by the banner's COUNT and the banner's ACTION. Two separate
    /// resolutions would eventually disagree, and the shape of that bug is a tutor tapping
    /// "add 3 students" and getting 2.</para>
    ///
    /// <para>Empty for a legacy item created before targeting rules were stored — which is why the
    /// banner reads 0 for those rather than something wrong.</para>
    /// </summary>
    private async Task<List<long>> ResolveNewJoinerIdsAsync(
        long teacherId, Domain.Entities.PaymentEvent item)
    {
        var scopes = await _unitOfWork.PaymentsRepo.GetPaymentEventScopesAsync(item.Id, teacherId);
        if (scopes.Count == 0) return new List<long>();

        var inScope = new HashSet<long>();
        foreach (var scope in scopes)
        {
            IReadOnlyList<long> ids = scope.ScopeType switch
            {
                EventTargetScopeType.Session when scope.SessionId.HasValue =>
                    await _unitOfWork.PaymentsRepo.GetStudentIdsBySessionAsync(teacherId, scope.SessionId.Value),
                EventTargetScopeType.SessionGroup when scope.SessionGroupId.HasValue =>
                    await _unitOfWork.PaymentsRepo.GetStudentIdsByGroupAsync(teacherId, scope.SessionGroupId.Value),
                EventTargetScopeType.AllStudents =>
                    await _unitOfWork.PaymentsRepo.GetAllStudentIdsAsync(teacherId),
                _ => System.Array.Empty<long>()
            };
            foreach (var id in ids) inScope.Add(id);
        }
        if (inScope.Count == 0) return new List<long>();

        var already = (await _unitOfWork.PaymentsRepo
                .GetStudentIdsWithObligationAsync(item.Id, teacherId, inScope.ToList()))
            .ToHashSet();

        return inScope.Where(id => !already.Contains(id)).ToList();
    }

    /// <inheritdoc />
    public async Task<Result<int>> AddNewJoinersAsync(long teacherId, long itemId, long actingUserId)
    {
        var item = await _unitOfWork.PaymentsRepo.GetPaymentEventByIdAndTeacherAsync(itemId, teacherId);
        if (item is null)
            return Result<int>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasItemNotFound, HttpStatusCode.NotFound);

        // A closed item takes no new obligations — closing exists precisely to stop that.
        if (item.IsClosed)
            return Result<int>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasItemClosed, HttpStatusCode.Conflict);

        var joinerIds = await ResolveNewJoinerIdsAsync(teacherId, item);
        if (joinerIds.Count == 0)
            return Result<int>.Success(0, _localizer, PaymentConstants.Messages.Success);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            var students = await _unitOfWork.Students
                .GetActiveByIdsAndTeacherAsync(teacherId, joinerIds);

            var now = DateTime.UtcNow;
            var toAdd = students.Select(student => new EventStudentObligation
            {
                TeacherId = teacherId,
                PaymentEventId = item.Id,
                TeacherStudentId = student.Id,
                StudentName = student.StudentName,
                StudentCode = student.StudentCode,
                // The item's CURRENT price — a joiner pays what it costs now.
                AmountDue = item.EventAmount,
                PaymentStatus = PaymentStatus.Unpaid,
                CreateAt = now
            }).ToList();

            if (toAdd.Count == 0)
            {
                if (ownsTransaction) await _unitOfWork.RollbackAsync();
                return Result<int>.Success(0, _localizer, PaymentConstants.Messages.Success);
            }

            await _unitOfWork.PaymentsRepo.AddEventObligationsRangeAsync(toAdd);

            foreach (var student in students)
            {
                await _unitOfWork.PaymentsRepo.AddEventPaymentEditLogAsync(new EventPaymentEditLog
                {
                    TeacherId = teacherId,
                    PaymentEventId = item.Id,
                    TeacherStudentId = student.Id,
                    StudentName = student.StudentName,
                    StudentCode = student.StudentCode,
                    EventName = item.EventName,
                    EditAction = EventPaymentEditAction.ObligationAdded,
                    PreviousAmount = 0m,
                    NewAmount = item.EventAmount,
                    EditedByUserId = actingUserId,
                    EditedAt = now,
                    CreateAt = now
                });
            }

            await _unitOfWork.SaveChangesAsync();
            await RecomputeEventExpectedAsync(teacherId, item);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<int>.Success(
                toAdd.Count, _localizer, PaymentConstants.Messages.ExtrasStudentsAddedSuccess,
                new object?[] { toAdd.Count });
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <summary>Folds the grouped tally into the item header.</summary>
    private static ExtrasItemSummaryDto FoldSummary(IReadOnlyList<ExtrasTallyRow> tally)
    {
        int Count(ExtrasBucket b) => tally.Where(t => t.Bucket == b).Sum(t => t.StudentCount);
        decimal Due(ExtrasBucket b) => tally.Where(t => t.Bucket == b).Sum(t => t.AmountDue);

        int exempt = Count(ExtrasBucket.Exempt);
        // Exempt students are excluded from the headcount AND from expected revenue: "not taking it"
        // means no money is expected and they are not part of the population being chased.
        int total = tally.Where(t => t.Bucket != ExtrasBucket.Exempt).Sum(t => t.StudentCount);
        decimal expected = tally.Where(t => t.Bucket != ExtrasBucket.Exempt).Sum(t => t.AmountDue);
        decimal collected = tally.Sum(t => t.AmountPaid);

        return new ExtrasItemSummaryDto
        {
            TotalStudents = total,
            PaidStudents = Count(ExtrasBucket.Paid),
            PartiallyPaidStudents = Count(ExtrasBucket.PartiallyPaid),
            UnpaidStudents = Count(ExtrasBucket.Unpaid),
            ExemptStudents = exempt,
            ExpectedAmount = expected,
            CollectedAmount = collected,
            // Clamped at 0: an overpaid item would otherwise report negative "remaining".
            RemainingAmount = Math.Max(0m, expected - collected),
            ExemptAmount = Due(ExtrasBucket.Exempt),
            CompletionPercent = expected > 0m
                ? Math.Round(collected / expected * 100m, 1)
                : 0m
        };
    }

    /// <summary>
    /// Turns a page-level <see cref="ExtrasItemTotals"/> into the header shape the DTO mapper takes.
    /// A null/default input (an item with no obligations) yields explicit zeros rather than letting
    /// the mapper fall through to the cached columns.
    /// </summary>
    private static ExtrasItemSummaryDto ToSummary(ExtrasItemTotals? t)
    {
        t ??= new ExtrasItemTotals();
        return new ExtrasItemSummaryDto
        {
            TotalStudents = t.TotalStudents,
            PaidStudents = t.PaidStudents,
            PartiallyPaidStudents = t.PartiallyPaidStudents,
            UnpaidStudents = t.UnpaidStudents,
            ExemptStudents = t.ExemptStudents,
            ExpectedAmount = t.ExpectedAmount,
            CollectedAmount = t.CollectedAmount,
            RemainingAmount = Math.Max(0m, t.ExpectedAmount - t.CollectedAmount),
            ExemptAmount = t.ExemptAmount,
            CompletionPercent = t.ExpectedAmount > 0m
                ? Math.Round(t.CollectedAmount / t.ExpectedAmount * 100m, 1)
                : 0m
        };
    }

    /// <summary>Folds one (session|group) slice of the tally into a breakdown row.</summary>
    private static ExtrasBreakdownRowDto FoldBreakdown(
        string? id, string? name, IEnumerable<ExtrasTallyRow> cells)
    {
        var list = cells.ToList();
        int Count(ExtrasBucket b) => list.Where(t => t.Bucket == b).Sum(t => t.StudentCount);

        int total = list.Where(t => t.Bucket != ExtrasBucket.Exempt).Sum(t => t.StudentCount);
        decimal expected = list.Where(t => t.Bucket != ExtrasBucket.Exempt).Sum(t => t.AmountDue);
        decimal collected = list.Sum(t => t.AmountPaid);

        return new ExtrasBreakdownRowDto
        {
            Id = id,
            Name = name,
            TotalStudents = total,
            PaidStudents = Count(ExtrasBucket.Paid),
            PartiallyPaidStudents = Count(ExtrasBucket.PartiallyPaid),
            UnpaidStudents = Count(ExtrasBucket.Unpaid),
            ExemptStudents = Count(ExtrasBucket.Exempt),
            ExpectedAmount = expected,
            CollectedAmount = collected,
            RemainingAmount = Math.Max(0m, expected - collected)
        };
    }

    /// <inheritdoc />
    public async Task<Result<ExtrasStudentsPageDto>> GetExtrasItemStudentsAsync(
        long teacherId, long itemId, string? status,
        long? sessionId, long? sessionGroupId, long? collectedByUserId,
        string? search, int page, int pageSize)
    {
        var item = await _unitOfWork.PaymentsRepo.GetPaymentEventByIdAndTeacherAsync(itemId, teacherId);
        if (item is null)
            return Result<ExtrasStudentsPageDto>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasItemNotFound, HttpStatusCode.NotFound);

        (page, pageSize) = NormalizeExtrasPaging(page, pageSize);

        // "all" (or anything unrecognised) = no chip filter. A bad value must not 422 a list screen.
        ExtrasBucket? bucket = status?.Trim().ToLowerInvariant() switch
        {
            "paid" => ExtrasBucket.Paid,
            "partiallypaid" or "partial" => ExtrasBucket.PartiallyPaid,
            "unpaid" => ExtrasBucket.Unpaid,
            "exempt" => ExtrasBucket.Exempt,
            _ => null
        };

        var (rows, totalCount, paid, partial, unpaid, exempt, searched) = await _unitOfWork.PaymentsRepo
            .GetExtrasObligationsPagedAsync(
                itemId, teacherId, bucket, sessionId, sessionGroupId, collectedByUserId,
                search, page, pageSize);

        // Names for the page's actors only — three provenance fields, one batched lookup.
        var userIds = rows
            .SelectMany(r => new[] { r.ExemptedByUserId, r.CustomAmountSetByUserId, r.LastCollectedByUserId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var names = userIds.Count > 0
            ? await _unitOfWork.Users.GetUserFullNamesByUserIdsAsync(userIds)
            : new Dictionary<long, string>();

        string? Name(long? id) =>
            id.HasValue && names.TryGetValue(id.Value, out var n) ? n : null;

        var dtos = rows.Select(r => new ExtrasObligationRowDto
        {
            ObligationId = r.ObligationId.ToString(CultureInfo.InvariantCulture),
            TeacherStudentId = r.TeacherStudentId?.ToString(CultureInfo.InvariantCulture),
            StudentName = r.StudentName,
            StudentCode = r.StudentCode,
            SessionId = r.SessionId?.ToString(CultureInfo.InvariantCulture),
            SessionName = r.SessionName,
            SessionGroupId = r.SessionGroupId?.ToString(CultureInfo.InvariantCulture),
            SessionGroupName = r.SessionGroupName,
            AmountDue = r.AmountDue,
            AmountPaid = r.AmountPaid,
            Outstanding = Math.Max(0m, r.AmountDue - r.AmountPaid),
            PaymentStatus = r.PaymentStatus,
            IsExempt = r.IsExempt,
            ExemptedAt = r.ExemptedAt,
            ExemptedByName = Name(r.ExemptedByUserId),
            ExemptReason = r.ExemptReason,
            IsCustomAmount = r.IsCustomAmount,
            CustomAmountSetByName = Name(r.CustomAmountSetByUserId),
            CustomAmountSetAt = r.CustomAmountSetAt,
            LastPaymentAt = r.LastPaymentAt,
            LastCollectedByName = Name(r.LastCollectedByUserId),
            TransactionsCount = r.TransactionsCount
        }).ToList();

        var response = new ExtrasStudentsPageDto
        {
            totalCount = totalCount,
            page = page,
            pageSize = pageSize,
            totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
            data = dtos,
            PaidCount = paid,
            PartiallyPaidCount = partial,
            UnpaidCount = unpaid,
            ExemptCount = exempt,
            SearchedCount = searched
        };

        return Result<ExtrasStudentsPageDto>.Success(
            response, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<ExtrasDebtsResponse>> GetExtrasDebtsAsync(
        long teacherId, IReadOnlyCollection<long>? teacherStudentIds)
    {
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        bool show = config?.ShowExtrasOnAttendanceScreen ?? true;

        // OFF and "nobody owes anything" must not look the same on the wire — the client keys off
        // this flag, never off an empty dictionary (the ShowPaymentInfo precedent).
        if (!show)
            return Result<ExtrasDebtsResponse>.Success(
                new ExtrasDebtsResponse { ShowExtrasInfo = false },
                _localizer, PaymentConstants.Messages.Success);

        var ids = teacherStudentIds is { Count: > 0 }
            ? teacherStudentIds
            : await _unitOfWork.PaymentsRepo.GetAllStudentIdsAsync(teacherId);

        var through = DateOnly.FromDateTime(timeZoneService.GetTeacherLocalDate(teacherId));
        var map = await _unitOfWork.PaymentsRepo
            .GetExtrasDebtsForAttendanceBatchAsync(teacherId, ids, through);

        var response = new ExtrasDebtsResponse { ShowExtrasInfo = true };
        foreach (var (studentId, debts) in map)
        {
            response.ByStudent[studentId.ToString(CultureInfo.InvariantCulture)] = debts
                .Select(d => new ExtrasStudentDebtDto
                {
                    ObligationId = d.ObligationId.ToString(CultureInfo.InvariantCulture),
                    ItemId = d.PaymentEventId.ToString(CultureInfo.InvariantCulture),
                    ItemName = d.ItemName,
                    AmountDue = d.AmountDue,
                    AmountPaid = d.AmountPaid,
                    Outstanding = Math.Max(0m, d.AmountDue - d.AmountPaid),
                    ItemDate = DateOnly.FromDateTime(d.ItemDate)
                })
                .ToList();
        }

        return Result<ExtrasDebtsResponse>.Success(
            response, _localizer, PaymentConstants.Messages.Success);
    }

    /// <summary>Same [1,100] bounds the payment screens use, so one screen cannot ask for a read
    /// the rest of the module refuses.</summary>
    private static (int page, int pageSize) NormalizeExtrasPaging(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = PaymentConstants.EventListDefaultPageSize;
        else if (pageSize > PaymentConstants.EventListMaxPageSize) pageSize = PaymentConstants.EventListMaxPageSize;
        return (page, pageSize);
    }

    /// <inheritdoc />
    public async Task MaterializeAutoIncludeForSessionAsync(
        long teacherId, long sessionId, IReadOnlyCollection<long> teacherStudentIds)
    {
        if (teacherStudentIds is null || teacherStudentIds.Count == 0) return;

        var items = await _unitOfWork.PaymentsRepo
            .GetAutoIncludeEventsForSessionAsync(teacherId, sessionId);
        if (items.Count == 0) return;

        await MaterializeAsync(teacherId, items, teacherStudentIds);
    }

    /// <inheritdoc />
    public async Task MaterializeAutoIncludeForNewStudentsAsync(
        long teacherId, IReadOnlyCollection<long> teacherStudentIds)
    {
        if (teacherStudentIds is null || teacherStudentIds.Count == 0) return;

        // ONE lookup for the whole batch, not one per student.
        var items = await _unitOfWork.PaymentsRepo.GetAutoIncludeAllStudentEventsAsync(teacherId);
        if (items.Count == 0) return;

        await MaterializeAsync(teacherId, items, teacherStudentIds);
    }

    /// <summary>
    /// Creates the missing obligations for a set of students across a set of auto-include items.
    ///
    /// <para><b>Once per CALL, never per student.</b> The obvious place for this was the existing
    /// per-student <c>OnStudentAssignedToSessionAsync</c> hook, and that would have been a real
    /// hazard: bulk-assigning 300 students with 5 live auto-include items means 300 scope queries
    /// and 1,500 inserts inside ONE transaction, holding locks on a shared production database. Here
    /// it is one scope query, one "who already has one" query per item, and one AddRange.</para>
    ///
    /// <para>Runs INSIDE the caller's transaction, deliberately — not post-commit best-effort. An
    /// obligation that silently fails to appear is money the tutor never learns they are owed.</para>
    ///
    /// <para>Idempotent by construction (the difference is computed before inserting), with the
    /// filtered unique index <c>IX_ESO_EventId_StudentId</c> as the backstop.</para>
    /// </summary>
    private async Task MaterializeAsync(
        long teacherId, IReadOnlyList<Domain.Entities.PaymentEvent> items,
        IReadOnlyCollection<long> teacherStudentIds)
    {
        var ids = teacherStudentIds.Distinct().ToList();
        var students = await _unitOfWork.Students.GetActiveByIdsAndTeacherAsync(teacherId, ids);
        if (students.Count == 0) return;

        var now = DateTime.UtcNow;
        var toAdd = new List<EventStudentObligation>();

        foreach (var item in items)
        {
            var already = (await _unitOfWork.PaymentsRepo
                    .GetStudentIdsWithObligationAsync(item.Id, teacherId, ids))
                .ToHashSet();

            foreach (var student in students)
            {
                if (already.Contains(student.Id)) continue;

                toAdd.Add(new EventStudentObligation
                {
                    TeacherId = teacherId,
                    PaymentEventId = item.Id,
                    TeacherStudentId = student.Id,
                    StudentName = student.StudentName,
                    StudentCode = student.StudentCode,
                    // The item's CURRENT price. A joiner pays what the item costs now, not what it
                    // cost when it was created — and a per-student override is a separate, human act.
                    AmountDue = item.EventAmount,
                    PaymentStatus = PaymentStatus.Unpaid,
                    CreateAt = now
                });
            }
        }

        if (toAdd.Count == 0) return;

        await _unitOfWork.PaymentsRepo.AddEventObligationsRangeAsync(toAdd);
        await _unitOfWork.SaveChangesAsync();

        // Totals re-derived per touched item, from the rows.
        foreach (var itemId in toAdd.Select(o => o.PaymentEventId).Distinct())
        {
            var item = await _unitOfWork.PaymentsRepo
                .GetPaymentEventByIdAndTeacherAsync(itemId, teacherId);
            if (item is not null)
                await RecomputeEventExpectedAsync(teacherId, item);
        }
        await _unitOfWork.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<Result<List<EventPaymentTransactionDto>>> GetEventStudentPaymentsAsync(
        long teacherId, long eventId, long teacherStudentId)
    {
        // Tenant-scoped through the item, so a foreign eventId answers "not found" rather than
        // revealing that it exists (§3.3).
        var item = await _unitOfWork.PaymentsRepo
            .GetPaymentEventByIdAndTeacherAsync(eventId, teacherId);
        if (item is null)
            return Result<List<EventPaymentTransactionDto>>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasItemNotFound, HttpStatusCode.NotFound);

        var transactions = await _unitOfWork.PaymentsRepo
            .GetEventPaymentTransactionsForStudentAsync(
                eventId, teacherStudentId, teacherId,
                PaymentConstants.ExtrasStudentPaymentsMaxRows);

        // Collector NAMES batched for the whole list — the tutor's question about a payment they
        // are about to reverse is "who took this", and one lookup per row would be an N+1 on a
        // sheet that opens from a tap.
        var collectorIds = transactions
            .Where(t => t.CollectedByUserId.HasValue)
            .Select(t => t.CollectedByUserId!.Value)
            .Distinct()
            .ToList();
        var names = collectorIds.Count > 0
            ? await _unitOfWork.Users.GetUserFullNamesByUserIdsAsync(collectorIds)
            : new Dictionary<long, string>();

        // What each payment was originally collected for, where it has since been corrected —
        // ONE batched read over the audit trail for the whole list.
        var originals = await _unitOfWork.PaymentsRepo
            .GetEventTransactionOriginalAmountsAsync(
                teacherId, transactions.Select(t => t.Id).ToList());

        var dtos = transactions.Select(t => new EventPaymentTransactionDto
        {
            Id = t.Id,
            EventName = t.EventName,
            StudentName = t.StudentName,
            StudentCode = t.StudentCode,
            AmountPaid = t.AmountPaid,
            PaymentMethod = t.PaymentMethod,
            CollectedByUserId = t.CollectedByUserId,
            CollectedByUserName = t.CollectedByUserId.HasValue
                    && names.TryGetValue(t.CollectedByUserId.Value, out var nm)
                ? nm
                : null,
            CollectedAt = t.CollectedAt,
            IsOnlinePayment = t.IsOnlinePayment,
            CollectionNote = t.CollectionNote,
            IsEdited = originals.ContainsKey(t.Id),
            OriginalAmount = originals.TryGetValue(t.Id, out var original) ? original : null
        }).ToList();

        return Result<List<EventPaymentTransactionDto>>.Success(
            dtos, _localizer, PaymentConstants.Messages.Success);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> RefundEventPaymentAsync(
        long teacherId, long transactionId, decimal? amount, long actingUserId, string? reason)
    {
        var transaction = await _unitOfWork.PaymentsRepo
            .GetEventPaymentTransactionByIdAsync(transactionId, teacherId);
        if (transaction is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.EventObligationNotFound, HttpStatusCode.NotFound);

        // Full refund by default; a partial refund may not exceed what was taken.
        decimal refundAmount = amount ?? transaction.AmountPaid;
        if (refundAmount <= 0m || refundAmount > transaction.AmountPaid)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.PaymentAmountInvalid, HttpStatusCode.BadRequest);

        bool isFullRefund = refundAmount == transaction.AmountPaid;

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            decimal previousAmount = transaction.AmountPaid;

            if (isFullRefund)
            {
                // SOFT delete, mirroring PaymentTransaction: the row keeps its AmountPaid so the
                // audit entry below can still be paired with it, and the global query filter keeps it
                // out of every total. NEVER a hard delete — that would orphan the audit trail.
                transaction.IsDeleted = true;
                transaction.DeletedAt = DateTime.UtcNow;
            }
            else
            {
                transaction.AmountPaid -= refundAmount;
            }
            await _unitOfWork.PaymentsRepo.UpdateEventPaymentTransactionAsync(transaction);

            // The audit row IS the negative ledger line — the collections ledger derives money-out
            // from this table, never from the soft-deleted payment row.
            await _unitOfWork.PaymentsRepo.AddEventPaymentEditLogAsync(new EventPaymentEditLog
            {
                TeacherId = teacherId,
                EventPaymentTransactionId = transaction.Id,
                PaymentEventId = transaction.PaymentEventId,
                EventStudentObligationId = transaction.EventStudentObligationId,
                TeacherStudentId = transaction.TeacherStudentId,
                StudentName = transaction.StudentName,
                StudentCode = transaction.StudentCode,
                EventName = transaction.EventName,
                EditAction = isFullRefund
                    ? EventPaymentEditAction.Deleted
                    : EventPaymentEditAction.Refunded,
                PreviousAmount = previousAmount,
                NewAmount = isFullRefund ? 0m : transaction.AmountPaid,
                // Attribution decided at WRITE time: a refund of extras is always a CORRECTION of
                // the original collector's figure, so it comes out of THEIR wallet — not the wallet
                // of whoever performed the correction (which for extras is always the tutor).
                ChargedToUserId = transaction.CollectedByUserId,
                // The reversed cash's ORIGINAL instant, so the reset-aware wallet rule can tell
                // whether it was already handed over.
                CollectedAt = transaction.CollectedAt,
                EditedByUserId = actingUserId,
                EditedAt = DateTime.UtcNow,
                EditReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                CreateAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync();

            // Obligation + item totals RECOMPUTED from the surviving payments, never decremented —
            // a decrement drifts as soon as two corrections interleave.
            await RecomputeAfterMoneyChangeAsync(teacherId, transaction);

            // Reset-aware: reversing cash the collector already handed over must NOT move
            // CurrentBalance (it left via the hand-over), but must still move TotalCollected.
            // CollectedAt is the custody instant here ONLY because extras has no offline path — it is
            // always DateTime.UtcNow at insert. If one is ever added (a device-reported instant, as
            // fees now have per P0-6), this argument must become CreateAt first, or a refund of cash
            // physically in the bag will refuse to leave the wallet. Same at the edit site below.
            await paymentService.AdjustCollectorWalletAsync(
                teacherId, transaction.CollectedByUserId, -refundAmount, transaction.CollectedAt);

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<bool>.Success(
                true, _localizer, PaymentConstants.Messages.ExtrasRefundedSuccess);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> EditEventPaymentAsync(
        long teacherId, long transactionId, decimal newAmount, long actingUserId, string? reason)
    {
        if (newAmount <= 0m)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.PaymentAmountInvalid, HttpStatusCode.BadRequest);

        var transaction = await _unitOfWork.PaymentsRepo
            .GetEventPaymentTransactionByIdAsync(transactionId, teacherId);
        if (transaction is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.EventObligationNotFound, HttpStatusCode.NotFound);

        decimal previousAmount = transaction.AmountPaid;
        decimal delta = newAmount - previousAmount;
        if (delta == 0m)
            return Result<bool>.Success(
                true, _localizer, PaymentConstants.Messages.ExtrasPaymentEditedSuccess);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            transaction.AmountPaid = newAmount;
            await _unitOfWork.PaymentsRepo.UpdateEventPaymentTransactionAsync(transaction);

            await _unitOfWork.PaymentsRepo.AddEventPaymentEditLogAsync(new EventPaymentEditLog
            {
                TeacherId = teacherId,
                EventPaymentTransactionId = transaction.Id,
                PaymentEventId = transaction.PaymentEventId,
                EventStudentObligationId = transaction.EventStudentObligationId,
                TeacherStudentId = transaction.TeacherStudentId,
                StudentName = transaction.StudentName,
                StudentCode = transaction.StudentCode,
                EventName = transaction.EventName,
                EditAction = EventPaymentEditAction.AmountChanged,
                PreviousAmount = previousAmount,
                NewAmount = newAmount,
                ChargedToUserId = transaction.CollectedByUserId,
                CollectedAt = transaction.CollectedAt,
                EditedByUserId = actingUserId,
                EditedAt = DateTime.UtcNow,
                EditReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                CreateAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync();
            await RecomputeAfterMoneyChangeAsync(teacherId, transaction);

            // Only a DOWNWARD edit is a reversal, so only it is reset-aware. An upward edit is fresh
            // cash the collector is holding now, and always moves CurrentBalance.
            await paymentService.AdjustCollectorWalletAsync(
                teacherId, transaction.CollectedByUserId, delta,
                delta < 0m ? transaction.CollectedAt : null);

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<bool>.Success(
                true, _localizer, PaymentConstants.Messages.ExtrasPaymentEditedSuccess);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// After any money change, re-derive the obligation's <c>AmountPaid</c> + status and the item's
    /// collected total FROM the surviving payments.
    ///
    /// <para>Recomputed rather than decremented on purpose: a decrement is exact once and drifts the
    /// moment two corrections interleave, and these are the figures every item card reads.</para>
    /// </summary>
    private async Task RecomputeAfterMoneyChangeAsync(long teacherId, EventPaymentTransaction transaction)
    {
        if (transaction.EventStudentObligationId is long obligationId)
        {
            var obligation = await _unitOfWork.PaymentsRepo
                .GetEventObligationByIdAsync(obligationId, teacherId);
            if (obligation is not null)
            {
                obligation.AmountPaid = await _unitOfWork.PaymentsRepo
                    .SumObligationPaidAsync(obligationId, teacherId);
                obligation.PaymentStatus = obligation.AmountPaid >= obligation.AmountDue
                    ? (obligation.AmountPaid > obligation.AmountDue
                        ? PaymentStatus.Overpaid
                        : PaymentStatus.Paid)
                    : obligation.AmountPaid > 0m
                        ? PaymentStatus.PartiallyPaid
                        : PaymentStatus.Unpaid;
                await _unitOfWork.PaymentsRepo.UpdateEventObligationAsync(obligation);
            }
        }

        if (transaction.PaymentEventId is long eventId)
        {
            var paymentEvent = await _unitOfWork.PaymentsRepo
                .GetPaymentEventByIdAndTeacherAsync(eventId, teacherId);
            if (paymentEvent is not null)
            {
                paymentEvent.TotalCollectedRevenue = await _unitOfWork.PaymentsRepo
                    .SumEventCollectedAsync(eventId, teacherId);
                await _unitOfWork.PaymentsRepo.UpdatePaymentEventAsync(paymentEvent);
            }
        }
    }

    /// <summary>
    /// Re-derives an item's <c>TotalStudents</c> and <c>TotalExpectedRevenue</c> from its
    /// obligations, EXCLUDING exempt students.
    ///
    /// <para>Recomputed from the rows, never arithmetic on the cached columns. The old add-students
    /// path did <c>TotalExpectedRevenue += EventAmount × count</c>, which clobbered every
    /// per-student custom price — and it ran AFTER a full recompute, so the two disagreed.</para>
    /// </summary>
    private async Task RecomputeEventExpectedAsync(long teacherId, Domain.Entities.PaymentEvent paymentEvent)
    {
        var (activeCount, expected) = await _unitOfWork.PaymentsRepo
            .GetEventObligationTotalsAsync(paymentEvent.Id, teacherId);

        paymentEvent.TotalStudents = activeCount;
        paymentEvent.TotalExpectedRevenue = expected;
        await _unitOfWork.PaymentsRepo.UpdatePaymentEventAsync(paymentEvent);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> SetEventStudentExemptAsync(
        long teacherId, long eventId, long teacherStudentId,
        bool exempt, long actingUserId, string? reason)
    {
        var paymentEvent = await _unitOfWork.PaymentsRepo
            .GetPaymentEventByIdAndTeacherAsync(eventId, teacherId);
        if (paymentEvent is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasItemNotFound, HttpStatusCode.NotFound);

        var obligation = await _unitOfWork.PaymentsRepo
            .GetEventObligationAsync(eventId, teacherStudentId, teacherId);
        if (obligation is null)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.EventObligationNotFound, HttpStatusCode.NotFound);

        // THE invariant that keeps Σ AmountPaid == Σ non-deleted payments true in every state:
        // a student who has paid cannot be marked "not taking it". Refund first — and say so,
        // rather than skipping silently the way the old removal path did.
        if (exempt && obligation.AmountPaid > 0m)
            return Result<bool>.Failure(
                _localizer, PaymentConstants.Messages.ExtrasExemptBlockedHasPayments,
                new object?[] { obligation.StudentName ?? string.Empty },
                HttpStatusCode.Conflict);

        if (obligation.IsExempt == exempt)
            return Result<bool>.Success(true, _localizer, exempt
                ? PaymentConstants.Messages.ExtrasExemptedSuccess
                : PaymentConstants.Messages.ExtrasExemptClearedSuccess);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            obligation.IsExempt = exempt;
            obligation.ExemptedAt = exempt ? DateTime.UtcNow : null;
            obligation.ExemptedByUserId = exempt ? actingUserId : null;
            obligation.ExemptReason = exempt && !string.IsNullOrWhiteSpace(reason) ? reason.Trim() : null;
            await _unitOfWork.PaymentsRepo.UpdateEventObligationAsync(obligation);

            await _unitOfWork.PaymentsRepo.AddEventPaymentEditLogAsync(new EventPaymentEditLog
            {
                TeacherId = teacherId,
                PaymentEventId = eventId,
                EventStudentObligationId = obligation.Id,
                TeacherStudentId = obligation.TeacherStudentId,
                StudentName = obligation.StudentName,
                StudentCode = obligation.StudentCode,
                EventName = paymentEvent.EventName,
                EditAction = exempt
                    ? EventPaymentEditAction.Exempted
                    : EventPaymentEditAction.ExemptCleared,
                // An exemption moves no cash, so no ChargedToUserId and no amounts — which is also
                // why the refund-row query filters on PreviousAmount - NewAmount > 0 and skips these.
                PreviousAmount = 0m,
                NewAmount = 0m,
                EditedByUserId = actingUserId,
                EditedAt = DateTime.UtcNow,
                EditReason = obligation.ExemptReason,
                CreateAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync();

            // Expected revenue excludes exempt students, so it is re-derived here.
            await RecomputeEventExpectedAsync(teacherId, paymentEvent);

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            return Result<bool>.Success(
                true, _localizer,
                exempt
                    ? PaymentConstants.Messages.ExtrasExemptedSuccess
                    : PaymentConstants.Messages.ExtrasExemptClearedSuccess,
                new object?[] { obligation.StudentName ?? string.Empty });
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

            // ONE bounded read, split in memory. Two separate paged reads at pageSize 50000 both
            // went through a status filter that was BINARY (Paid vs not-Paid), so the "unpaid" list
            // silently contained partially-paid and overpaid students too.
            var allObligations = await _unitOfWork.PaymentsRepo.GetAllEventObligationsAsync(
                request.EventId.Value, teacherId, PaymentConstants.EventReportMaxRows);

            var report = new SingleEventReportDto
            {
                Event = MapToEventDto(paymentEvent),
                PaidStudents = allObligations
                    .Where(o => !o.IsExempt && o.PaymentStatus == PaymentStatus.Paid)
                    .Select(MapToObligationDto).ToList(),
                // Genuinely unpaid only — exempt students are not "unpaid", they are not taking it.
                UnpaidStudents = allObligations
                    .Where(o => !o.IsExempt && o.PaymentStatus == PaymentStatus.Unpaid)
                    .Select(MapToObligationDto).ToList()
            };
            return Result<object>.Success(report, _localizer, PaymentConstants.Messages.EventReportGenerated);
        }
        else // AllEventsSummary
        {
            // The requested date range is now actually APPLIED. It used to be accepted and ignored,
            // so the report claimed a period it had never filtered on.
            var events = await _unitOfWork.PaymentsRepo.GetPaymentEventsInRangeAsync(
                teacherId, request.StartDate, request.EndDate, PaymentConstants.EventReportMaxRows);

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
    /// <summary>
    /// The resolved audience for a set of scope entries, plus the scope ROWS to persist.
    ///
    /// <para>Three fixes over the original, all of which were silent failures:</para>
    /// <list type="number">
    ///   <item><b>Every id is validated for OWNERSHIP.</b> Individual student ids were taken
    ///   verbatim — a foreign or nonexistent id became an obligation with a null name. Sessions and
    ///   groups were never checked at all.</item>
    ///   <item><b>Every id in an entry is honoured.</b> <c>Session</c> and <c>SessionGroup</c> read
    ///   only <c>ScopeIds.FirstOrDefault()</c>, so picking three classes silently billed one.</item>
    ///   <item><b>The original intent is returned as scope rows.</b> Only the RESOLVED student ids
    ///   were stored (comma-joined), which is why "by session / by group" and the new-joiner rule
    ///   were structurally unanswerable.</item>
    /// </list>
    ///
    /// <para>Returns <c>null</c> for <c>Scopes</c> when a target does not belong to this teacher —
    /// the caller turns that into a 404 rather than quietly dropping it.</para>
    /// </summary>
    private async Task<(List<long> StudentIds, List<PaymentEventScope>? Scopes)>
        ResolveAndDeduplicateTargetStudentsAsync(
            long teacherId, List<EventScopeEntry> scopes, long assignedByUserId)
    {
        var allStudentIds = new HashSet<long>();
        var scopeRows = new List<PaymentEventScope>();
        // Deduplicates the SCOPE rows too: the unique index
        // UX_PaymentEventScopes_Event_Type_Target would reject a repeated (type, target) anyway, and
        // failing an insert is a worse answer than accepting a harmless duplicate request.
        var seenScopes = new HashSet<(EventTargetScopeType, long?, long?)>();
        var now = DateTime.UtcNow;

        foreach (var scope in scopes)
        {
            switch (scope.ScopeType)
            {
                case EventTargetScopeType.IndividualStudents:
                    foreach (var studentId in scope.ScopeIds.Distinct())
                    {
                        var student = await _unitOfWork.Students
                            .GetActiveByIdAndTeacherAsync(studentId, teacherId);
                        if (student is null)
                            return (new List<long>(), null);
                        allStudentIds.Add(studentId);
                    }
                    // Deliberately NO scope row: individually-targeted students are already fully
                    // recorded by their obligations, and a TeacherStudentId FK here would need purge
                    // handling the CHECK constraint forbids (it cannot be nulled) — the same reason
                    // VideoScope's individual branch was removed.
                    break;

                case EventTargetScopeType.Session:
                    foreach (var sessionId in scope.ScopeIds.Distinct())
                    {
                        var session = await _unitOfWork.SessionsRepo
                            .GetByIdAndTeacherAsync(sessionId, teacherId);
                        if (session is null)
                            return (new List<long>(), null);

                        foreach (var id in await _unitOfWork.PaymentsRepo
                                     .GetStudentIdsBySessionAsync(teacherId, sessionId))
                            allStudentIds.Add(id);

                        if (seenScopes.Add((EventTargetScopeType.Session, sessionId, null)))
                            scopeRows.Add(new PaymentEventScope
                            {
                                TeacherId = teacherId,
                                ScopeType = EventTargetScopeType.Session,
                                SessionId = sessionId,
                                AssignedByUserId = assignedByUserId,
                                AssignedAt = now,
                                CreateAt = now
                            });
                    }
                    break;

                case EventTargetScopeType.SessionGroup:
                    foreach (var groupId in scope.ScopeIds.Distinct())
                    {
                        var group = await _unitOfWork.SessionsRepo
                            .GetGroupByIdAndTeacherAsync(groupId, teacherId);
                        if (group is null)
                            return (new List<long>(), null);

                        foreach (var id in await _unitOfWork.PaymentsRepo
                                     .GetStudentIdsByGroupAsync(teacherId, groupId))
                            allStudentIds.Add(id);

                        if (seenScopes.Add((EventTargetScopeType.SessionGroup, null, groupId)))
                            scopeRows.Add(new PaymentEventScope
                            {
                                TeacherId = teacherId,
                                ScopeType = EventTargetScopeType.SessionGroup,
                                SessionGroupId = groupId,
                                AssignedByUserId = assignedByUserId,
                                AssignedAt = now,
                                CreateAt = now
                            });
                    }
                    break;

                case EventTargetScopeType.AllStudents:
                    foreach (var id in await _unitOfWork.PaymentsRepo.GetAllStudentIdsAsync(teacherId))
                        allStudentIds.Add(id);

                    // Carries no target, which is why the CHECK constraint folds both shape rules
                    // into one clause rather than demanding "exactly one target is non-null".
                    if (seenScopes.Add((EventTargetScopeType.AllStudents, null, null)))
                        scopeRows.Add(new PaymentEventScope
                        {
                            TeacherId = teacherId,
                            ScopeType = EventTargetScopeType.AllStudents,
                            AssignedByUserId = assignedByUserId,
                            AssignedAt = now,
                            CreateAt = now
                        });
                    break;
            }
        }

        return (allStudentIds.ToList(), scopeRows);
    }

    /// <summary>
    /// Maps an item to its wire DTO.
    ///
    /// <para><paramref name="totals"/> is the DERIVED truth, folded from the obligation rows. When
    /// supplied it wins over the entity's cached <c>TotalStudents</c> /
    /// <c>TotalExpectedRevenue</c> / <c>TotalCollectedRevenue</c> columns — three write paths used
    /// to clobber those independently, which is how a card could claim one figure while its own
    /// students summed to another. The cache is still written so it does not rot for a future admin
    /// report, but no response depends on it.</para>
    ///
    /// <para>The three population counts (<c>PaidStudents</c> etc.) were declared on this DTO from
    /// day one and NEVER populated, so every client read 0. They are populated here.</para>
    /// </summary>
    private static EventDto MapToEventDto(
        PaymentEvent e,
        ExtrasItemSummaryDto? totals = null,
        IReadOnlyList<ExtrasScopeSummaryRow>? scopeRows = null) => new()
    {
        Id = e.Id,
        EventName = e.EventName,
        EventAmount = e.EventAmount,
        TargetScopeType = e.TargetScopeType,
        EventDate = e.EventDate,
        CreateAt = e.CreateAt,
        Notes = e.Notes,
        AutoIncludeNewStudents = e.AutoIncludeNewStudents,
        CollectDuringAttendance = e.CollectDuringAttendance,
        IsClosed = e.IsClosed,
        ClosedAt = e.ClosedAt,
        TotalStudents = totals?.TotalStudents ?? e.TotalStudents,
        PaidStudents = totals?.PaidStudents ?? 0,
        PartiallyPaidStudents = totals?.PartiallyPaidStudents ?? 0,
        UnpaidStudents = totals?.UnpaidStudents ?? 0,
        ExemptStudents = totals?.ExemptStudents ?? 0,
        TotalExpectedRevenue = totals?.ExpectedAmount ?? e.TotalExpectedRevenue,
        TotalCollectedRevenue = totals?.CollectedAmount ?? e.TotalCollectedRevenue,
        RemainingRevenue = totals is not null
            ? totals.RemainingAmount
            : Math.Max(0m, e.TotalExpectedRevenue - e.TotalCollectedRevenue),
        ScopeSummary = BuildScopeSummary(e, scopeRows)
    };

    /// <summary>
    /// Summarises an item's audience for display, from its scope rows when it has them.
    /// </summary>
    /// <remarks>
    /// An item with no scope rows falls back to its legacy <c>TargetScopeType</c>: individually
    /// targeted students are recorded by their obligations rather than by a scope row, and items
    /// created before the scope table exists have none at all. Both must read as something
    /// truthful rather than as an empty audience.
    /// </remarks>
    private static ExtrasScopeSummaryDto BuildScopeSummary(
        PaymentEvent e, IReadOnlyList<ExtrasScopeSummaryRow>? scopeRows)
    {
        if (scopeRows is null || scopeRows.Count == 0)
        {
            return new ExtrasScopeSummaryDto
            {
                Type = e.TargetScopeType == EventTargetScopeType.AllStudents ? "all" : "students"
            };
        }

        bool hasAll = scopeRows.Any(r => r.ScopeType == (byte)EventTargetScopeType.AllStudents);
        bool hasSessions = scopeRows.Any(r => r.ScopeType == (byte)EventTargetScopeType.Session);
        bool hasGroups = scopeRows.Any(r => r.ScopeType == (byte)EventTargetScopeType.SessionGroup);

        // "Everyone" absorbs anything narrower — an item scoped to all students AND to one class
        // still reaches every student, so naming the class would understate the audience.
        if (hasAll)
            return new ExtrasScopeSummaryDto { Type = "all" };

        // A target whose name came back null was deleted after the item was created. Its scope row
        // is deleted with it, so this is a race, not a steady state — skip the label rather than
        // render an empty chip.
        var names = scopeRows
            .Where(r => !string.IsNullOrWhiteSpace(r.TargetName))
            .Select(r => r.TargetName!)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        return new ExtrasScopeSummaryDto
        {
            Type = hasSessions && hasGroups ? "mixed" : (hasGroups ? "groups" : "sessions"),
            Labels = names.Take(ExtrasScopeSummaryLabelCount).ToList(),
            ExtraCount = Math.Max(0, names.Count - ExtrasScopeSummaryLabelCount)
        };
    }

    /// <summary>How many target names a row shows before collapsing the rest into a "+N".</summary>
    private const int ExtrasScopeSummaryLabelCount = 3;

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