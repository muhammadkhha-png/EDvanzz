using Edvanz.Domain.Entities.Help;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Read access to the Help / Onboarding content lookup. All query logic (persona
/// filtering, active filtering, child includes, ordering) is encapsulated here per
/// the named-repo-method convention — services never build LINQ predicates.
/// </summary>
public interface IHelpContentRepo : IGenericRepo<HelpModule, long>
{
    /// <summary>
    /// Active modules for a persona (or all personas when null), each with its
    /// ordered tour steps and articles (+ sections) eager-loaded. Ordered by DisplayOrder.
    /// </summary>
    Task<IReadOnlyList<HelpModule>> GetModulesWithContentAsync(HelpPersona? persona);

    /// <summary>Ordered tour steps for one persona's module key (empty if none / inactive).</summary>
    Task<IReadOnlyList<HelpTourStep>> GetTourStepsAsync(HelpPersona persona, string moduleKey);

    /// <summary>Ordered articles (with sections) for one persona's module key.</summary>
    Task<IReadOnlyList<HelpArticle>> GetArticlesAsync(HelpPersona persona, string moduleKey);

    /// <summary>Active FAQ items for a persona (or all personas when null), ordered.</summary>
    Task<IReadOnlyList<HelpFaqItem>> GetFaqsAsync(HelpPersona? persona);
}
