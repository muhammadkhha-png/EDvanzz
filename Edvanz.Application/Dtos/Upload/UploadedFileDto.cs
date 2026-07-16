namespace Edvanz.Application.Dtos.Upload;

/// <summary>
/// One uploaded file's public descriptor — the <c>data[]</c> item returned by the
/// upload and replace endpoints. These field names are the wire contract the
/// frontend consumes (url / originalName / size / mimeType).
/// </summary>
public sealed class UploadedFileDto
{
    /// <summary>Permanent, anonymously-readable blob URL.</summary>
    public string Url { get; set; } = default!;

    /// <summary>Original client filename (display only — never used in the blob path).</summary>
    public string OriginalName { get; set; } = default!;

    /// <summary>Size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>MIME / content type.</summary>
    public string MimeType { get; set; } = default!;
}
