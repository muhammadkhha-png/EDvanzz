using System.Globalization;
using System.Net;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Help;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities.Help;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;

namespace Edvanz.Application.Services.Help;

/// <summary>
/// Resolves help/onboarding content to the request culture (EN vs Egyptian-AR) and
/// maps entities to the client DTO shape. All query logic lives in
/// <see cref="IHelpContentRepo"/> per the named-repo-method convention.
/// </summary>
public class HelpContentService : IHelpContentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;

    public HelpContentService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    /// <summary>True when the active request culture is Arabic (Egyptian).</summary>
    private static bool IsArabic =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("ar", StringComparison.OrdinalIgnoreCase);

    public async Task<Result<HelpManifestDto>> GetManifestAsync(string? persona)
    {
        if (!TryResolvePersona(persona, out var parsed, out var personaError))
            return Result<HelpManifestDto>.Failure(_localizer, personaError!, HttpStatusCode.BadRequest);

        var modules = await _unitOfWork.HelpContentRepo.GetModulesWithContentAsync(parsed);
        var faqs = await _unitOfWork.HelpContentRepo.GetFaqsAsync(parsed);

        var dto = new HelpManifestDto
        {
            Version = ComputeVersion(modules, faqs),
            Modules = modules.Select(MapModule).ToList(),
            Faqs = faqs.Select(MapFaq).ToList(),
        };

        return Result<HelpManifestDto>.Success(dto, _localizer, "Success");
    }

    public async Task<Result<List<HelpTourStepDto>>> GetTourAsync(string persona, string moduleKey)
    {
        if (!TryResolvePersona(persona, out var parsed, out var personaError) || parsed is null)
            return Result<List<HelpTourStepDto>>.Failure(_localizer, personaError ?? "InvalidPersona", HttpStatusCode.BadRequest);

        var steps = await _unitOfWork.HelpContentRepo.GetTourStepsAsync(parsed.Value, moduleKey);
        return Result<List<HelpTourStepDto>>.Success(steps.Select(MapStep).ToList(), _localizer, "Success");
    }

    public async Task<Result<List<HelpArticleDto>>> GetArticlesAsync(string persona, string moduleKey)
    {
        if (!TryResolvePersona(persona, out var parsed, out var personaError) || parsed is null)
            return Result<List<HelpArticleDto>>.Failure(_localizer, personaError ?? "InvalidPersona", HttpStatusCode.BadRequest);

        var articles = await _unitOfWork.HelpContentRepo.GetArticlesAsync(parsed.Value, moduleKey);
        return Result<List<HelpArticleDto>>.Success(articles.Select(MapArticle).ToList(), _localizer, "Success");
    }

    public async Task<Result<List<HelpFaqDto>>> GetFaqAsync(string? persona)
    {
        if (!TryResolvePersona(persona, out var parsed, out var personaError))
            return Result<List<HelpFaqDto>>.Failure(_localizer, personaError!, HttpStatusCode.BadRequest);

        var faqs = await _unitOfWork.HelpContentRepo.GetFaqsAsync(parsed);
        return Result<List<HelpFaqDto>>.Success(faqs.Select(MapFaq).ToList(), _localizer, "Success");
    }

    // ── Mapping ────────────────────────────────────────────────────────────────

    private HelpModuleDto MapModule(HelpModule m) => new()
    {
        Key = m.Key,
        Persona = PersonaToWire(m.Persona),
        Status = StatusToWire(m.Status),
        Order = m.DisplayOrder,
        Title = IsArabic ? m.TitleAr : m.TitleEn,
        Tour = m.Tour.OrderBy(s => s.DisplayOrder).Select(MapStep).ToList(),
        Articles = m.Articles.OrderBy(a => a.DisplayOrder).Select(MapArticle).ToList(),
    };

    private HelpTourStepDto MapStep(HelpTourStep s) => new()
    {
        AnchorKey = s.AnchorKey,
        Order = s.DisplayOrder,
        Title = IsArabic ? s.TitleAr : s.TitleEn,
        Body = IsArabic ? s.BodyAr : s.BodyEn,
    };

    private HelpArticleDto MapArticle(HelpArticle a) => new()
    {
        Key = a.Key,
        Order = a.DisplayOrder,
        Title = IsArabic ? a.TitleAr : a.TitleEn,
        Sections = a.Sections.OrderBy(s => s.DisplayOrder).Select(MapSection).ToList(),
    };

    private HelpSectionDto MapSection(HelpArticleSection s) => new()
    {
        Heading = IsArabic ? s.HeadingAr : s.HeadingEn,
        Body = IsArabic ? s.BodyAr : s.BodyEn,
    };

    private HelpFaqDto MapFaq(HelpFaqItem f) => new()
    {
        Persona = PersonaToWire(f.Persona),
        ModuleKey = f.ModuleKey,
        Order = f.DisplayOrder,
        Question = IsArabic ? f.QuestionAr : f.QuestionEn,
        Answer = IsArabic ? f.AnswerAr : f.AnswerEn,
    };

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the wire persona ("teacher"/"student"/"assistant"). Null/empty → all
    /// personas (parsed = null, ok = true). An unrecognized value → ok = false.
    /// </summary>
    private static bool TryResolvePersona(string? persona, out HelpPersona? parsed, out string? error)
    {
        parsed = null;
        error = null;
        if (string.IsNullOrWhiteSpace(persona))
            return true;

        switch (persona.Trim().ToLowerInvariant())
        {
            case "teacher": parsed = HelpPersona.Teacher; return true;
            case "student": parsed = HelpPersona.Student; return true;
            case "assistant": parsed = HelpPersona.Assistant; return true;
            default: error = "InvalidPersona"; return false;
        }
    }

    private static string PersonaToWire(HelpPersona persona) => persona switch
    {
        HelpPersona.Teacher => "teacher",
        HelpPersona.Student => "student",
        HelpPersona.Assistant => "assistant",
        _ => "teacher",
    };

    private static string StatusToWire(HelpModuleStatus status) => status switch
    {
        HelpModuleStatus.Live => "live",
        HelpModuleStatus.ComingSoon => "coming_soon",
        _ => "live",
    };

    /// <summary>
    /// Deterministic version stamp from the latest CreateAt across content, so a
    /// client can cheaply detect changes. (Seed content is authored with an explicit
    /// CreateAt; future edits should bump it.)
    /// </summary>
    private static string ComputeVersion(
        IReadOnlyList<HelpModule> modules, IReadOnlyList<HelpFaqItem> faqs)
    {
        var latest = DateTime.MinValue;
        foreach (var m in modules) if (m.CreateAt > latest) latest = m.CreateAt;
        foreach (var f in faqs) if (f.CreateAt > latest) latest = f.CreateAt;
        return latest == DateTime.MinValue
            ? "0"
            : latest.ToUniversalTime().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
    }
}
