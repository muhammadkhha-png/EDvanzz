using Edvanz.Domain.Entities;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Repository for the central file registry (<see cref="FileObject"/>). Inherits generic CRUD over
/// <see cref="FileObject"/> and adds the named queries the gated file endpoint, the attach/detach
/// lifecycle, and the garbage-collector job need. No raw predicates cross into the Application
/// layer (§3.1).
/// </summary>
public interface IFileObjectRepo : IGenericRepo<FileObject, long>
{
    /// <summary>
    /// Loads a registry row by its opaque public id. <paramref name="tracked"/> = true returns a
    /// change-tracked entity for mutation (attach/detach); false is an AsNoTracking read.
    /// </summary>
    Task<FileObject?> GetByPublicIdAsync(Guid publicId, bool tracked = false);

    /// <summary>
    /// Rows the garbage collector may reclaim: every <c>Detached</c> row plus every <c>Pending</c>
    /// row created before <paramref name="pendingCutoffUtc"/> (abandoned uploads past the grace
    /// window). AsNoTracking — the job deletes the blob then hard-deletes the row.
    /// </summary>
    Task<IReadOnlyList<FileObject>> GetReclaimableAsync(DateTime pendingCutoffUtc);

    /// <summary>
    /// The live attachment files for a video — <c>Attached</c> <see cref="FileObject"/>s of category
    /// <see cref="Edvanz.Domain.Enums.FileCategory.VideoAttachment"/> back-referencing this video.
    /// AsNoTracking; ordered oldest-first.
    /// </summary>
    Task<IReadOnlyList<FileObject>> GetVideoAttachmentsAsync(long videoAssetId);

    /// <summary>
    /// Batch lookup by internal ids — one query for a whole page of rows (e.g. resolving the
    /// student video list's photo <c>FileObject.Id</c>s to <c>PublicId</c>s without an N+1).
    /// AsNoTracking.
    /// </summary>
    Task<IReadOnlyList<FileObject>> GetByIdsAsync(IReadOnlyCollection<long> ids);

    /// <summary>
    /// The live (<c>Attached</c>) attachment files for MANY videos in one query — the batch
    /// counterpart of <see cref="GetVideoAttachmentsAsync"/>, used by the student video list.
    /// AsNoTracking; ordered oldest-first within each video.
    /// </summary>
    Task<IReadOnlyList<FileObject>> GetVideoAttachmentsForVideosAsync(IReadOnlyCollection<long> videoAssetIds);
}
