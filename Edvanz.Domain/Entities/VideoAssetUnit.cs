namespace Edvanz.Domain.Entities;

/// <summary>
/// Join row for the Video↔Unit M:N relationship (Module 14). A video can belong
/// to multiple units, and a unit can contain multiple videos; access to a video
/// is the union of its own <see cref="VideoAsset.Scopes"/> OR any linked unit's
/// <see cref="VideoUnit.Scopes"/>. Composite key <c>(VideoAssetId, UnitId)</c>.
/// No <c>[ForeignKey]</c> data annotations — the fluent API in
/// <c>EdvanzDbContext.OnModelCreating</c> is the sole source of truth for FK
/// behavior (EF Core 10 silently drops an explicit <c>OnDelete</c> when both
/// coexist on the same relationship).
/// </summary>
public class VideoAssetUnit
{
    public long VideoAssetId { get; set; }
    public VideoAsset VideoAsset { get; set; } = null!;

    public long UnitId { get; set; }
    public VideoUnit Unit { get; set; } = null!;
}
