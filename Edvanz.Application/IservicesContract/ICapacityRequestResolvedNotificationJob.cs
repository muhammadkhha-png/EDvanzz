using Hangfire;

namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Hangfire job triggered by AdminSubscriptionService.ApproveCapacityRequestAsync /
/// RejectCapacityRequestAsync. Sends Push + inserts a UserNotification row telling the
/// teacher the outcome of their capacity-increase request.
///
/// Separate from the payment notification jobs because:
///   - Different localization keys (CapacityRequestApproved/RejectedTitle/Body).
///   - Different deep-link payload (routes to the capacity-requests screen).
///   - Different NotificationCategory audit trail (CapacityRequestApproved/Rejected).
/// </summary>
public interface ICapacityRequestResolvedNotificationJob
{
    /// <summary>
    /// Dispatches the resolution notification across all channels.
    /// </summary>
    /// <param name="teacherId">The teacher whose capacity request was resolved.</param>
    /// <param name="requestId">The CapacityIncreaseRequest row id.</param>
    /// <param name="approved">True for approved (body carries the new capacity), false for rejected.</param>
    /// <param name="rejectionReason">The admin's reason when <paramref name="approved"/> is false; ignored otherwise.</param>
    [Queue("notifications")]
    Task SendAsync(long teacherId, long requestId, bool approved, string? rejectionReason);
}
