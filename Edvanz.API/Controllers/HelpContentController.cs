using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Help;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Interactive-onboarding + Help-Center + FAQ content (Layers 1–2 of the onboarding
/// system). SuperAdmin-managed reference data, resolved to the request language
/// (EN or Egyptian-AR via Accept-Language).
///
/// AUTHORIZATION: [AllowAnonymous] — tours/help/FAQ must be reachable pre-login
/// (the first-launch onboarding path runs before auth). Content carries no
/// tenant/PII, so anonymous read is safe.
/// </summary>
[ApiController]
[Route("api/help")]
[AllowAnonymous]
public class HelpContentController : ApiBaseController
{
    private readonly IHelpContentService _helpContent;

    public HelpContentController(IHelpContentService helpContent)
    {
        _helpContent = helpContent;
    }

    /// <summary>
    /// Full payload (modules + tours + articles + faqs) for a persona, or all
    /// personas when <paramref name="persona"/> is omitted.
    /// SAMPLE: GET /api/help/manifest?persona=teacher
    /// </summary>
    [HttpGet("manifest")]
    [ProducesResponseType(typeof(Result<HelpManifestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetManifest([FromQuery] string? persona = null)
        => ToResponse(await _helpContent.GetManifestAsync(persona));

    /// <summary>
    /// Ordered coach-mark tour steps for one persona's module (Layer 1).
    /// SAMPLE: GET /api/help/tours/student_links?persona=teacher
    /// </summary>
    [HttpGet("tours/{moduleKey}")]
    [ProducesResponseType(typeof(Result<List<HelpTourStepDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTour([FromRoute] string moduleKey, [FromQuery] string persona)
        => ToResponse(await _helpContent.GetTourAsync(persona, moduleKey));

    /// <summary>
    /// Ordered Help-Center articles (with sections) for one persona's module (Layer 2).
    /// SAMPLE: GET /api/help/articles/student_links?persona=teacher
    /// </summary>
    [HttpGet("articles/{moduleKey}")]
    [ProducesResponseType(typeof(Result<List<HelpArticleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetArticles([FromRoute] string moduleKey, [FromQuery] string persona)
        => ToResponse(await _helpContent.GetArticlesAsync(persona, moduleKey));

    /// <summary>
    /// FAQ list for a persona, or all personas when <paramref name="persona"/> is omitted.
    /// SAMPLE: GET /api/help/faq?persona=student
    /// </summary>
    [HttpGet("faq")]
    [ProducesResponseType(typeof(Result<List<HelpFaqDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFaq([FromQuery] string? persona = null)
        => ToResponse(await _helpContent.GetFaqAsync(persona));
}
