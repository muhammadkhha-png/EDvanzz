using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities;

/// <summary>
/// An optional exam attached to a video at creation time. One video has at
/// most one exam, created atomically alongside the video row. No standalone
/// GET/PUT/DELETE endpoints exist for exams yet — intentionally out of scope
/// for this pass; the exam is created once, at video-creation time, and not
/// currently editable afterward.
///
/// FK POSTURE: (VideoAssetId, TeacherId) → VideoAssets(Id, TeacherId) is
/// CASCADE — unlike VideoScope/VideoAnalytics's NoAction-plus-audit-snapshot
/// posture. Exams have no forensic-snapshot requirement, so a video
/// hard-delete simply removes the exam and all its questions/options at the
/// DB level with zero service-layer cleanup code needed.
/// </summary>
public class VideoExam : BaseEntity
{
    public long VideoAssetId { get; set; }
    public VideoAsset VideoAsset { get; set; } = null!;

    /// <summary>Tenant scope, denormalized (VCM convention) — see FK posture above.</summary>
    public long TeacherId { get; set; }

    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>The user (Teacher or Assistant) who created this exam.</summary>
    public long? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public ICollection<VideoExamQuestion> Questions { get; set; } = new List<VideoExamQuestion>();
}