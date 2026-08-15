using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities.Help;

/// <summary>
/// One step of a module's in-context coach-mark tour (Layer 1). The
/// <see cref="AnchorKey"/> is the contract between content and the GlobalKey the
/// Flutter screen registers on the real widget the step spotlights.
/// </summary>
public class HelpTourStep : BaseEntity
{
    public long HelpModuleId { get; set; }
    public HelpModule HelpModule { get; set; } = null!;

    /// <summary>Stable anchor id matching a registered GlobalKey on the target screen.</summary>
    public string AnchorKey { get; set; } = null!;

    /// <summary>Order of this step within the tour.</summary>
    public int DisplayOrder { get; set; }

    public string TitleEn { get; set; } = null!;
    public string TitleAr { get; set; } = null!;
    public string BodyEn { get; set; } = null!;
    public string BodyAr { get; set; } = null!;
}
