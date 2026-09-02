namespace Edvanz.Domain.Constants;

/// <summary>
/// Single-sourced constants for the PUBLIC parent portal (parent.edvanz.io → this API,
/// server-to-server). The header names are part of the wire contract with the PHP portal, so
/// they live here rather than as literals scattered across the filter, the controller and the
/// rate-limiter partitioner.
/// </summary>
public static class ParentPortalConstants
{
    /// <summary>Shared secret header. Must equal <c>ParentPortal:PortalKey</c> or the request is rejected 401.</summary>
    public const string PortalKeyHeader = "X-Portal-Key";

    /// <summary>Opaque per-browser device id minted by the portal. Hashed server-side; the raw value is never stored.</summary>
    public const string DeviceHeader = "X-Portal-Device";

    /// <summary>Real client IP forwarded by the PHP portal. Used ONLY for the hashed audit column and as a rate-limit fallback.</summary>
    public const string ClientIpHeader = "X-Portal-Client-IP";

    /// <summary>Name of the rate-limiter policy applied to the public portal routes.</summary>
    public const string RateLimitPolicy = "parent-portal";

    /// <summary>Route prefix of the public portal controller.</summary>
    public const string RouteBase = "api/parent-portal";

    /// <summary>Grant states as returned on the wire (lowercase, stable — the PHP/Flutter clients branch on these).</summary>
    public static class States
    {
        /// <summary>Approved: the device may read the student's shared data.</summary>
        public const string Active = "active";

        /// <summary>Waiting for the teacher. ALSO returned for a discarded request — see the security note in ParentPortalService.</summary>
        public const string Pending = "pending";

        /// <summary>The teacher rejected the request.</summary>
        public const string Rejected = "rejected";

        /// <summary>The teacher ended an approved grant.</summary>
        public const string Revoked = "revoked";

        /// <summary>No grant on this device (never requested, or the parent removed it themselves).</summary>
        public const string None = "none";

        /// <summary>The grant is fine but the teacher switched the portal off.</summary>
        public const string Disabled = "disabled";

        /// <summary>The grant is fine but the roster record is gone (deleted from the teacher's list).</summary>
        public const string StudentRemoved = "studentRemoved";
    }
}
