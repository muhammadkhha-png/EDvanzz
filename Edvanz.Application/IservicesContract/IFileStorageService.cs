namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Blob-storage abstraction. Interface lives in Application; the Azure implementation lives in
/// Infrastructure — same tier split as <see cref="IWhatsAppSender"/>.
///
/// All files served through the gated <c>GET /api/files/{fileId}</c> endpoint live in the single
/// PRIVATE uploads container. The registry (<c>FileObject</c>) owns the canonical reference (the
/// blob path); read URLs are time-limited SAS URLs regenerated per authorized read — never
/// persisted, never anonymously readable.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Uploads a stream to the given blob path in the private uploads container and returns that
    /// same path (the caller persists it on the registry row).
    /// </summary>
    Task<string> UploadAsync(string blobPath, Stream content, string contentType);

    /// <summary>Deletes a blob from the uploads container. No-ops if it doesn't exist (idempotent).</summary>
    Task DeleteAsync(string blobPath);

    /// <summary>
    /// Generates a time-limited SAS read URL for a blob in the uploads container. When
    /// <paramref name="downloadFileName"/> is provided, the SAS token sets Content-Disposition to
    /// force a download with that filename (relevant for PDFs). When
    /// <paramref name="lifetimeMinutes"/> is null the configured
    /// <c>AzureBlobStorageOptions.UploadsSasLifetimeMinutes</c> default is used.
    /// </summary>
    Task<string> GetReadUrlAsync(string blobPath, string? downloadFileName = null, int? lifetimeMinutes = null);
}
