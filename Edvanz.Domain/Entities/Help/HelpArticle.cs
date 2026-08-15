using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities.Help;

/// <summary>
/// A Help-Center reference article for a module (Layer 2) — the "refer back later"
/// content that explains a screen's buttons/labels/statuses. Composed of ordered
/// <see cref="Sections"/>, mirroring the existing bundled legal-doc shape
/// (assets/legal/terms_*.json → sections:[{heading, body}]).
/// </summary>
public class HelpArticle : BaseEntity
{
    public long HelpModuleId { get; set; }
    public HelpModule HelpModule { get; set; } = null!;

    /// <summary>Stable article key (e.g. "connect_vs_bind"); deep-link target for "?" affordances.</summary>
    public string Key { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public string TitleEn { get; set; } = null!;
    public string TitleAr { get; set; } = null!;

    // Navigation
    public ICollection<HelpArticleSection> Sections { get; set; } = new List<HelpArticleSection>();
}
