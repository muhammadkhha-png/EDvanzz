namespace Edvanz.Application.IservicesContract;

/// <summary>
/// Blob-storage abstraction for uploaded files (Track F — video attachments,
/// §5). Interface lives in Application; the Azure implementation lives in
/// Infrastructure — same tier split as <see cref="IWhatsAppSender"/>.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Uploads a stream to the given blob path and returns that same path
    /// (the caller persists it — the DB owns the canonical reference, not a
    /// URL, since SAS URLs are time-limited and regenerated per read).
    /// </summary>
    Task<string> UploadAsync(string blobPath, Stream content, string contentType);

    /// <summary>Deletes a blob. No-ops if it doesn't exist (idempotent for cleanup flows).</summary>
    Task DeleteAsync(string blobPath);

    /// <summary>
    /// Generates a time-limited SAS read URL for a blob. When
    /// <paramref name="downloadFileName"/> is provided, the SAS token sets
    /// Content-Disposition to force a browser/HTTP-client download with that
    /// filename instead of rendering the file inline (relevant for PDFs,
    /// which browsers otherwise open in a viewer tab). Null (default) keeps
    /// existing behavior — no disposition override.
    /// </summary>
    Task<string> GetReadUrlAsync(string blobPath, string? downloadFileName = null);
}
