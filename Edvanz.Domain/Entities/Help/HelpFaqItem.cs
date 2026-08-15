using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Entities.Help;

/// <summary>
/// One FAQ entry (question + answer), scoped to a persona. Bilingual paired
/// columns (EN + Egyptian-AR). Optionally associated with a module via
/// <see cref="ModuleKey"/> for deep-linking, but FAQs are primarily browsed as a
/// flat, searchable per-persona list.
/// </summary>
public class HelpFaqItem : BaseEntity
{
    public HelpPersona Persona { get; set; }

    /// <summary>Optional owning module key (e.g. "student_links"); null = general FAQ.</summary>
    public string? ModuleKey { get; set; }

    public int DisplayOrder { get; set; }

    public string QuestionEn { get; set; } = null!;
    public string QuestionAr { get; set; } = null!;
    public string AnswerEn { get; set; } = null!;
    public string AnswerAr { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}
