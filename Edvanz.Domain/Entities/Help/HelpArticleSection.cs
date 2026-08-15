using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities.Help;

/// <summary>
/// One heading + body block of a <see cref="HelpArticle"/>. Body is long-form
/// (Markdown-lite) content, so it lives in a DB column rather than resx.
/// </summary>
public class HelpArticleSection : BaseEntity
{
    public long HelpArticleId { get; set; }
    public HelpArticle HelpArticle { get; set; } = null!;

    public int DisplayOrder { get; set; }

    /// <summary>Optional section heading (nullable — a section can be body-only).</summary>
    public string? HeadingEn { get; set; }
    public string? HeadingAr { get; set; }

    public string BodyEn { get; set; } = null!;
    public string BodyAr { get; set; } = null!;
}
