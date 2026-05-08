namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies a single row in the <c>VideoWatchEvents</c> append-only event log.
///
/// REQ-VCM-FR-03 / Story B-C: The event log unifies Open and Stop events in one
/// table. Distinguishing them with a discriminator (instead of two tables) keeps
/// the "find last event for this device" query single-index, single-seek.
///
/// Numeric values are part of the persisted contract — never reorder or repurpose.
/// </summary>
public enum VideoEventType : byte
{
    /// <summary>
    /// Student opened the video. Recorded by <c>POST /api/videos/{id}/start</c>.
    /// <c>DeltaSinceLastSeconds</c> is always 0 for Open events.
    /// </summary>
    Open = 1,

    /// <summary>
    /// Student stopped watching (paused, backgrounded, navigated away, or closed).
    /// Recorded by <c>POST /api/videos/{id}/stop</c>. <c>DeltaSinceLastSeconds</c>
    /// carries the server-validated, clamped delta against the most recent prior
    /// event for the same (student, video, device) tuple.
    /// </summary>
    Stop = 2
}
