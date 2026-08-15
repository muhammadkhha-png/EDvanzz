using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Help;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Serves interactive-onboarding + Help-Center + FAQ content, resolved to the
/// request's language (EN or Egyptian-AR via the Accept-Language provider). Backs
/// the anonymous <c>api/help</c> endpoints. Content is SuperAdmin-managed reference
/// data — these are read-only queries.
/// </summary>
public interface IHelpContentService
{
    /// <summary>
    /// Full payload (modules + tours + articles + faqs) for a persona, or all
    /// personas when <paramref name="persona"/> is null. Non-null invalid persona → 400.
    /// </summary>
    Task<Result<HelpManifestDto>> GetManifestAsync(string? persona);

    /// <summary>Ordered coach-mark tour steps for one persona's module (Layer 1).</summary>
    Task<Result<List<HelpTourStepDto>>> GetTourAsync(string persona, string moduleKey);

    /// <summary>Ordered Help-Center articles (with sections) for one persona's module (Layer 2).</summary>
    Task<Result<List<HelpArticleDto>>> GetArticlesAsync(string persona, string moduleKey);

    /// <summary>FAQ list for a persona, or all personas when <paramref name="persona"/> is null.</summary>
    Task<Result<List<HelpFaqDto>>> GetFaqAsync(string? persona);
}
