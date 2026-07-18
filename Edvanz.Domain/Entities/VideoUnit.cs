using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Named container that groups a teacher's videos (G-UNIT / Module 14
/// Track C).
///
/// DESIGN (boundary model): a <see cref="VideoUnit"/> carries its own Target
/// Scope (<see cref="Scopes"/>) that acts as the BOUNDARY for its videos — a
/// video's own <see cref="VideoScope"/> must stay within the union of its
/// units' scopes (enforced on every video-scope write). Unit scope is NOT a
/// separate grant: student visibility is decided purely by the video's own
/// <see cref="VideoScope"/> (which the boundary guarantees sits inside the
/// unit). See <see cref="VideoUnitScope"/> for the scope-row shape.
///
/// Relationship: Video↔Unit is M:N via <see cref="VideoAssetUnit"/>. Every
/// video must belong to at least one unit (enforced on create/update). A unit
/// cannot be deleted, nor its scope shrunk, while that would leave a member
/// video without a unit or targeting sessions the unit no longer covers (409);
/// otherwise the service layer removes the unit's <see cref="VideoAssetUnit"/>
/// rows on delete.
///
/// Soft-delete via <see cref="DeletedAt"/>, same convention as
/// <c>Teacher</c>/<c>StudentUser</c>/<c>ParentUser</c> — unlike
/// <see cref="VideoAsset"/>, a unit has no audit-snapshot requirement, so it
/// doesn't need the VCM hard-delete exception (REQ-VCM-BR-03 applies only to
/// VideoAsset and its direct children).
/// </summary>
public class VideoUnit : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher. All units are scoped to this teacher.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>Display name, e.g. "Mathematics - unit 1" (Figma S2 evidence).</summary>
    public string Title { get; set; } = null!;

    /// <summary>Optional teacher notes.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// The user (Teacher or Assistant) who created this unit. Same
    /// attribution rationale as <see cref="VideoAsset.CreatedByUserId"/>.
    /// </summary>
    public long? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    /// <summary>Soft-delete marker. Null = active. Query-filtered by default.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// M:N link rows to the videos currently assigned to this unit. See
    /// <see cref="VideoAsset.AssetUnits"/> remarks — a video can belong to multiple
    /// units, and a unit's videos are the other side of the same join table.
    /// </summary>
    public ICollection<VideoAssetUnit> AssetUnits { get; set; } = new List<VideoAssetUnit>();

    /// <summary>
    /// This unit's own Target Scope rows (the BOUNDARY for its videos).
    /// NoAction-deleted with the unit. These rows bound what a member video's
    /// own <see cref="VideoScope"/> may target; they do NOT themselves grant a
    /// student access (student visibility is decided by the video's scope).
    /// </summary>
    public ICollection<VideoUnitScope> Scopes { get; set; } = new List<VideoUnitScope>();
}
