using Edvanz.Domain.Entities;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Repository for <see cref="SubscriptionRequest"/> — the teacher-initiated queue for
/// ACTIVATING a subscription (new tutors / renewals choosing Full or Managerial). Mirrors the
/// CapacityIncreaseRequest queue: tenant-scoped lookups for the teacher side, unscoped lookups
/// for the super-admin review side, and a paginated FIFO admin queue.
///
/// All query logic is encapsulated here; the Application layer never builds raw predicates.
/// </summary>
public interface ISubscriptionRequestRepo : IGenericRepo<SubscriptionRequest, long>
{
    /// <summary>Loads a request by Id, scoped to the owning teacher (tenant guard).</summary>
    Task<SubscriptionRequest?> GetByIdAndTeacherAsync(long requestId, long teacherId);

    /// <summary>Super-admin lookup that bypasses the teacher tenant guard (admin sees all).</summary>
    Task<SubscriptionRequest?> GetByIdForAdminAsync(long requestId);

    /// <summary>
    /// True if the teacher has a live Pending request. Backstopped by the filtered unique index
    /// UX_SubscriptionRequests_Teacher_Pending.
    /// </summary>
    Task<bool> HasPendingRequestAsync(long teacherId);

    /// <summary>Paginated admin queue of Pending requests, oldest first (FIFO).</summary>
    Task<(IReadOnlyList<SubscriptionRequest> Items, int TotalCount)> GetAdminQueuePagedAsync(
        int page, int pageSize);

    /// <summary>Paginated history of the teacher's own requests, all statuses, newest first.</summary>
    Task<(IReadOnlyList<SubscriptionRequest> Items, int TotalCount)> GetByTeacherPagedAsync(
        long teacherId, int page, int pageSize);

    /// <summary>Marks a tracked request Modified. SaveChanges is NOT called here (caller owns the commit).</summary>
    void UpdateRequest(SubscriptionRequest request);
}
