namespace Edvanz.Application.IservicesContract
{
    /// <summary>
    /// Per-request channel that lets the action/service that knows which record was
    /// touched declare a human-readable label for it (e.g. "Student 'Ahmed Mostafa' (#42)").
    /// Consumed by AuditTrailAttribute when it writes the audit row, satisfying
    /// REQ-USR-029: the audit trail names the specific record affected, not just the
    /// endpoint that was hit.
    ///
    /// The label is opt-in per endpoint. When none is set, the attribute falls back to a
    /// route-id-enriched endpoint name, so un-migrated endpoints degrade gracefully.
    ///
    /// Backed by HttpContext.Items — the same per-request hand-off the
    /// SecurityStampValidationMiddleware → PermissionHandler pipeline already uses.
    /// </summary>
    public interface IAuditContext
    {
        /// <summary>
        /// Records the human-readable identity of the record affected by the current
        /// request. Last write wins. No-op when there is no active HttpContext
        /// (e.g. background jobs), so it is always safe to call.
        /// </summary>
        void SetAffectedRecord(string label);

        /// <summary>The label set during this request, or <c>null</c> if none was supplied.</summary>
        string? AffectedRecord { get; }
    }
}