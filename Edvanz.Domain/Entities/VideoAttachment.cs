using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities;

/// <summary>
/// A file uploaded alongside a video (Track F / §5 "Files / Attachments").
/// Hard-delete-only, same family as <see cref="VideoAsset"/> — deleting the
/// video deletes both the row and its underlying blob (best-effort, outside
/// the DB transaction; see <c>VideoService.DeleteVideoAsync</c>).
///
/// <see cref="BlobPath"/> is the canonical reference; read URLs are
/// time-limited SAS URLs generated per request via
/// <c>IFileStorageService.GetReadUrlAsync</c> — never stored.
/// </summary>
public class VideoAttachment : BaseEntity
{
    /// <summary>
    /// Foreign key to the parent video. NoAction — same posture as
    /// <see cref="VideoScope"/>/<see cref="VideoAnalytics"/> siblings; the
    /// service layer deletes attachment blobs before the video's audit
    /// snapshot + hard-delete.
    /// </summary>
    public long VideoAssetId { get; set; }
    public VideoAsset VideoAsset { get; set; } = null!;

    /// <summary>Tenant scope, denormalized for index performance (VCM convention).</summary>
    public long TeacherId { get; set; }

    /// <summary>Original file name as uploaded by the teacher.</summary>
    public string FileName { get; set; } = null!;

    /// <summary>MIME type, e.g. <c>application/pdf</c>.</summary>
    public string ContentType { get; set; } = null!;

    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Blob path: <c>{teacherId}/{videoAssetId}/{guid}-{fileName}</c> — tenant-
    /// scoped even though access is also DB-gated (defense in depth).
    /// </summary>
    public string BlobPath { get; set; } = null!;

    /// <summary>
    /// The user (Teacher or Assistant) who uploaded this file. Nullable:
    /// SET_NULL on actor purge, same rationale as
    /// <see cref="VideoAsset.CreatedByUserId"/>.
    /// </summary>
    public long? UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }
}
