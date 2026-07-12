using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Named container that groups a teacher's videos (G-UNIT / Module 14
/// Track C).
///
/// FINAL DECISION (supersedes the earlier "purely organizational" design):
/// a <see cref="VideoUnit"/> carries its own Target Scope
/// (<see cref="Scopes"/>), in addition to each video's own
/// <see cref="VideoScope"/>. A student is authorized to access a video the
/// moment EITHER scope resolves them. See
/// <c>IVideoAssetRepo.IsStudentInVideoScopeAsync</c> for the union check and
/// <see cref="VideoUnitScope"/> for the scope-row shape.
///
/// Optional relationship: Video↔Unit is M:N via <see cref="VideoAssetUnit"/> —
/// "loose" videos with no unit link rows are valid; their access is governed
/// solely by their own <see cref="VideoScope"/> rows (no unit scope applies).
/// Deleting a unit does not delete or orphan its videos; the service layer
/// removes the unit's <see cref="VideoAssetUnit"/> rows, so videos simply
/// become loose again for that unit — and, once loose, stop being covered by
/// the (now-deleted) unit's scope entirely.
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
    /// This unit's own Target Scope rows. NoAction-deleted with the unit.
    /// A student authorized by ANY of these rows gets access to EVERY video
    /// in this unit, independent of each video's own <see cref="VideoScope"/>.
    /// </summary>
    public ICollection<VideoUnitScope> Scopes { get; set; } = new List<VideoUnitScope>();
}
