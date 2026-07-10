using Edvanz.Application.Options;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Helpers;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace Edvanz.Application.Services;

/// <summary>
/// Resolves whether a teacher currently has an Active/ExpiringSoon subscription, reusing the
/// same source of truth as the authorization layer: the current-subscription projection plus
/// <see cref="SubscriptionStatusCalculator"/> (status is derived, never stored — D-08).
/// </summary>
public sealed class SubscriptionGateService : ISubscriptionGateService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptionsMonitor<FreeTierQuotaOptions> _quotaOptions;

    public SubscriptionGateService(
        IUnitOfWork unitOfWork,
        IOptionsMonitor<FreeTierQuotaOptions> quotaOptions)
    {
        _unitOfWork = unitOfWork;
        _quotaOptions = quotaOptions;
    }

    /// <inheritdoc />
    public FreeTierQuotaOptions Quotas => _quotaOptions.CurrentValue;

    /// <inheritdoc />
    public async Task<bool> HasActiveSubscriptionAsync(long teacherId)
    {
        var projection = await _unitOfWork.Users.GetCurrentSubscriptionStatusAsync(teacherId);
        if (projection is null)
            return false;

        var subForStatus = new TeacherSubscription
        {
            StartDate = projection.StartDate,
            EndDate = projection.EndDate,
            IsCurrent = true
        };

        var status = SubscriptionStatusCalculator.Derive(subForStatus, DateTime.UtcNow);
        return status == SubscriptionStatus.Active || status == SubscriptionStatus.ExpiringSoon;
    }
}
