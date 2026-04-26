using Edvanz.Application.Options;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Edvanz.Infrastructure.BackGroundJobs;

/// <summary>
/// Hourly pending-payment expiry sweep (EC-18 / §12 Phase 8).
///
/// BEHAVIOR:
///   Calls ISubscriptionPaymentRepo.ExpireStalePendingPaymentsAsync with the
///   cutoff timestamp UtcNow - SubscriptionDefaultsOptions.PendingPaymentExpiryHours
///   (default 24). The repo emits a single SQL UPDATE — no entities loaded.
///
/// IDEMPOTENT:
///   The repo's WHERE clause filters Status = Initiated, so a second run on the
///   same data is a no-op.
///
/// SCOPE:
///   Only Initiated rows expire. AwaitingSuperAdminApproval rows stay until the
///   admin acts on them — those represent real money the tutor sent that needs
///   manual reconciliation.
/// </summary>
public class PendingPaymentExpiryJob
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly SubscriptionDefaultsOptions _defaults;
    private readonly ILogger<PendingPaymentExpiryJob> _logger;

    public PendingPaymentExpiryJob(
        IUnitOfWork unitOfWork,
        IOptions<SubscriptionDefaultsOptions> defaults,
        ILogger<PendingPaymentExpiryJob> logger)
    {
        _unitOfWork = unitOfWork;
        _defaults = defaults.Value;
        _logger = logger;
    }

    /// <summary>
    /// Hangfire entry point. Registered as an hourly recurring job in Program.cs.
    /// </summary>
    public async Task RunAsync()
    {
        DateTime cutoffUtc = DateTime.UtcNow.AddHours(-_defaults.PendingPaymentExpiryHours);

        int expired = await _unitOfWork.SubscriptionPaymentsRepo
            .ExpireStalePendingPaymentsAsync(cutoffUtc);

        if (expired > 0)
        {
            _logger.LogInformation(
                "PendingPaymentExpiryJob: expired {Count} stale Initiated rows older than {Cutoff}",
                expired, cutoffUtc);
        }
    }
}