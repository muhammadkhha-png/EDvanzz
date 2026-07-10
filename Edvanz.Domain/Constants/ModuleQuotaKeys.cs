namespace Edvanz.Domain.Constants;

/// <summary>
/// Stable keys for the per-module free-tier quotas stored in the ModuleQuota table.
/// Each key maps to one row; the create path for that module gates on it. Attendance and
/// payments are intentionally absent — they are free for everyone.
/// </summary>
public static class ModuleQuotaKeys
{
    public const string Students = "Students";
    public const string Sessions = "Sessions";
    public const string Assistants = "Assistants";
    public const string Groups = "Groups";
    public const string Videos = "Videos";
    public const string AssignmentTemplates = "AssignmentTemplates";
    public const string Events = "Events";
    public const string MessageTemplates = "MessageTemplates";
    public const string Triggers = "Triggers";

    /// <summary>All known keys with their seeded default free-tier limits (used by the migration seed).</summary>
    public static readonly IReadOnlyDictionary<string, int> Defaults = new Dictionary<string, int>
    {
        [Students] = 1,
        [Sessions] = 1,
        [Assistants] = 0,
        [Groups] = 0,
        [Videos] = 1,
        [AssignmentTemplates] = 1,
        [Events] = 1,
        [MessageTemplates] = 1,
        [Triggers] = 0,
    };
}
