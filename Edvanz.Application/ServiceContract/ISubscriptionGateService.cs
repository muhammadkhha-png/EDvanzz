namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Business-layer gate for the free-tier model. Answers "is this teacher subscribed?" and, for
/// unsubscribed teachers, whether they may still create another item in a given module under its
/// configured quota (see the ModuleQuota table / ModuleQuotaKeys).
/// </summary>
public interface ISubscriptionGateService
{
    /// <summary>
    /// True when the teacher has an Active or ExpiringSoon subscription (i.e. quotas are lifted).
    /// False for expired / never-subscribed teachers, who are held to the free-tier caps.
    /// </summary>
    Task<bool> HasActiveSubscriptionAsync(long teacherId);

    /// <summary>
    /// Whether the teacher may create another item in <paramref name="moduleKey"/>:
    /// subscribed → always true; otherwise the current count (from <paramref name="currentCountFactory"/>)
    /// must be below the module's free-tier limit. The count factory is only invoked when needed
    /// (skipped for subscribers and for subscriber-only modules whose limit is 0).
    /// </summary>
    Task<bool> CanCreateAsync(long teacherId, string moduleKey, Func<Task<int>> currentCountFactory);
}
