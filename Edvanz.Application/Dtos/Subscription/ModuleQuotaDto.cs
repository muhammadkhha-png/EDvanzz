namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Output DTO for the admin module-quota endpoints (api/admin/module-quotas) — one row
/// per feature module's free-tier creation limit.
/// </summary>
public class ModuleQuotaDto
{
    /// <summary>Stable module identifier (see ModuleQuotaKeys) — the PUT route key.</summary>
    public string ModuleKey { get; set; } = string.Empty;

    /// <summary>Max items an unsubscribed teacher may create in this module. 0 = subscriber-only.</summary>
    public int FreeTierLimit { get; set; }

    /// <summary>Optional human-readable note.</summary>
    public string? Description { get; set; }

    /// <summary>When the limit was last changed via the admin endpoint; null if never edited.</summary>
    public DateTime? UpdatedAt { get; set; }
}
