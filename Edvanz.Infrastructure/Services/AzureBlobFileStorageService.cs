using System.Text;
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
    /// <summary>Stem used when a filename strips to nothing in the ASCII fallback (an all-Arabic name does).</summary>
    private const string FallbackFileStem = "file";

    /// <summary>RFC 5987 prefers upper-case HEXDIG in a <c>pct-encoded</c> octet.</summary>
    private const string UpperHexDigits = "0123456789ABCDEF";

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
            sasBuilder.ContentDisposition = BuildContentDisposition(downloadFileName);

        return Task.FromResult(blobClient.GenerateSasUri(sasBuilder).ToString());
    }

    /// <summary>
    /// Builds the <c>Content-Disposition</c> that Azure echoes back to the client on a gated
    /// download (it travels in the SAS as <c>rscd</c>).
    ///
    /// The names are the teacher's own, and this app's primary language is Egyptian Arabic — exam
    /// papers and video attachments arrive called things like «امتحان الشهر الأول.pdf». An HTTP
    /// header parameter cannot carry those bytes: RFC 6266 confines the plain <c>filename</c>
    /// parameter to ISO-8859-1, so raw UTF-8 there is non-conformant, and Safari on iOS — which is
    /// how most of our students open a PDF — saves such a file under a mangled name or gives up and
    /// uses a generic one. RFC 5987's <c>filename*=UTF-8''…</c> is the form that carries them, but a
    /// client that predates it ignores <c>filename*</c> entirely. So BOTH are emitted: conformant
    /// clients prefer the extended parameter, everything else still reads the ASCII one.
    ///
    /// The extended parameter is added ONLY when the name actually needs it, so a name that is
    /// already printable ASCII produces the exact header this method produced before the extended
    /// form existed — no existing download changes behaviour.
    /// </summary>
    private static string BuildContentDisposition(string downloadFileName)
    {
        string disposition = $"attachment; filename=\"{ToAsciiFallbackName(downloadFileName)}\"";

        // Anything outside printable ASCII is exactly what the quoted form cannot carry.
        bool needsExtendedForm = false;
        foreach (char c in downloadFileName)
        {
            if (c is < ' ' or > '~')
            {
                needsExtendedForm = true;
                break;
            }
        }

        return needsExtendedForm
            ? $"{disposition}; filename*=UTF-8''{EncodeRfc5987ExtValue(downloadFileName)}"
            : disposition;
    }

    /// <summary>
    /// Derives the ISO-8859-1-safe name for the plain <c>filename</c> parameter. Characters it
    /// cannot hold are dropped rather than transliterated (a row of underscores is no more useful
    /// than a generic name), but the EXTENSION is carried across on its own: the operating system
    /// picks the viewer off it, so an exam paper that arrives without its <c>.pdf</c> opens in
    /// nothing. An Arabic name strips to an empty stem, which is why there is a generic one to fall
    /// back to — the real name still reaches the client through <c>filename*</c>.
    /// </summary>
    private static string ToAsciiFallbackName(string downloadFileName)
    {
        string extension = SanitizeToQuotableAscii(Path.GetExtension(downloadFileName));
        if (extension is "." or "")
            extension = string.Empty;

        string stem = SanitizeToQuotableAscii(Path.GetFileNameWithoutExtension(downloadFileName));
        if (stem.Length == 0)
            stem = FallbackFileStem;

        return stem + extension;
    }

    /// <summary>
    /// Keeps printable ASCII only, minus the characters RFC 6266's quoted-string cannot hold
    /// unescaped: a literal <c>"</c> becomes <c>'</c> (long-standing behaviour — it would otherwise
    /// terminate the header value, and an apostrophe keeps the name readable) and <c>\</c> is
    /// dropped, since it would read as the start of a quoted-pair. Runs of spaces left behind by
    /// dropped characters are collapsed so the result does not look truncated.
    /// </summary>
    private static string SanitizeToQuotableAscii(string segment)
    {
        var builder = new StringBuilder(segment.Length);

        foreach (char c in segment)
        {
            char kept = c switch
            {
                '"' => '\'',
                '\\' => '\0',
                < ' ' or > '~' => '\0',
                _ => c,
            };

            if (kept == '\0')
                continue;

            // Collapse the gaps a strip leaves: no leading space, never two in a row.
            if (kept == ' ' && (builder.Length == 0 || builder[^1] == ' '))
                continue;

            builder.Append(kept);
        }

        while (builder.Length > 0 && builder[^1] == ' ')
            builder.Length--;

        return builder.ToString();
    }

    /// <summary>
    /// Percent-encodes the UTF-8 bytes of <paramref name="value"/> as an RFC 5987 <c>ext-value</c>,
    /// leaving only <c>attr-char</c> (ALPHA / DIGIT / <c>! # $ &amp; + - . ^ _ ` | ~</c>) as-is.
    ///
    /// Spelled out rather than delegated to <see cref="Uri.EscapeDataString"/> on purpose: that
    /// method's pass-through set is RFC 3986's "unreserved", which is a DIFFERENT list that has
    /// already moved once across runtimes (.NET Framework left <c>! ' ( ) *</c> unescaped, .NET
    /// Core escapes them) — and <c>' ( ) *</c> are not <c>attr-char</c>, so borrowing it would make
    /// conformance depend on which runtime we happen to be on. Today's set
    /// (<c>A-Z a-z 0-9 - . _ ~</c>) is a strict subset of <c>attr-char</c> and would be correct;
    /// this is simply the rule the RFC states, stated once.
    /// </summary>
    private static string EncodeRfc5987ExtValue(string value)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(value);
        var builder = new StringBuilder(utf8.Length * 3);

        foreach (byte b in utf8)
        {
            if (IsRfc5987AttrChar(b))
            {
                builder.Append((char)b);
                continue;
            }

            builder.Append('%').Append(UpperHexDigits[b >> 4]).Append(UpperHexDigits[b & 0x0F]);
        }

        return builder.ToString();
    }

    /// <summary>RFC 5987 <c>attr-char</c> — the only bytes an <c>ext-value</c> may carry unencoded.</summary>
    private static bool IsRfc5987AttrChar(byte b) =>
        b is >= (byte)'0' and <= (byte)'9'
        or >= (byte)'A' and <= (byte)'Z'
        or >= (byte)'a' and <= (byte)'z'
        or (byte)'!' or (byte)'#' or (byte)'$' or (byte)'&' or (byte)'+' or (byte)'-'
        or (byte)'.' or (byte)'^' or (byte)'_' or (byte)'`' or (byte)'|' or (byte)'~';
}
