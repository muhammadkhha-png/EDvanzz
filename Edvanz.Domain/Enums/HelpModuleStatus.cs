namespace Edvanz.Domain.Enums;

/// <summary>
/// Whether a module's real feature is wired to a live backend, or still a
/// mock/placeholder shell. Drives the client's "coming soon" ribbon and tour
/// suppression, and can be flipped from the backend the day a feature ships
/// (no app-store release). See the plan's "Mock / not-yet-wired modules" note.
/// </summary>
public enum HelpModuleStatus
{
    Live = 1,
    ComingSoon = 2,
}
