using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Options;
using Microsoft.Extensions.Options;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IFileStorageService"/>
/// (Track F — video attachments, §5). Registered in
/// <c>InfrastructureServiceExtensions.AddInfrastructure</c>. The Azure SDK
/// manages its own HTTP client internally — no <c>AddHttpClient</c> needed,
/// unlike <c>IWhatsAppSender</c>.
/// </summary>
public sealed class AzureBlobFileStorageService : IFileStorageService
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobFileStorageService(IOptions<AzureBlobStorageOptions> options)
    {
        var config = options.Value;
        var serviceClient = new BlobServiceClient(config.ConnectionString);
        _containerClient = serviceClient.GetBlobContainerClient(config.ContainerName);
    }

    /// <inheritdoc />
    public async Task<string> UploadAsync(string blobPath, Stream content, string contentType)
    {
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobClient = _containerClient.GetBlobClient(blobPath);
        await blobClient.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType });

        return blobPath;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string blobPath)
    {
        var blobClient = _containerClient.GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync();
    }

    /// <inheritdoc />
    public Task<string> GetReadUrlAsync(string blobPath, string? downloadFileName = null)
    {
        var blobClient = _containerClient.GetBlobClient(blobPath);

        if (!blobClient.CanGenerateSasUri)
            throw new InvalidOperationException(
                "AzureBlobStorage connection string must carry account-key credentials to generate SAS URLs.");

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(15),
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        if (downloadFileName is not null)
        {
            // Quotes escaped defensively — a filename containing a literal
            // `"` would otherwise break the header value.
            string safeName = downloadFileName.Replace("\"", "'");
            sasBuilder.ContentDisposition = $"attachment; filename=\"{safeName}\"";
        }

        return Task.FromResult(blobClient.GenerateSasUri(sasBuilder).ToString());
    }
}
