namespace Edvanz.Application.Options;

/// <summary>
/// Azure Blob Storage configuration. Bound from appsettings.json section "AzureBlobStorage".
/// </summary>
public class AzureBlobStorageOptions
{
    public const string Section = "AzureBlobStorage";

    /// <summary>Azure Storage account connection string. Never source-controlled.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Container name for the legacy video-attachments blobs (still referenced by the private
    /// SAS read path via <c>GetReadUrlAsync</c> until fully migrated onto the uploads container).
    /// </summary>
    public string ContainerName { get; set; } = "video-attachments";

    /// <summary>
    /// The single PRIVATE container that all files served through the gated
    /// <c>GET /api/files/{fileId}</c> endpoint live in (registry-backed). Anonymous access is
    /// disabled on this container; reads are handed out as time-limited SAS URLs after the
    /// caller passes the resource-scoped authorization check.
    /// </summary>
    public string UploadsContainerName { get; set; } = "uploads";

    /// <summary>
    /// Lifetime, in minutes, of a SAS read URL handed out for an uploads-container blob. Sized
    /// to outlast a single exam sitting so a long-running image reference does not break
    /// mid-session. Defaults to 4 hours.
    /// </summary>
    public int UploadsSasLifetimeMinutes { get; set; } = 240;

    /// <summary>
    /// Grace window, in hours, before the garbage collector reclaims a <c>Pending</c> file
    /// (uploaded but never attached to a resource). Gives the frontend time to finish the
    /// create/update request that references a freshly uploaded file. Defaults to 24 hours.
    /// (<c>Detached</c> files are reclaimed on the next sweep regardless of this window.)
    /// </summary>
    public int UploadsPendingGraceHours { get; set; } = 24;
}
