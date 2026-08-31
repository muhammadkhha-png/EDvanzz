namespace Edvanz.Domain.Constants;

/// <summary>
/// Constants for the generic file-upload endpoint (images + PDF → permanent
/// public blob URLs). Centralizes the allow-list, size/count limits, and
/// localization keys so they are compile-time checked and single-sourced.
///
/// Same pattern as <see cref="VideoConstants"/>. Unlike video attachments
/// (private container + 15-minute SAS reads), these blobs live in a PUBLIC
/// container and the returned URL is permanent — see
/// <c>AzureBlobFileStorageService.UploadPublicAsync</c>.
/// </summary>
public static class UploadConstants
{
    /// <summary>Maximum size of a single uploaded file. 25 MB — raised from 10 MB
    /// (2026-08-31) so larger lecture PDFs are accepted; the client compresses
    /// oversized PDFs down to fit before upload. Kept within the 50 MB request
    /// cap on <see cref="Edvanz.API"/>'s UploadController.</summary>
    public const long MaxFileSizeBytes = 25 * 1024 * 1024;

    /// <summary>Maximum number of files accepted in one multipart request.</summary>
    public const int MaxFilesPerRequest = 10;

    /// <summary>
    /// Allowed content types: common raster images + PDF. SVG is deliberately
    /// excluded (it can embed scripts). The client-supplied ContentType is
    /// trusted here — the same trust boundary the video-attachment path uses
    /// (no magic-byte sniffing anywhere in the codebase).
    /// </summary>
    public static readonly string[] AllowedContentTypes =
    {
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "image/bmp",  "image/tiff", "image/heic", "image/heif",
        "application/pdf"
    };

    /// <summary>
    /// Localization message keys. Names match entries in <c>Messages.en.resx</c> /
    /// <c>Messages.ar.resx</c>. Service code references these constants — never
    /// inline string literals — so a typo is a build failure, not a runtime miss.
    /// </summary>
    public static class Messages
    {
        public const string NoFiles      = "UploadNoFiles";
        public const string TooManyFiles = "UploadTooManyFiles";
        public const string FileTooLarge = "UploadFileTooLarge";
        public const string InvalidType  = "UploadInvalidType";
        public const string InvalidUrl   = "UploadInvalidUrl";   // URL not a blob in our public container
        public const string NotOwned     = "UploadFileNotOwned"; // IDOR guard (403)
    }
}
