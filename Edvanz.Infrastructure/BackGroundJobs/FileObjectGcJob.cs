using Edvanz.Application.IservicesContract;
using Edvanz.Application.Options;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Edvanz.Infrastructure.BackGroundJobs;

/// <summary>
/// Hourly garbage collector for the central file registry (<see cref="Edvanz.Domain.Entities.FileObject"/>).
/// The single, reliable reaper of blob storage: it removes the blob + registry row for
/// <list type="bullet">
///   <item><c>Pending</c> files older than the configured grace window — abandoned uploads that
///         were never attached to a resource;</item>
///   <item>every <c>Detached</c> file — released by a replace or a resource delete.</item>
/// </list>
///
/// This is why the update/delete paths never delete blobs inline: a blob cannot join the DB
/// transaction, so doing it here (idempotent, retried each sweep) means a transient Azure failure
/// never leaks a blob permanently. Registered as an hourly recurring job in Program.cs.
/// </summary>
public class FileObjectGcJob
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly AzureBlobStorageOptions _options;
    private readonly ILogger<FileObjectGcJob> _logger;

    public FileObjectGcJob(
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        IOptions<AzureBlobStorageOptions> options,
        ILogger<FileObjectGcJob> logger)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Hangfire entry point. Registered as an hourly recurring job in Program.cs.</summary>
    public async Task RunAsync()
    {
        DateTime pendingCutoffUtc = DateTime.UtcNow.AddHours(-_options.UploadsPendingGraceHours);

        var reclaimable = await _unitOfWork.FileObjectsRepo.GetReclaimableAsync(pendingCutoffUtc);
        if (reclaimable.Count == 0)
            return;

        int reaped = 0;
        foreach (var file in reclaimable)
        {
            // Delete the blob FIRST. If it fails we keep the row so the next sweep retries —
            // deleting the row before the blob would orphan the blob permanently.
            try
            {
                await _fileStorage.DeleteAsync(file.BlobPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "FileObjectGcJob: failed to delete blob '{BlobPath}' for FileObject {Id} — will retry next sweep.",
                    file.BlobPath, file.Id);
                continue;
            }

            await _unitOfWork.FileObjectsRepo.DeleteAsync(file);
            reaped++;
        }

        if (reaped > 0)
        {
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("FileObjectGcJob: reclaimed {Count} file objects (blob + row).", reaped);
        }
    }
}
