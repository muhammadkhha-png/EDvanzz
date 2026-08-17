namespace Edvanz.Domain.Constants;

/// <summary>
/// Constants for the Center tenancy tier.
/// </summary>
public static class CenterConstants
{
    /// <summary>
    /// Request header a Center (or CenterAssistant) login sends to select WHICH of its teachers
    /// it is currently operating as. The value is a <c>Teacher.Id</c>; it is ALWAYS validated
    /// against the caller's center membership (ICenterRepo.IsTeacherInCenterAsync, fail-closed)
    /// before being used as the tenant — never trusted blindly (honors CLAUDE.md §3.3 / BUG-12).
    /// </summary>
    public const string ActingTeacherHeader = "X-Acting-Teacher-Id";

    // ── Free tier (a center with NO active subscription) ──
    // Mirrors the teacher free-tier philosophy: a center without a subscription isn't unlimited — it
    // gets a small trial. Teacher SLOTS are capped here; each created teacher's STUDENTS then fall
    // under the normal per-teacher free-tier limit automatically (they have no active subscription).

    /// <summary>Full-plan teacher slots a center may create with NO active subscription (trial).</summary>
    public const int FreeTierFullTeacherSlots = 1;

    /// <summary>Managerial-plan teacher slots a center may create with NO active subscription.</summary>
    public const int FreeTierManagerialTeacherSlots = 0;
}
