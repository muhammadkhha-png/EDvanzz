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

    /// <summary>
    /// Uploads a stream to the PUBLIC container and returns its PERMANENT
    /// absolute URL (anonymously readable, no SAS token). Use for assets meant
    /// to be embedded / displayed long-term (the generic upload endpoint).
    /// Contrast <see cref="UploadAsync"/>, which targets the private container
    /// and is read back via time-limited SAS.
    /// </summary>
    Task<string> UploadPublicAsync(string blobPath, Stream content, string contentType);

    /// <summary>Deletes a blob from the PUBLIC container. Idempotent (no-op if it doesn't exist).</summary>
    Task DeletePublicAsync(string blobPath);

    /// <summary>
    /// Parses a public-blob URL back to its blob path (the blob name within the
    /// public container), or returns null if the URL is not a blob in this
    /// account's public container. Used to authorize and act on delete / replace
    /// requests that arrive as a URL rather than a stored path.
    /// </summary>
    string? ResolvePublicBlobPath(string url);
}
