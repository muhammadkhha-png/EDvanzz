using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Options;
using Microsoft.Extensions.Options;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IFileStorageService"/>. All files live in a
/// single PRIVATE container (<see cref="AzureBlobStorageOptions.UploadsContainerName"/>); reads are
/// handed out as time-limited SAS URLs only after the gated endpoint authorizes the caller. There
/// is no public/anonymous access. Registered in
/// <c>InfrastructureServiceExtensions.AddInfrastructure</c>.
/// </summary>
public sealed class AzureBlobFileStorageService : IFileStorageService
{
    private readonly BlobContainerClient _containerClient;
    private readonly int _defaultSasLifetimeMinutes;

    public AzureBlobFileStorageService(IOptions<AzureBlobStorageOptions> options)
    {
        var config = options.Value;
        var serviceClient = new BlobServiceClient(config.ConnectionString);
        _containerClient = serviceClient.GetBlobContainerClient(config.UploadsContainerName);
        _defaultSasLifetimeMinutes = config.UploadsSasLifetimeMinutes;
    }

    /// <inheritdoc />
    public async Task<string> UploadAsync(string blobPath, Stream content, string contentType)
    {
        // Private container — CreateIfNotExists is a no-op once it exists; PublicAccessType.None
        // guarantees anonymous reads never work even if the blob URL leaks.
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobClient = _containerClient.GetBlobClient(blobPath);
        await blobClient.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType });

        return blobPath;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string blobPath) =>
        await _containerClient.GetBlobClient(blobPath).DeleteIfExistsAsync();

    /// <inheritdoc />
    public Task<string> GetReadUrlAsync(
        string blobPath, string? downloadFileName = null, int? lifetimeMinutes = null)
    {
        var blobClient = _containerClient.GetBlobClient(blobPath);

        if (!blobClient.CanGenerateSasUri)
            throw new InvalidOperationException(
                "AzureBlobStorage connection string must carry account-key credentials to generate SAS URLs.");

        int lifetime = lifetimeMinutes ?? _defaultSasLifetimeMinutes;

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(lifetime),
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        if (downloadFileName is not null)
        {
            // Quotes escaped defensively — a filename containing a literal `"` would otherwise
            // break the header value.
            string safeName = downloadFileName.Replace("\"", "'");
            sasBuilder.ContentDisposition = $"attachment; filename=\"{safeName}\"";
        }

        return Task.FromResult(blobClient.GenerateSasUri(sasBuilder).ToString());
    }
}
