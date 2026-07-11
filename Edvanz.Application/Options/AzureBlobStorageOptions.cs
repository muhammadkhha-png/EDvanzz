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
}
