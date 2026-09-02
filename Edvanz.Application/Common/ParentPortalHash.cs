using System.Security.Cryptography;
using System.Text;

namespace Edvanz.Application.Common;

/// <summary>
/// One-way hashing for the public parent portal's opaque identifiers.
///
/// The portal's RAW device id and the caller IP are never persisted: a leaked database row must
/// not let anyone replay a parent's browser identity, and the IP is audit-only. The API stores
/// SHA-256 hex and compares hashes.
///
/// Deliberately UNSALTED and deterministic — the value has to be looked up by equality on every
/// read (<c>IX_PPA_DeviceHash</c>), and the input is a high-entropy GUID minted by the portal, so
/// a per-row salt would buy nothing and cost the index.
/// </summary>
public static class ParentPortalHash
{
    /// <summary>
    /// SHA-256 hex (64 lowercase chars) of the trimmed input, or null when the input is blank.
    /// Callers treat null as "no device identity" and fail the request.
    /// </summary>
    public static string? Compute(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw.Trim()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
