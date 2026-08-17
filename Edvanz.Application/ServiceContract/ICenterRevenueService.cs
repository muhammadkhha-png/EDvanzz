using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Center;

namespace Edvanz.Application.ServiceContract;

/// <summary>Center revenue-share report: per teacher and center-wide, the center's cut on BOTH real
/// collection and expected revenue for a month.</summary>
public interface ICenterRevenueService
{
    /// <param name="month">"YYYY-MM"; null = the center's current local (Africa/Cairo) month.</param>
    Task<Result<CenterRevenueReportDto>> GetRevenueAsync(long centerId, string? month);
}
