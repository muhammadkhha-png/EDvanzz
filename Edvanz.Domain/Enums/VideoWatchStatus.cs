namespace Edvanz.Domain.Enums;

/// <summary>
/// Three-state watch indicator for a student's video-list card (V4). Computed on read
/// from the per-(video, student) <see cref="Entities.VideoAnalytics"/> row against the
/// video's <see cref="Entities.VideoAsset.DurationSeconds"/> — never persisted:
/// <list type="bullet">
///   <item><see cref="Completed"/> — watched ≥ <c>VideoConstants.CompletionThresholdPercent</c>
///         of the duration (reuses the same threshold as the teacher analytics
///         <c>completedCount</c> so the two never disagree).</item>
///   <item><see cref="InProgress"/> — opened / has watch seconds but below the completion
///         threshold.</item>
///   <item><see cref="NotStarted"/> — no analytics row (never opened), or 0 watch seconds.</item>
/// </list>
/// Serialized as a string via the global <c>JsonStringEnumConverter</c>.
/// Numeric values are part of the wire contract — never reorder.
/// </summary>
public enum VideoWatchStatus : byte
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
}
