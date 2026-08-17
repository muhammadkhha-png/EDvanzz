namespace Edvanz.Domain.Constants;

public static class UserRoles
{
    public const string User = "User";
    public const string Teacher = "Teacher";
    public const string Assistant = "Assistant";
    public const string Student = "Student";
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>
    /// The Center login. Owns several Teachers and "acts as" any of them via the
    /// <c>X-Acting-Teacher-Id</c> header (validated against center membership).
    /// Value must equal <c>UserType.Center.ToString()</c> — the JWT Role claim source.
    /// </summary>
    public const string Center = "Center";

    /// <summary>
    /// An assistant operating under a Center, spanning all of the center's teachers.
    /// Value must equal <c>UserType.CenterAssistant.ToString()</c>.
    /// </summary>
    public const string CenterAssistant = "CenterAssistant";
}
