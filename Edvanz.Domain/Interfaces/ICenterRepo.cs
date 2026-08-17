using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

/// <summary>A roster student matched by code within a center, carrying its owning teacher so the
/// front desk can disambiguate a shared code (e.g. two teachers both using "100D").</summary>
public class CenterStudentCodeMatch
{
    public long TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string TeacherCode { get; set; } = string.Empty;
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public string? StudentPhoneNumber { get; set; }
    /// <summary>The student's currently-assigned session (null if unassigned) — lets a front-desk
    /// scan jump straight into that session's take-attendance form.</summary>
    public long? SessionId { get; set; }
    public string? SessionName { get; set; }
}

/// <summary>
/// Data access for the Center tenancy tier: the center account, its login/assistant resolution,
/// membership checks used by the "acting-as" resolution seam, quota-enforcement counts, and the
/// current-subscription lookup. All center reads/writes go through here (no raw LINQ in services).
/// </summary>
public interface ICenterRepo : IGenericRepo<Center, long>
{
    /// <summary>Loads the center owned by a login user (role Center), or null. Excludes soft-deleted.</summary>
    Task<Center?> GetCenterByUserIdAsync(long userId);

    /// <summary>Loads a center assistant by login user id (role CenterAssistant), or null. Excludes soft-deleted.</summary>
    Task<CenterAssistant?> GetCenterAssistantByUserIdAsync(long userId);

    /// <summary>Non-deleted center assistants for a center, each with its User loaded (for lists).</summary>
    Task<IReadOnlyList<CenterAssistant>> GetCenterAssistantsByCenterAsync(long centerId);

    /// <summary>A center assistant by its id (with User), or null. Excludes soft-deleted.</summary>
    Task<CenterAssistant?> GetCenterAssistantByIdAsync(long centerAssistantId);

    /// <summary>Loads a center by id (excludes soft-deleted), or null.</summary>
    Task<Center?> GetCenterByIdAsync(long centerId);

    /// <summary>
    /// True when <paramref name="teacherId"/> is a non-deleted teacher owned by
    /// <paramref name="centerId"/> (ANY account status). Membership guard for MANAGEMENT
    /// (edit/deactivate/reactivate a teacher).
    /// </summary>
    Task<bool> IsTeacherInCenterAsync(long centerId, long teacherId);

    /// <summary>
    /// True when the teacher is owned by the center AND is not deactivated. The fail-closed guard
    /// behind ACTING-AS resolution — a center cannot operate a teacher it has deactivated.
    /// </summary>
    Task<bool> IsActiveTeacherInCenterAsync(long centerId, long teacherId);

    /// <summary>True if the given 8-digit center code already exists (uniqueness pre-check).</summary>
    Task<bool> ExistsByCenterCodeAsync(string centerCode);

    /// <summary>Ids of every non-deleted teacher owned by the center.</summary>
    Task<IReadOnlyList<long>> GetTeacherIdsByCenterAsync(long centerId);

    /// <summary>Non-deleted teachers owned by the center, each with its User loaded (for lists/overview).</summary>
    Task<IReadOnlyList<Teacher>> GetTeachersByCenterAsync(long centerId);

    /// <summary>Count of the center's active (non-deleted) teachers of a given plan — teacher-slot enforcement.</summary>
    Task<int> CountActiveTeachersByPlanAsync(long centerId, SubscriptionPlanType plan);

    /// <summary>Count of roster students across the center's teachers of a given plan — student-pool enforcement.</summary>
    Task<int> CountCenterStudentsByPlanAsync(long centerId, SubscriptionPlanType plan);

    /// <summary>Count of roster students across ALL the center's teachers — overall student-capacity enforcement.</summary>
    Task<int> CountCenterStudentsTotalAsync(long centerId);

    /// <summary>Per-teacher roster student counts for the center's teachers (teacherId → count), for list/overview.</summary>
    Task<Dictionary<long, int>> GetStudentCountsByCenterTeachersAsync(long centerId);

    /// <summary>
    /// Every active roster student CODE across ALL the center's teachers — the candidate pool the AUTO
    /// code generator uses so a center-owned teacher's generated code is unique center-wide.
    /// </summary>
    Task<IReadOnlyList<string>> GetAllStudentCodesForCenterAsync(long centerId);

    /// <summary>The center's current subscription row (IsCurrent = true), or null. Includes no navs.</summary>
    Task<CenterSubscription?> GetCurrentCenterSubscriptionAsync(long centerId);

    /// <summary>
    /// The center's current subscription row with a pessimistic lock (UPDLOCK, HOLDLOCK) for the
    /// activation transaction — mirrors UserRepo.GetCurrentSubscriptionForUpdateAsync.
    /// </summary>
    Task<CenterSubscription?> GetCurrentCenterSubscriptionForUpdateAsync(long centerId);

    /// <summary>
    /// Flips the previous current row to IsCurrent=false and inserts the new one — the filtered unique
    /// index IX_CenterSubscriptions_Current guarantees exactly one current row. Caller owns the tx/commit.
    /// </summary>
    Task FlipCurrentAndInsertNewCenterSubscriptionAsync(CenterSubscription? previousCurrent, CenterSubscription newSubscription);

    /// <summary>The center's live Pending subscription request, or null (one at a time by filtered index).</summary>
    Task<CenterSubscriptionRequest?> GetPendingRequestByCenterAsync(long centerId);

    /// <summary>A center subscription request by id (tracked, for admin approve/reject), or null.</summary>
    Task<CenterSubscriptionRequest?> GetCenterSubscriptionRequestByIdAsync(long requestId);

    /// <summary>All Pending center subscription requests, oldest first (admin FIFO queue).</summary>
    Task<IReadOnlyList<CenterSubscriptionRequest>> GetPendingCenterSubscriptionRequestsAsync();

    /// <summary>
    /// Exact-match roster students carrying the given code across ALL the center's teachers (active
    /// rows only). Backs the front-desk center-wide code resolve / disambiguation.
    /// </summary>
    Task<IReadOnlyList<CenterStudentCodeMatch>> ResolveStudentsByCodeAcrossCenterAsync(long centerId, string code);
}
