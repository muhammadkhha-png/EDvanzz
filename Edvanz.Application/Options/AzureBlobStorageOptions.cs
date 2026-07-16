namespace Edvanz.Application.Options;

/// <summary>
/// Azure Blob Storage configuration for video attachments (Track F, §5).
/// Bound from appsettings.json section "AzureBlobStorage".
/// </summary>
public class AzureBlobStorageOptions
{
    public const string Section = "AzureBlobStorage";

    /// <summary>Azure Storage account connection string. Never source-controlled.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Container name attachments are uploaded into.</summary>
    public string ContainerName { get; set; } = "video-attachments";

    /// <summary>
    /// Container for the generic upload endpoint. PUBLIC (anonymous blob read) so
    /// the returned URLs are permanent and directly embeddable. Separate from
    /// <see cref="ContainerName"/> (private video attachments served via SAS).
    /// </summary>
    public string PublicContainerName { get; set; } = "uploads";
}
