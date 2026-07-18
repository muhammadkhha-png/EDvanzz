using Edvanz.Application.Dtos;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// The single application service for the central file registry (<see cref="FileObject"/>):
/// gated reads, the attach/detach lifecycle, and gated-URL reconstruction. Authorization is
/// derived from the file's <see cref="FileCategory"/> and its attached resource — one place,
/// fail-closed by default.
/// </summary>
public interface IFileAccessService
{
    /// <summary>
    /// Gated read for <c>GET /api/files/{publicId}</c>. Resolves the caller from the JWT, authorizes
    /// (owner → SuperAdmin → category policy), and on success returns a fresh, time-limited SAS URL
    /// as the <c>Data</c> for the controller to 302-redirect to. Fails 404 (unknown file) / 403
    /// (not authorized).
    /// </summary>
    Task<Result<string>> TryGetReadUrlAsync(Guid publicId);

    /// <summary>
    /// Validates that the caller may attach the file identified by <paramref name="publicId"/> to a
    /// resource of category <paramref name="expectedCategory"/>, then flips it to
    /// <c>Attached</c> on the tracked entity (the caller sets its own FK and owns the commit — this
    /// does NOT call SaveChanges). Returns the tracked <see cref="FileObject"/> on success, or a
    /// failure: 404 <c>FileNotFound</c>, 403 <c>FileNotOwned</c>, 400 <c>FileCategoryMismatch</c>,
    /// 409 <c>FileAlreadyInUse</c> (already attached to a different resource).
    /// </summary>
    Task<Result<FileObject>> ResolveForAttachAsync(
        Guid publicId, FileCategory expectedCategory, long callerUserId, long? callerTeacherId);

    /// <summary>
    /// Marks a file <c>Detached</c> (and clears its <see cref="FileObject.VideoAssetId"/>) on the
    /// tracked entity so the GC job reaps its blob on the next sweep. No-op if the id is null or the
    /// row is already gone. Does NOT call SaveChanges — runs inside the caller's transaction.
    /// </summary>
    Task DetachAsync(long? fileObjectId);

    /// <summary>Reconstructs the stable gated URL (<c>{scheme}://{host}/api/files/{publicId}</c>).</summary>
    string BuildGatedUrl(Guid publicId);

    /// <summary>
    /// Read-side convenience: loads the registry row for <paramref name="fileObjectId"/> (an
    /// internal <c>FileObject.Id</c> FK held by a consuming entity) and returns its gated URL, or
    /// null when the id is null or the row is gone.
    /// </summary>
    Task<string?> TryBuildGatedUrlAsync(long? fileObjectId);

    /// <summary>
    /// Batched twin of <see cref="TryBuildGatedUrlAsync"/>: loads every registry row for the given
    /// internal <c>FileObject.Id</c> FKs in ONE query and returns a map from internal id to its
    /// gated URL — same URL format and same per-id null-handling as the single method (an id whose
    /// row is missing is simply absent from the map, so a <c>TryGetValue</c>/<c>GetValueOrDefault</c>
    /// lookup yields null exactly as the single method would). Removes the per-question N+1 in the
    /// quiz take/review screens. Authorization is unaffected — it is enforced later on the actual
    /// <c>GET /api/files/{id}</c> fetch, not during URL construction.
    /// </summary>
    Task<IReadOnlyDictionary<long, string?>> TryBuildGatedUrlsAsync(IEnumerable<long> fileInternalIds);
}
