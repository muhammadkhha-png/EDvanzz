using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Center;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Center-facing self-service: overview, and management of the center's teacher PROFILES (which have
/// no login of their own). The center operates each teacher's data by "acting as" it (the acting-as
/// resolvers), not through this service.
/// </summary>
public interface ICenterService
{
    Task<Result<CenterOverviewDto>> GetOverviewAsync(long centerId);

    /// <summary>Center's own settings (revenue-share %, student-code mode, + the full center-default
    /// configuration = teacher-parity toggles + prorated tiers) — center-controlled.</summary>
    Task<Result<CenterSettingsDto>> GetSettingsAsync(long centerId);
    Task<Result<CenterSettingsDto>> UpdateSettingsAsync(long centerId, UpdateCenterSettingsDto dto);

    /// <summary>Overwrites EVERY non-deleted center teacher's configuration (all mirrored toggles +
    /// prorated tiers) from the center's default config, running the SAME per-teacher save the teacher
    /// settings screen runs — including the proration reconcile. Never touches a teacher's
    /// capacity/subscription/revenue-override. Idempotent; returns the number of teachers updated.</summary>
    Task<Result<ApplyCenterConfigResultDto>> ApplyConfigToAllTeachersAsync(long centerId);

    Task<Result<List<CenterTeacherListItemDto>>> GetTeachersAsync(long centerId);
    Task<Result<CenterTeacherListItemDto>> CreateTeacherAsync(long centerId, long actingUserId, CreateCenterTeacherDto dto);
    Task<Result<CenterTeacherListItemDto>> UpdateTeacherAsync(long centerId, long teacherId, UpdateCenterTeacherDto dto);
    Task<Result<string>> DeactivateTeacherAsync(long centerId, long teacherId);
    Task<Result<string>> ReactivateTeacherAsync(long centerId, long teacherId);

    /// <summary>Enable (or re-point) a center-owned teacher's login: set the sign-in username + an
    /// initial password and activate the identity so the teacher can log in normally.</summary>
    Task<Result<CenterTeacherListItemDto>> EnableTeacherLoginAsync(long centerId, long teacherId, EnableCenterTeacherLoginDto dto);

    /// <summary>Center-managed password reset for one of its teachers (no old-password needed).
    /// Revokes the teacher's live sessions.</summary>
    Task<Result<string>> ResetTeacherPasswordAsync(long centerId, long teacherId, ResetCenterTeacherPasswordDto dto);

    /// <summary>Turn off a teacher's login (blocks sign-in + revokes sessions) without deleting the
    /// teacher or any data. Reversible via <see cref="EnableTeacherLoginAsync"/>.</summary>
    Task<Result<string>> DisableTeacherLoginAsync(long centerId, long teacherId);

    /// <summary>Center-wide exact-match code resolve — returns one candidate per teacher using the
    /// code (0/1/many) so a shared code can be disambiguated at the front desk.</summary>
    Task<Result<List<CenterStudentResolveCandidateDto>>> ResolveStudentByCodeAsync(long centerId, string? code);

    /// <summary>
    /// TODAY's scheduled class occurrences across the center's ACTIVE teachers (teacher-local
    /// date), ordered by teacher then start time — the session-first pick list for front-desk
    /// attendance scanning.
    /// </summary>
    Task<Result<List<CenterTodaySessionDto>>> GetTodaySessionsAsync(long centerId);

    /// <summary>
    /// The recurrence SCHEDULES of every ACTIVE session across the center's ACTIVE teachers (with
    /// teacher identity), in a single batched read. The front-desk attendance picker runs the
    /// teacher-home recurrence logic client-side over these to render a week-day strip and the
    /// selected day's classes grouped per teacher — matching the teacher home exactly, scaled to many
    /// teachers. Unlike <see cref="GetTodaySessionsAsync"/> this is schedule-derived (not occurrence-
    /// derived), so it never lags materialized occurrences.
    /// </summary>
    Task<Result<List<CenterTeacherScheduleDto>>> GetTeacherScheduleSummariesAsync(long centerId);
}
