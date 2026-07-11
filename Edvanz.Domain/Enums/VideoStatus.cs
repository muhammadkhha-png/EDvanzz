namespace Edvanz.Domain.Enums;

/// <summary>
/// Publish state of a <see cref="Entities.VideoAsset"/> (Track D1 / §5 open
/// question "Visibility (Published/Draft)"). Gates student visibility
/// alongside <see cref="Entities.VideoAsset.PublishDate"/> — see
/// <c>IVideoAssetRepo.IsStudentInVideoScopeAsync</c> and
/// <c>GetVisibleVideosForStudentAsync</c>.
///
/// Persisted as <c>tinyint</c>, default <see cref="Published"/> so existing
/// rows (created before this column existed) keep their current student
/// visibility on migration.
/// </summary>
public enum VideoStatus
{
    /// <summary>Hidden from students regardless of <c>PublishDate</c>.</summary>
    Draft = 0,

    /// <summary>
    /// Visible to students once <c>PublishDate</c> is null or has passed.
    /// A future <c>PublishDate</c> with this status is a "scheduled" video.
    /// </summary>
    Published = 1
}
