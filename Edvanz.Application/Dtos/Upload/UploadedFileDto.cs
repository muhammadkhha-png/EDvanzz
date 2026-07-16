namespace Edvanz.Application.Dtos.Upload;

/// <summary>
/// One uploaded file's descriptor — the <c>data[]</c> item returned by the upload and replace
/// endpoints. The client stores <see cref="FileId"/> and sends it back when creating/updating a
/// resource; <see cref="Url"/> is the stable gated URL it can embed directly. These field names are
/// the wire contract the frontend consumes.
/// </summary>
public sealed class UploadedFileDto
{
    /// <summary>Opaque registry id (FileObject.PublicId). Sent back to attach the file to a resource.</summary>
    public Guid FileId { get; set; }

    /// <summary>Stable gated URL (<c>/api/files/{fileId}</c>) — embeddable; re-checks access per fetch.</summary>
    public string Url { get; set; } = default!;

    /// <summary>Original client filename (display only — never used in the blob path).</summary>
    public string OriginalName { get; set; } = default!;

    /// <summary>Size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>MIME / content type.</summary>
    public string MimeType { get; set; } = default!;
}
