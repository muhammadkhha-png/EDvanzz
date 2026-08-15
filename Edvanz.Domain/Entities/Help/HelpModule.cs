using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Entities.Help;

/// <summary>
/// One app module's onboarding + help content bucket (e.g. "student_links",
/// "payments"). SuperAdmin-managed reference data — global, not tenant-scoped.
/// Bilingual as paired columns (EN + Egyptian-AR), mirroring the <see cref="Subject"/>
/// lookup convention. A module owns an ordered guided <see cref="Tour"/> (Layer 1)
/// and a set of Help-Center <see cref="Articles"/> (Layer 2).
/// </summary>
public class HelpModule : BaseEntity
{
    /// <summary>Stable, code-referenced key (e.g. "student_links"). Unique. Never localized.</summary>
    public string Key { get; set; } = null!;

    /// <summary>Which persona this module's content is authored for.</summary>
    public HelpPersona Persona { get; set; }

    /// <summary>Live vs coming-soon; suppresses tours + shows a ribbon on the client.</summary>
    public HelpModuleStatus Status { get; set; } = HelpModuleStatus.Live;

    /// <summary>Display order within the persona's Help-Center module list.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Module display title — English.</summary>
    public string TitleEn { get; set; } = null!;

    /// <summary>Module display title — Egyptian Arabic.</summary>
    public string TitleAr { get; set; } = null!;

    /// <summary>Soft-hide flag; inactive modules are excluded from all reads.</summary>
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<HelpTourStep> Tour { get; set; } = new List<HelpTourStep>();
    public ICollection<HelpArticle> Articles { get; set; } = new List<HelpArticle>();
}
