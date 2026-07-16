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
    private readonly BlobContainerClient _publicContainerClient;

    public AzureBlobFileStorageService(IOptions<AzureBlobStorageOptions> options)
    {
        var config = options.Value;
        var serviceClient = new BlobServiceClient(config.ConnectionString);
        _containerClient = serviceClient.GetBlobContainerClient(config.ContainerName);
        _publicContainerClient = serviceClient.GetBlobContainerClient(config.PublicContainerName);
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

    /// <inheritdoc />
    public async Task<string> UploadPublicAsync(string blobPath, Stream content, string contentType)
    {
        // Public-read container so the returned URL is permanent and directly
        // embeddable. CreateIfNotExists is a no-op when the container already
        // exists (e.g. pre-created public via the Azure CLI). Requires the
        // storage account to permit anonymous blob access.
        await _publicContainerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var blobClient = _publicContainerClient.GetBlobClient(blobPath);
        await blobClient.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType,                             // so <img>/PDF render correctly
                CacheControl = "public, max-age=31536000, immutable",  // objects are immutable — new URL on change
            },
        });

        return blobClient.Uri.ToString();
    }

    /// <inheritdoc />
    public async Task DeletePublicAsync(string blobPath) =>
        await _publicContainerClient.GetBlobClient(blobPath).DeleteIfExistsAsync();

    /// <inheritdoc />
    public string? ResolvePublicBlobPath(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            var builder = new BlobUriBuilder(new Uri(url));

            bool belongsToPublicContainer =
                string.Equals(builder.Host, _publicContainerClient.Uri.Host, StringComparison.OrdinalIgnoreCase)
                && string.Equals(builder.BlobContainerName, _publicContainerClient.Name, StringComparison.Ordinal)
                && !string.IsNullOrEmpty(builder.BlobName);

            return belongsToPublicContainer ? builder.BlobName : null;
        }
        catch
        {
            // Malformed URL, wrong scheme, not a blob URL — treat as "not ours".
            return null;
        }
    }
}
