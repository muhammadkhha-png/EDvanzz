using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for the central file registry (<see cref="FileObject"/>). See
/// <see cref="IFileObjectRepo"/>.
/// </summary>
public sealed class FileObjectRepo : GenericRepo<FileObject, long>, IFileObjectRepo
{
    public FileObjectRepo(EdvanzDbContext context) : base(context) { }

    /// <inheritdoc />
    public async Task<FileObject?> GetByPublicIdAsync(Guid publicId, bool tracked = false)
    {
        var query = _context.Set<FileObject>().AsQueryable();
        if (!tracked)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(f => f.PublicId == publicId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileObject>> GetReclaimableAsync(DateTime pendingCutoffUtc)
    {
        return await _context.Set<FileObject>()
            .AsNoTracking()
            .Where(f => f.Status == FileStatus.Detached
                     || (f.Status == FileStatus.Pending && f.CreateAt < pendingCutoffUtc))
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileObject>> GetVideoAttachmentsAsync(long videoAssetId)
    {
        return await _context.Set<FileObject>()
            .AsNoTracking()
            .Where(f => f.VideoAssetId == videoAssetId
                     && f.Category == FileCategory.VideoAttachment
                     && f.Status == FileStatus.Attached)
            .OrderBy(f => f.CreateAt)
            .ToListAsync();
    }
}
