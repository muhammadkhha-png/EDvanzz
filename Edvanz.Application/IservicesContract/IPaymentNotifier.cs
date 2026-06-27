namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Intent-revealing contract for payment-triggered outbound messaging.
/// Part of Option-B: per-module notifier interfaces over IMessageDispatcher.
///
/// Caller: PaymentService.CollectPaymentAsync (post-commit, after ownsTransaction guard).
/// Engine: IMessageDispatcher.DispatchAsync.
///
/// REQ-MSG-020: Automated triggers for PaymentReceived and PartialPaymentRecorded.
///
/// OUT OF SCOPE here (time-based, need a scheduled scanning job):
///   - TriggerEventType.PaymentNotReceived
///   - TriggerEventType.ConsecutiveNonPayment
/// </summary>
public interface IPaymentNotifier
{
    /// <summary>
    /// Fires when a payment is successfully collected for a student.
    /// Always dispatches <see cref="TriggerEventType.PaymentReceived"/>.
    /// Additionally dispatches <see cref="TriggerEventType.PartialPaymentRecorded"/>
    /// when <paramref name="isPartial"/> is <c>true</c>.
    /// REQ-MSG-020.
    /// </summary>
    /// <param name="teacherId">Resolved from JWT — never from route or body.</param>
    /// <param name="teacherStudentId">TeacherStudent.Id of the paying student.</param>
    /// <param name="sessionId">Session associated with the payment period (nullable — event payments may not have one).</param>
    /// <param name="sessionName">Human-readable session name for the message template.</param>
    /// <param name="amountPaid">The amount collected in this transaction.</param>
    /// <param name="paymentPeriodLabel">Human-readable period label, e.g. "January 2025".</param>
    /// <param name="isPartial">
    /// <c>true</c> when the period's PaymentStatus is PartiallyPaid after this collection.
    /// </param>
    Task OnPaymentRecordedAsync(
        long teacherId,
        long teacherStudentId,
        long? sessionId,
        string sessionName,
        decimal amountPaid,
        string paymentPeriodLabel,
        bool isPartial);
}
