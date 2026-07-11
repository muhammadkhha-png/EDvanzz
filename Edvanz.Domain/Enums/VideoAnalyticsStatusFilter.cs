namespace Edvanz.Domain.Enums;

/// <summary>
/// Server-side row filter for the teacher analytics report
/// (<c>GET /api/videos/{id}/analytics</c>, G-ANL-4). Maps to the
/// <c>statusFilter</c> query parameter; bound to a JSON-string converter at
/// the controller boundary, same convention as <see cref="VideoAnalyticsSortBy"/>.
///
/// "Completed" uses the same <c>VideoConstants.CompletionThresholdPercent</c>
/// threshold as the <c>completedCount</c> aggregate (G-ANL-1) — one
/// definition, reused by both.
/// </summary>
public enum VideoAnalyticsStatusFilter
{
    /// <summary>No filter — every resolved student in scope.</summary>
    All = 0,

    /// <summary>Students who have opened the video at least once.</summary>
    Seen = 1,

    /// <summary>Students who have never opened the video.</summary>
    Unseen = 2,

    /// <summary>Students whose estimated completion meets the threshold.</summary>
    Completed = 3
}
