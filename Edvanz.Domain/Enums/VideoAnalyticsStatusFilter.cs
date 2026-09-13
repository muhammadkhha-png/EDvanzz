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

    /// <summary>
    /// Students who have actually WATCHED the video — cleared
    /// <c>VideoConstants.WatchedMinSeconds</c>. Opening it and leaving does not count;
    /// see <see cref="OpenedOnly"/>.
    /// </summary>
    Seen = 1,

    /// <summary>
    /// Students who have not watched it — the union of <see cref="OpenedOnly"/> and
    /// <see cref="NeverOpened"/>. This is the honest complement of <see cref="Seen"/>,
    /// so Seen + Unseen always equals the students in scope.
    /// </summary>
    Unseen = 2,

    /// <summary>Students whose estimated completion meets the threshold.</summary>
    Completed = 3,

    /// <summary>
    /// Students who opened the video but never cleared the watch bar. They have the link
    /// and showed intent — a different problem from a student who never opened it, and
    /// the reason <see cref="Unseen"/> needed splitting.
    /// </summary>
    OpenedOnly = 4,

    /// <summary>Students with no analytics row at all — they never pressed play.</summary>
    NeverOpened = 5
}
