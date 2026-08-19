using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Center;
using Edvanz.Application.Dtos.Teacher;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Edvanz.Domain.ServiceContract;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Center self-service. Teacher PROFILES are created with NO usable login (their identity
/// <see cref="User"/> row is IsActive=false with a random unguessable password), then initialized
/// through the SHARED <see cref="ITeacherService.InitializeTeacherAsync"/> so a center-owned teacher
/// is identical to a standalone one everywhere the app scopes on Teacher.Id. Slot/pool quotas are
/// enforced against the center's current subscription when one exists.
/// </summary>
public class CenterService : ICenterService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly IPasswordService _passwordService;
    private readonly ITeacherService _teacherService;
    private readonly IservicesContract.ISubscriptionCacheService _subscriptionCache;
    private readonly IservicesContract.IUserAuthInvalidationService _authInvalidation;
    private readonly ITimeZoneService _timeZone;

    public CenterService(
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer,
        IPasswordService passwordService,
        ITeacherService teacherService,
        IservicesContract.ISubscriptionCacheService subscriptionCache,
        IservicesContract.IUserAuthInvalidationService authInvalidation,
        ITimeZoneService timeZone)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _passwordService = passwordService;
        _teacherService = teacherService;
        _subscriptionCache = subscriptionCache;
        _authInvalidation = authInvalidation;
        _timeZone = timeZone;
    }

    /// <inheritdoc />
    public async Task<Result<CenterOverviewDto>> GetOverviewAsync(long centerId)
    {
        var center = await _unitOfWork.Centers.GetCenterByIdAsync(centerId);
        if (center == null)
            return Result<CenterOverviewDto>.Failure(_localizer, "CenterNotFound", HttpStatusCode.NotFound);

        var full = await _unitOfWork.Centers.CountActiveTeachersByPlanAsync(centerId, SubscriptionPlanType.Full);
        var managerial = await _unitOfWork.Centers.CountActiveTeachersByPlanAsync(centerId, SubscriptionPlanType.Managerial);
        var students = await _unitOfWork.Centers.CountCenterStudentsTotalAsync(centerId);
        var sub = await _unitOfWork.Centers.GetCurrentCenterSubscriptionAsync(centerId);

        var dto = new CenterOverviewDto
        {
            CenterId = center.Id,
            Name = center.Name,
            CenterCode = center.CenterCode,
            DefaultRevenueSharePercent = center.DefaultRevenueSharePercent,
            TeacherCount = full + managerial,
            FullTeacherCount = full,
            ManagerialTeacherCount = managerial,
            StudentCount = students,
            HasActiveSubscription = sub != null && sub.EndDate > DateTime.UtcNow,
            FullTeacherSlots = sub?.FullTeacherSlots,
            ManagerialTeacherSlots = sub?.ManagerialTeacherSlots,
            StudentCapacityTotal = sub?.StudentCapacityTotal,
            StudentCapacityUnderFull = sub?.StudentCapacityUnderFull,
            StudentCapacityUnderManagerial = sub?.StudentCapacityUnderManagerial,
            SubscriptionEndDate = sub?.EndDate
        };
        return Result<CenterOverviewDto>.Success(dto, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<CenterSettingsDto>> GetSettingsAsync(long centerId)
    {
        var center = await _unitOfWork.Centers.GetCenterByIdAsync(centerId);
        if (center == null)
            return Result<CenterSettingsDto>.Failure(_localizer, "CenterNotFound", HttpStatusCode.NotFound);

        return Result<CenterSettingsDto>.Success(new CenterSettingsDto
        {
            CenterId = center.Id,
            Name = center.Name,
            CenterCode = center.CenterCode,
            DefaultRevenueSharePercent = center.DefaultRevenueSharePercent,
            StudentCodeGenerationMode = center.StudentCodeGenerationMode
        }, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<CenterSettingsDto>> UpdateSettingsAsync(long centerId, UpdateCenterSettingsDto dto)
    {
        var center = await _unitOfWork.Centers.GetCenterByIdAsync(centerId);
        if (center == null)
            return Result<CenterSettingsDto>.Failure(_localizer, "CenterNotFound", HttpStatusCode.NotFound);
        if (dto.DefaultRevenueSharePercent is < 0 or > 100)
            return Result<CenterSettingsDto>.Failure(_localizer, "InvalidRevenueSharePercent", HttpStatusCode.BadRequest);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(dto.Name)) center.Name = dto.Name.Trim();
            if (dto.DefaultRevenueSharePercent.HasValue) center.DefaultRevenueSharePercent = dto.DefaultRevenueSharePercent.Value;
            if (dto.StudentCodeGenerationMode.HasValue) center.StudentCodeGenerationMode = dto.StudentCodeGenerationMode.Value;
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            return Result<CenterSettingsDto>.Success(new CenterSettingsDto
            {
                CenterId = center.Id,
                Name = center.Name,
                CenterCode = center.CenterCode,
                DefaultRevenueSharePercent = center.DefaultRevenueSharePercent,
                StudentCodeGenerationMode = center.StudentCodeGenerationMode
            }, _localizer, "CenterSettingsUpdated");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<CenterSettingsDto>.Failure(_localizer, "ServerError");
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<CenterTeacherListItemDto>>> GetTeachersAsync(long centerId)
    {
        var center = await _unitOfWork.Centers.GetCenterByIdAsync(centerId);
        if (center == null)
            return Result<List<CenterTeacherListItemDto>>.Failure(_localizer, "CenterNotFound", HttpStatusCode.NotFound);

        var teachers = await _unitOfWork.Centers.GetTeachersByCenterAsync(centerId);
        var counts = await _unitOfWork.Centers.GetStudentCountsByCenterTeachersAsync(centerId);

        var list = teachers.Select(t => ToTeacherItem(t, center.DefaultRevenueSharePercent,
            center.StudentCodeGenerationMode, counts.TryGetValue(t.Id, out var c) ? c : 0)).ToList();

        return Result<List<CenterTeacherListItemDto>>.Success(list, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<CenterTeacherListItemDto>> CreateTeacherAsync(long centerId, long actingUserId, CreateCenterTeacherDto dto)
    {
        var center = await _unitOfWork.Centers.GetCenterByIdAsync(centerId);
        if (center == null)
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "CenterNotFound", HttpStatusCode.NotFound);

        if (string.IsNullOrWhiteSpace(dto.FullName))
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "TeacherNameRequired");
        if ((dto.SubjectIds == null || dto.SubjectIds.Count == 0) && string.IsNullOrWhiteSpace(dto.CustomSubject))
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "SubjectRequired");
        if (dto.RevenueSharePercentOverride is < 0 or > 100)
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "InvalidRevenueSharePercent");

        // Teacher-slot quota — ALWAYS enforced. With an active subscription the cap is the entitlement;
        // WITHOUT one the center gets a small free-tier trial (mirrors the teacher free-tier), so it is
        // never unlimited. Each created teacher's students then fall under the normal per-teacher
        // free-tier limit automatically (they have no active subscription).
        var sub = await _unitOfWork.Centers.GetCurrentCenterSubscriptionAsync(centerId);
        var used = await _unitOfWork.Centers.CountActiveTeachersByPlanAsync(centerId, dto.PlanType);
        var slots = dto.PlanType == SubscriptionPlanType.Managerial
            ? (sub?.ManagerialTeacherSlots ?? CenterConstants.FreeTierManagerialTeacherSlots)
            : (sub?.FullTeacherSlots ?? CenterConstants.FreeTierFullTeacherSlots);
        if (used >= slots)
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "CenterTeacherSlotExhausted", HttpStatusCode.Conflict);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Identity row for a center-owned teacher: NO usable login (IsActive=false + random hash),
            // a system-generated unique username (never used to sign in).
            var user = new User
            {
                UserType = UserType.Teacher,
                FullName = dto.FullName.Trim(),
                Username = $"ct_{center.CenterCode}_{Guid.NewGuid():N}".Substring(0, 24),
                PasswordHashed = _passwordService.HashPassword(Guid.NewGuid().ToString("N")),
                IsActive = false,
                CreateAt = DateTime.UtcNow,
                CreateByUserId = actingUserId
            };
            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var initResult = await _teacherService.InitializeTeacherAsync(new CreateTeacherDto
            {
                UserId = user.Id,
                SubjectIds = dto.SubjectIds ?? new List<long>(),
                CustomSubject = dto.CustomSubject,
                LanguagePreference = dto.LanguagePreference,
                CreatedByUserId = actingUserId,
                StudentCapacity = dto.StudentCapacity
            });
            if (!initResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync();
                return Result<CenterTeacherListItemDto>.Failure(_localizer, initResult.Message);
            }

            var teacher = await _unitOfWork.Users.GetTeacherByUserIdAsync(user.Id);
            if (teacher == null)
            {
                await _unitOfWork.RollbackAsync();
                return Result<CenterTeacherListItemDto>.Failure(_localizer, "ServerError");
            }

            teacher.CenterId = centerId;
            teacher.CenterPlanType = dto.PlanType;
            teacher.RevenueSharePercentOverride = dto.RevenueSharePercentOverride;
            teacher.StudentCodeModeOverride = dto.StudentCodeModeOverride;
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            return Result<CenterTeacherListItemDto>.Success(
                ToTeacherItem(teacher, center.DefaultRevenueSharePercent, center.StudentCodeGenerationMode, 0, user.FullName),
                _localizer, "CenterTeacherCreated");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "ServerError");
        }
    }

    /// <inheritdoc />
    public async Task<Result<CenterTeacherListItemDto>> UpdateTeacherAsync(long centerId, long teacherId, UpdateCenterTeacherDto dto)
    {
        if (!await _unitOfWork.Centers.IsTeacherInCenterAsync(centerId, teacherId))
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "CenterTeacherNotFound", HttpStatusCode.NotFound);
        if (dto.RevenueSharePercentOverride is < 0 or > 100)
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "InvalidRevenueSharePercent");

        var center = await _unitOfWork.Centers.GetCenterByIdAsync(centerId);
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher == null || center == null)
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "CenterTeacherNotFound", HttpStatusCode.NotFound);

        var user = await _unitOfWork.Users.GetUserByIdAsync(teacher.UserId);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(dto.FullName) && user != null)
                user.FullName = dto.FullName.Trim();
            if (dto.PlanType.HasValue) teacher.CenterPlanType = dto.PlanType.Value;
            if (dto.StudentCapacity.HasValue) teacher.StudentCapacity = dto.StudentCapacity.Value;
            // Overrides are set UNCONDITIONALLY (including to null) so the edit form can clear an
            // override back to "inherit center default" — the form always sends the intended value.
            teacher.RevenueSharePercentOverride = dto.RevenueSharePercentOverride;
            teacher.StudentCodeModeOverride = dto.StudentCodeModeOverride;

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            // A plan-type change alters this teacher's effective (center-redirected) subscription
            // projection — cached by teacherId — so the managerial/active gate re-reads it.
            if (dto.PlanType.HasValue)
            {
                try { await _subscriptionCache.InvalidateAsync(teacher.Id); } catch { /* cache best-effort */ }
            }

            var counts = await _unitOfWork.Centers.GetStudentCountsByCenterTeachersAsync(centerId);
            return Result<CenterTeacherListItemDto>.Success(
                ToTeacherItem(teacher, center.DefaultRevenueSharePercent, center.StudentCodeGenerationMode,
                    counts.TryGetValue(teacher.Id, out var c) ? c : 0, user?.FullName),
                _localizer, "CenterTeacherUpdated");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "ServerError");
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> DeactivateTeacherAsync(long centerId, long teacherId)
    {
        if (!await _unitOfWork.Centers.IsTeacherInCenterAsync(centerId, teacherId))
            return Result<string>.Failure(_localizer, "CenterTeacherNotFound", HttpStatusCode.NotFound);

        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher == null)
            return Result<string>.Failure(_localizer, "CenterTeacherNotFound", HttpStatusCode.NotFound);

        var user = await _unitOfWork.Users.GetUserByIdAsync(teacher.UserId);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            teacher.AccountStatus = AccountStatus.Inactive;
            teacher.DeactivatedAt = DateTime.UtcNow;
            // Suspending the teacher also blocks their login (if enabled) — a deactivated teacher
            // must not be able to sign in. Reactivation does NOT auto-re-enable login; the center
            // re-enables it explicitly (fail-closed).
            if (user != null && user.IsActive == true)
                await DisableLoginInTransactionAsync(user);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            return Result<string>.Success("ok", _localizer, "CenterTeacherDeactivated");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<string>.Failure(_localizer, "ServerError");
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> ReactivateTeacherAsync(long centerId, long teacherId)
    {
        if (!await _unitOfWork.Centers.IsTeacherInCenterAsync(centerId, teacherId))
            return Result<string>.Failure(_localizer, "CenterTeacherNotFound", HttpStatusCode.NotFound);

        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher == null)
            return Result<string>.Failure(_localizer, "CenterTeacherNotFound", HttpStatusCode.NotFound);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            teacher.AccountStatus = AccountStatus.Active;
            teacher.DeactivatedAt = null;
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            return Result<string>.Success("ok", _localizer, "Success");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<string>.Failure(_localizer, "ServerError");
        }
    }

    /// <inheritdoc />
    public async Task<Result<CenterTeacherListItemDto>> EnableTeacherLoginAsync(long centerId, long teacherId, EnableCenterTeacherLoginDto dto)
    {
        var username = dto.Username?.Trim() ?? string.Empty;
        if (username.Length < 4)
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "CenterTeacherUsernameInvalid");
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8)
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "PasswordTooShort");

        if (!await _unitOfWork.Centers.IsTeacherInCenterAsync(centerId, teacherId))
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "CenterTeacherNotFound", HttpStatusCode.NotFound);

        var center = await _unitOfWork.Centers.GetCenterByIdAsync(centerId);
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher == null || center == null)
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "CenterTeacherNotFound", HttpStatusCode.NotFound);

        var user = await _unitOfWork.Users.GetUserByIdAsync(teacher.UserId);
        if (user == null)
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "ServerError");

        // Username must be globally unique (login identity). Allow re-pointing the SAME teacher's
        // username; reject a value already taken by another account.
        var existing = await _unitOfWork.Users.GetByUserName(username);
        if (existing != null && existing.Id != user.Id)
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "CenterTeacherUsernameTaken", HttpStatusCode.Conflict);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            user.Username = username;
            user.PasswordHashed = _passwordService.HashPassword(dto.Password);
            user.IsActive = true;
            user.SecurityStamp = Guid.NewGuid().ToString();
            // Snapshot invalidation joins this transaction (BEFORE SaveChanges) — the IsActive flip
            // must be visible to SecurityStampValidationMiddleware on the teacher's first request (§5.1).
            await _authInvalidation.InvalidateUserAsync(user.Id);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            var counts = await _unitOfWork.Centers.GetStudentCountsByCenterTeachersAsync(centerId);
            return Result<CenterTeacherListItemDto>.Success(
                ToTeacherItem(teacher, center.DefaultRevenueSharePercent, center.StudentCodeGenerationMode,
                    counts.TryGetValue(teacher.Id, out var c) ? c : 0, user.FullName),
                _localizer, "CenterTeacherLoginEnabled");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<CenterTeacherListItemDto>.Failure(_localizer, "ServerError");
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> ResetTeacherPasswordAsync(long centerId, long teacherId, ResetCenterTeacherPasswordDto dto)
    {
        if (!string.Equals(dto.NewPassword, dto.ConfirmPassword, StringComparison.Ordinal))
            return Result<string>.Failure(_localizer, "PasswordConfirmationMismatch");
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
            return Result<string>.Failure(_localizer, "PasswordTooShort");

        if (!await _unitOfWork.Centers.IsTeacherInCenterAsync(centerId, teacherId))
            return Result<string>.Failure(_localizer, "CenterTeacherNotFound", HttpStatusCode.NotFound);

        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher == null)
            return Result<string>.Failure(_localizer, "CenterTeacherNotFound", HttpStatusCode.NotFound);

        var user = await _unitOfWork.Users.GetUserByIdAsync(teacher.UserId);
        if (user == null)
            return Result<string>.Failure(_localizer, "ServerError");
        if (user.IsActive != true)
            return Result<string>.Failure(_localizer, "CenterTeacherLoginNotEnabled", HttpStatusCode.Conflict);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            user.PasswordHashed = _passwordService.HashPassword(dto.NewPassword);
            user.SecurityStamp = Guid.NewGuid().ToString();
            // A center-driven reset always revokes the teacher's live sessions (mirrors the admin
            // force-reset), so a shared/old device is signed out.
            var tokens = _unitOfWork.RefreshTokenRepo.GetByUserId(user.Id);
            await _unitOfWork.GetRepository<RefreshToken, long>().DeleteRangeAsync(tokens);
            await _authInvalidation.InvalidateUserAsync(user.Id);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            return Result<string>.Success("ok", _localizer, "CenterTeacherPasswordReset");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<string>.Failure(_localizer, "ServerError");
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> DisableTeacherLoginAsync(long centerId, long teacherId)
    {
        if (!await _unitOfWork.Centers.IsTeacherInCenterAsync(centerId, teacherId))
            return Result<string>.Failure(_localizer, "CenterTeacherNotFound", HttpStatusCode.NotFound);

        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher == null)
            return Result<string>.Failure(_localizer, "CenterTeacherNotFound", HttpStatusCode.NotFound);

        var user = await _unitOfWork.Users.GetUserByIdAsync(teacher.UserId);
        if (user == null)
            return Result<string>.Failure(_localizer, "ServerError");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await DisableLoginInTransactionAsync(user);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            return Result<string>.Success("ok", _localizer, "CenterTeacherLoginDisabled");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<string>.Failure(_localizer, "ServerError");
        }
    }

    /// <summary>Blocks login on a User (IsActive=false), bumps the security stamp, revokes live
    /// sessions, and joins the Redis snapshot invalidation to the caller's transaction. The caller
    /// owns SaveChanges/Commit (§5.2).</summary>
    private async Task DisableLoginInTransactionAsync(User user)
    {
        user.IsActive = false;
        user.SecurityStamp = Guid.NewGuid().ToString();
        var tokens = _unitOfWork.RefreshTokenRepo.GetByUserId(user.Id);
        await _unitOfWork.GetRepository<RefreshToken, long>().DeleteRangeAsync(tokens);
        await _authInvalidation.InvalidateUserAsync(user.Id);
    }

    /// <inheritdoc />
    public async Task<Result<List<CenterStudentResolveCandidateDto>>> ResolveStudentByCodeAsync(long centerId, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result<List<CenterStudentResolveCandidateDto>>.Success(
                new List<CenterStudentResolveCandidateDto>(), _localizer, "Success");

        var matches = await _unitOfWork.Centers.ResolveStudentsByCodeAcrossCenterAsync(centerId, code.Trim());
        var list = new List<CenterStudentResolveCandidateDto>();
        foreach (var m in matches)
        {
            // Resolve TODAY's occurrence (teacher-local) for the student's session so a scan can jump
            // straight into that session's take-attendance form; null when there's no class today.
            long? todayOccurrenceId = null;
            if (m.SessionId is long sessionId)
            {
                var localToday = _timeZone.GetTeacherLocalDate(m.TeacherId);
                var occurrence = await _unitOfWork.AttendanceRepo.GetOccurrenceBySessionAndDateAsync(sessionId, localToday);
                todayOccurrenceId = occurrence?.Id;
            }

            list.Add(new CenterStudentResolveCandidateDto
            {
                TeacherId = m.TeacherId,
                TeacherName = m.TeacherName,
                TeacherCode = m.TeacherCode,
                TeacherStudentId = m.TeacherStudentId,
                StudentName = m.StudentName,
                StudentCode = m.StudentCode,
                StudentPhoneNumber = m.StudentPhoneNumber,
                SessionId = m.SessionId,
                SessionName = m.SessionName,
                TodaySessionOccurrenceId = todayOccurrenceId
            });
        }

        // Messaging hint for the client: many → show a picker, one → auto-select, none → not found.
        var messageKey = list.Count > 1 ? "StudentCodeCandidates" : "Success";
        return Result<List<CenterStudentResolveCandidateDto>>.Success(list, _localizer, messageKey);
    }

    // ── mappers ──
    private static CenterTeacherListItemDto ToTeacherItem(Teacher t, decimal centerDefaultPercent,
        GenerationMode centerDefaultCodeMode, int studentCount, string? fullNameOverride = null) => new()
    {
        TeacherId = t.Id,
        FullName = fullNameOverride ?? t.User?.FullName ?? string.Empty,
        TeacherCode = t.TeacherCode,
        PlanType = t.CenterPlanType,
        StudentCapacity = t.StudentCapacity,
        RevenueSharePercentOverride = t.RevenueSharePercentOverride,
        EffectiveRevenueSharePercent = t.RevenueSharePercentOverride ?? centerDefaultPercent,
        StudentCodeModeOverride = t.StudentCodeModeOverride,
        EffectiveStudentCodeMode = t.StudentCodeModeOverride ?? centerDefaultCodeMode,
        AccountStatus = t.AccountStatus,
        StudentCount = studentCount,
        // Login gate = identity User.IsActive. The placeholder username is hidden until login is on.
        LoginEnabled = t.User?.IsActive == true,
        LoginUsername = t.User?.IsActive == true ? t.User?.Username : null
    };
}
