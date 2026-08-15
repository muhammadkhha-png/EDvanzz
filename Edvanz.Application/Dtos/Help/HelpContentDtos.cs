namespace Edvanz.Application.Dtos.Help;

/// <summary>
/// Full help/onboarding payload for one persona, already resolved to the requested
/// language (EN or Egyptian-AR). Shared shape with the bundled asset fallback.
/// </summary>
public class HelpManifestDto
{
    /// <summary>Content version stamp for client cache invalidation.</summary>
    public string Version { get; set; } = string.Empty;

    public List<HelpModuleDto> Modules { get; set; } = new();
    public List<HelpFaqDto> Faqs { get; set; } = new();
}

public class HelpModuleDto
{
    public string Key { get; set; } = string.Empty;

    /// <summary>"teacher" | "student" | "assistant".</summary>
    public string Persona { get; set; } = string.Empty;

    /// <summary>"live" | "coming_soon" — drives the client ribbon + tour suppression.</summary>
    public string Status { get; set; } = string.Empty;

    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;

    public List<HelpTourStepDto> Tour { get; set; } = new();
    public List<HelpArticleDto> Articles { get; set; } = new();
}

public class HelpTourStepDto
{
    /// <summary>Matches a GlobalKey registered on the target screen.</summary>
    public string AnchorKey { get; set; } = string.Empty;

    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class HelpArticleDto
{
    public string Key { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<HelpSectionDto> Sections { get; set; } = new();
}

public class HelpSectionDto
{
    public string? Heading { get; set; }
    public string Body { get; set; } = string.Empty;
}

public class HelpFaqDto
{
    public string Persona { get; set; } = string.Empty;
    public string? ModuleKey { get; set; }
    public int Order { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}
