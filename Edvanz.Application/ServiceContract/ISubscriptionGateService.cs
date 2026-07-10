namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Small helper used by create paths that enforce free-tier quotas. Answers the single
/// question "does this teacher currently have an Active/ExpiringSoon subscription?" so a
/// service can decide whether a creation cap applies.
///
/// Kept separate from the authorization-layer ActiveSubscriptionHandler: that handler gates
/// HTTP requests, whereas this is business logic invoked inside the service layer.
/// </summary>
public interface ISubscriptionGateService
{
    /// <summary>
    /// True when the teacher has an Active or ExpiringSoon subscription (i.e. quotas are lifted).
    /// False for expired / never-subscribed teachers, who are held to the free-tier caps.
    /// </summary>
    Task<bool> HasActiveSubscriptionAsync(long teacherId);
}
