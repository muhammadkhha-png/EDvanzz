using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Repository for the PendingSubscriptionPayment table — the in-flight queue
/// of subscription-renewal payment attempts (§5.2).
///
/// Pending payments NEVER grant access (BR-SUB-008). Only after the lifecycle
/// reaches Confirmed does SubscriptionService produce a TeacherSubscription row.
/// This repo owns lookup, listing, status transitions, and bulk expiry of the
/// pending queue.
///
/// All query logic is encapsulated here; the Application layer never builds
/// raw expression predicates.
/// </summary>
public interface ISubscriptionPaymentRepo : IGenericRepo<PendingSubscriptionPayment, long>
{
    // ══════════════════════════════════════════════
    // SINGLE-ROW LOOKUPS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Loads a pending payment by Id, scoped to the owning teacher (tenant guard).
    /// Used by the renewal-status polling endpoint and the manual-submit flow.
    /// </summary>
    Task<PendingSubscriptionPayment?> GetByIdAndTeacherAsync(long pendingPaymentId, long teacherId);

    /// <summary>
    /// Super-admin lookup that bypasses the teacher tenant guard (admin sees all).
    /// Used by the admin approve / reject endpoints.
    /// </summary>
    Task<PendingSubscriptionPayment?> GetByIdForAdminAsync(long pendingPaymentId);

    // (GetByPaymobSessionIdAsync was removed 2026-07-17 with the Paymob webhook —
    // the renewal flow is manual-only. The PaymobSessionId column and its index
    // remain for historical rows.)

    // ══════════════════════════════════════════════
    // LIST QUERIES
    // ══════════════════════════════════════════════

    /// <summary>
    /// Paginated admin queue of pending payments awaiting super-admin approval
    /// (Status = AwaitingSuperAdminApproval). Ordered InitiatedAt ASC (oldest first
    /// — FIFO so tutors don't wait while newer ones jump the queue).
    ///
    /// Returns the raw entities; the AdminSubscriptionService is responsible for
    /// decryption (REQ-SUB-NFR-004) and tenant-context enrichment.
    /// </summary>
    Task<(IReadOnlyList<PendingSubscriptionPayment> Items, int TotalCount)> GetAdminQueuePagedAsync(
        int page, int pageSize);

    /// <summary>
    /// Returns true if the teacher has ANY pending payment in non-terminal status
    /// (Initiated or AwaitingSuperAdminApproval). Used to short-circuit a duplicate
    /// initiate request — the renewal flow refuses to create a second pending row
    /// while one is already in flight.
    /// </summary>
    Task<bool> HasInFlightPaymentAsync(long teacherId);

    // ══════════════════════════════════════════════
    // BULK OPERATIONS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Bulk-expire pending payments whose Status is still Initiated and whose
    /// InitiatedAt is older than the supplied cutoff (typically UtcNow.AddHours(-24)).
    /// Run by PendingPaymentExpiryJob hourly (§12 Phase 8 / EC-18).
    ///
    /// Uses ExecuteUpdateAsync — no in-memory load — same pattern as
    /// AttendanceRepo.NullifyOccurrenceReferencesForSessionAsync.
    /// Returns the number of rows transitioned to Expired.
    /// </summary>
    Task<int> ExpireStalePendingPaymentsAsync(DateTime cutoffUtc);
    /// <summary>
    /// Marks a tracked PendingSubscriptionPayment as Modified so EF will write its
    /// changes on the next SaveChanges. Called by SubscriptionService when it
    /// transitions Status / writes ResolvedByUserId / persists the encrypted
    /// submitted-details blob. SaveChanges is NOT called here.
    /// </summary>
    void UpdatePending(PendingSubscriptionPayment pending);

    /// <summary>
    /// Detaches a PendingSubscriptionPayment from the change tracker. Called by
    /// SubscriptionService.ConfirmPaymentAsync inside the §6.6 retry loop, between
    /// a failed attempt and the re-fetch, so the second attempt starts with a clean
    /// tracker state.
    /// </summary>
    void DetachPending(PendingSubscriptionPayment pending);
}