using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Central registry row for a single uploaded file — the industry-standard "media/attachment"
/// table (à la Rails ActiveStorage <c>Blob</c>). One row per uploaded file. Files are referenced
/// everywhere by the opaque <see cref="PublicId"/>, never by blob path, and every read is gated
/// through <c>GET /api/files/{PublicId}</c>, which authorizes from the ATTACHED resource
/// (via <see cref="Category"/>) before handing out a short-lived SAS URL.
///
/// Lifecycle (<see cref="Status"/>): Pending → Attached → Detached. All transitions happen inside
/// the owning resource's DB transaction; blob deletion is done exclusively by <c>FileObjectGcJob</c>
/// so a transient Azure failure never leaks a blob permanently.
///
/// PERSISTED CONTRACT: column types/lengths/FK behaviors are configured in
/// <c>EdvanzDbContext.OnModelCreating</c>. FKs are Fluent-only, NoAction (§4.1/§4.2).
/// </summary>
public class FileObject : BaseEntity
{
    /// <summary>
    /// Opaque, unguessable public identifier — the token in the gated URL
    /// (<c>/api/files/{PublicId}</c>). Distinct from the sequential <see cref="BaseEntity.Id"/>,
    /// which is never exposed. Unique index.
    /// </summary>
    public Guid PublicId { get; set; } = Guid.NewGuid();

    /// <summary>The user who uploaded the file (owner authorization rule).</summary>
    public long OwnerUserId { get; set; }

    /// <summary>
    /// Tenant scope: the teacher this file belongs to, resolved from the uploader's JWT
    /// (assistant → owning teacher). Null for files with no teacher tenant (e.g. a national-ID
    /// image whose owner is not yet a teacher).
    /// </summary>
    public long? TeacherId { get; set; }

    /// <summary>What the file is — selects the authorization policy.</summary>
    public FileCategory Category { get; set; }

    /// <summary>Lifecycle state — basis for the generic garbage collector.</summary>
    public FileStatus Status { get; set; } = FileStatus.Pending;

    /// <summary>
    /// Internal, non-guessable blob path within the private uploads container
    /// (<c>files/{teacherId}/{guid}</c>). Never returned to clients.
    /// </summary>
    public string BlobPath { get; set; } = null!;

    /// <summary>MIME type of the stored blob.</summary>
    public string ContentType { get; set; } = null!;

    /// <summary>Size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Original client filename (used as the download name for attachments).</summary>
    public string OriginalName { get; set; } = null!;

    /// <summary>
    /// Back-reference for the one one-to-many case — a video's attachments. Set when a
    /// <see cref="FileCategory.VideoAttachment"/> file is attached to a video; null for every
    /// single-valued category (those are referenced by an FK on the consuming entity instead).
    /// </summary>
    public long? VideoAssetId { get; set; }
}
