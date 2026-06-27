using Edvanz.Application.Dtos.DispatcherDtos;
using Edvanz.Application.Dtos.MessageResolver;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities.Messaging;
using Microsoft.Extensions.Logging;

namespace Edvanz.Application.Services;

/// <summary>
/// Translates payment domain events into IMessageDispatcher calls.
/// Part of Option-B: per-module notifier over IMessageDispatcher.
///
/// Design rules:
/// 1. Never throws — all dispatch failures are caught, logged, and swallowed.
///    The originating payment write already committed before this runs.
/// 2. Called post-commit only (ownsTransaction guard in PaymentService).
///    This guarantees Hangfire jobs are never enqueued for rolled-back data.
/// 3. Always dispatches PaymentReceived; additionally dispatches
///    PartialPaymentRecorded when the period is still partially paid.
///
/// NOT covered here (time-based — need a scheduled scanning job):
///   - TriggerEventType.PaymentNotReceived
///   - TriggerEventType.ConsecutiveNonPayment
/// </summary>
public sealed class PaymentNotifier : IPaymentNotifier
{
    private readonly IMessageDispatcher _dispatcher;
    private readonly ILogger<PaymentNotifier> _logger;

    public PaymentNotifier(
        IMessageDispatcher dispatcher,
        ILogger<PaymentNotifier> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnPaymentRecordedAsync(
        long teacherId,
        long teacherStudentId,
        long? sessionId,
        string sessionName,
        decimal amountPaid,
        string paymentPeriodLabel,
        bool isPartial)
    {
        // Shared context — same data for both events dispatched below.
        var context = new MessageResolveContext
        {
            SessionName = sessionName,
            Date = DateTime.UtcNow,
            PaymentStatus = isPartial ? "Partially Paid" : "Paid",
            PaymentAmount = amountPaid,
            PaymentPeriod = paymentPeriodLabel
        };

        // 1. PaymentReceived — always fires on every successful collection.
        await SafeDispatchAsync(new DispatchRequest
        {
            TeacherId = teacherId,
            EventType = TriggerEventType.PaymentReceived,
            StudentIds = new List<long> { teacherStudentId },
            SessionId = sessionId,
            SharedContext = context
        });

        // 2. PartialPaymentRecorded — only when the period is still outstanding.
        if (isPartial)
        {
            await SafeDispatchAsync(new DispatchRequest
            {
                TeacherId = teacherId,
                EventType = TriggerEventType.PartialPaymentRecorded,
                StudentIds = new List<long> { teacherStudentId },
                SessionId = sessionId,
                SharedContext = context
            });
        }
    }

    // ── SAFE WRAPPER ──────────────────────────────────────────────────────────

    private async Task SafeDispatchAsync(DispatchRequest request)
    {
        try
        {
            await _dispatcher.DispatchAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[PaymentNotifier] Dispatch failed for event {Event} " +
                "(Teacher={TeacherId}, Student={StudentId}). " +
                "Payment is committed; notification was not delivered.",
                request.EventType, request.TeacherId,
                request.StudentIds.FirstOrDefault());
        }
    }
}
