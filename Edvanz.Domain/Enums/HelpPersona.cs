namespace Edvanz.Domain.Enums;

/// <summary>
/// Audience a piece of help/onboarding content targets. Distinct from
/// <see cref="Edvanz.Domain.Enums"/> user roles — help content is authored per
/// app persona (Parent is intentionally out of scope for the onboarding system).
/// </summary>
public enum HelpPersona
{
    Teacher = 1,
    Student = 2,
    Assistant = 3,
}
