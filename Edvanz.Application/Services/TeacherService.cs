using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Teacher;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Helpers;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements all Teacher module operations.
/// Follows the Result pattern for operation outcomes.
/// All database access goes through IUnitOfWork.Users (IUserRepo) — no direct
/// GetRepository calls with raw expression predicates.
/// 
/// ARCHITECTURAL NOTE:
/// All query logic is encapsulated in IUserRepo named methods.
/// If a query needs to change, you edit the repo method — not this service.
/// 
/// TRANSACTION SAFETY:
/// All transactional methods use the ownsTransaction pattern:
///   bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
/// This makes them safe for both standalone calls and nested calls
/// from the User module's registration flow.
/// </summary>
public class TeacherService : ITeacherService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITeacherCodeGenerator _codeGenerator;
    private readonly ISubscriptionGateService _subscriptionGate;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;
    private readonly IUserAuthInvalidationService _authInvalidation;

    public TeacherService(
        IUnitOfWork unitOfWork,
        ITeacherCodeGenerator codeGenerator,
        ISubscriptionGateService subscriptionGate,
        IStringLocalizer<Domain.Resources.Messages> localizer,
        IUserAuthInvalidationService authInvalidation)   // NEW
    {
        _unitOfWork = unitOfWork;
        _codeGenerator = codeGenerator;
        _subscriptionGate = subscriptionGate;
        _localizer = localizer;
        _authInvalidation = authInvalidation;             // NEW
    }
    /// <inheritdoc />
    public async Task<Result<string>> ToggleTeacherStatusAsync(ToggleAccountStatus req)
    {
        // Include soft-deleted so a Suspended teacher can be reactivated.
        var teacher = await _unitOfWork.Users.GetTeacherByIdIncludingDeletedAsync(req.accountId);
        if (teacher is null)
            return Result<string>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        var user = await _unitOfWork.Users.GetUserByIdAsync(teacher.UserId);
        if (user is null)
            return Result<string>.Failure(_localizer, "UserNotFound", HttpStatusCode.NotFound);

        if (teacher.AccountStatus == req.targetStatus)
            return Result<string>.Failure(_localizer, "TeacherAlreadyInThisStatus");

        // Status + login gate.
        teacher.AccountStatus = req.targetStatus;
        user.IsActive = req.targetStatus == AccountStatus.Active;  // the actual sign-in gate

        switch (req.targetStatus)
        {
            case AccountStatus.Active:
                teacher.DeactivatedAt = null;
                teacher.DeletedAt = null;
                break;
            case AccountStatus.Inactive:
                teacher.DeactivatedAt = DateTime.UtcNow;
                break;
            case AccountStatus.Suspended:
                teacher.DeletedAt = DateTime.UtcNow;   // soft-delete (no +2min)
                break;
        }

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _unitOfWork.Users.UpdateTeacherAsync(teacher);

            // CLAUDE.md §5.1: stamp bump BEFORE SaveChanges so it joins this transaction —
            // forces sign-out and blocks re-login for Inactive/Suspended.
            await _authInvalidation.InvalidateUserAsync(user.Id);

            await _unitOfWork.SaveChangesAsync();
            if (ownsTransaction) await _unitOfWork.CommitAsync();
        }
        catch
        {
            if (ownsTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }

        return Result<string>.Success("Success", _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<TeacherProfileDto>> InitializeTeacherAsync(CreateTeacherDto dto)
    {
        // Validate the user exists and is of type Teacher
        var user = await _unitOfWork.Users.GetByIdAndTypeAsync(dto.UserId, UserType.Teacher);
        if (user is null)
            return Result<TeacherProfileDto>.Failure(_localizer, "UserNotFound", HttpStatusCode.NotFound);

        // Ensure no duplicate Teacher record for this user
        bool teacherExists = await _unitOfWork.Users.TeacherExistsByUserIdAsync(dto.UserId);
        if (teacherExists)
            return Result<TeacherProfileDto>.Failure(_localizer, "TeacherAlreadyInitialized", HttpStatusCode.Conflict);

        // Validate subjects exist if provided
        if (dto.SubjectIds.Count > 0)
        {
            foreach (var subjectId in dto.SubjectIds)
            {
                bool subjectExists = await _unitOfWork.Users.SubjectExistsAndActiveAsync(subjectId);
                if (!subjectExists)
                    return Result<TeacherProfileDto>.Failure(_localizer, "InvalidSubject", HttpStatusCode.BadRequest);
            }
        }

        // Validate at least one subject source is provided
        if (dto.SubjectIds.Count == 0 && string.IsNullOrWhiteSpace(dto.CustomSubject))
            return Result<TeacherProfileDto>.Failure(_localizer, "SubjectRequired", HttpStatusCode.BadRequest);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;

        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Generate unique 8-digit teacher code (AAM-FR-03.3 / AAM-NFR-03)
            string teacherCode = await _codeGenerator.GenerateUniqueCodeAsync();

            // Create the Teacher record
            var teacher = new Teacher
            {
                UserId = dto.UserId,
                TeacherCode = teacherCode,
                StudentCapacity = dto.StudentCapacity,
                LanguagePreference = dto.LanguagePreference,
                CustomSubject = dto.CustomSubject?.Trim(),
                AccountStatus = AccountStatus.Active,
                IsConfigurationCompleted = false,
                CreatedByUserId = dto.CreatedByUserId,
                CreateAt = DateTime.UtcNow
            };

            await _unitOfWork.Users.AddTeacherAsync(teacher);
            await _unitOfWork.SaveChangesAsync();

            // Create TeacherSubject records
            foreach (var subjectId in dto.SubjectIds)
            {
                var teacherSubject = new TeacherSubject
                {
                    TeacherId = teacher.Id,
                    SubjectId = subjectId,
                    CreateAt = DateTime.UtcNow
                };
                await _unitOfWork.Users.AddTeacherSubjectAsync(teacherSubject);
            }

            // Create default TeacherConfiguration (AAM-BR-04 / AAM-NFR-05)
            var config = new TeacherConfiguration
            {
                TeacherId = teacher.Id,
                StudentCodeGenerationMode = GenerationMode.Auto,
                StudentCodeLanguage = GenerationLanguage.English,
                SessionNameMode = GenerationMode.Auto,
                SessionNameLanguage = GenerationLanguage.English,
                IsProratedPaymentEnabled = false,
                ConsecutiveAbsenceThreshold = 3,
                ConsecutiveUnpaidThreshold = 3,
                BarcodeDisplayMode = BarcodeDisplayMode.InApp,
                StudentVisibilityAttendance = true,
                StudentVisibilityPayment = true,
                StudentVisibilityHomework = true,
                StudentVisibilityExamDefault = true, // 2026-07-18: offline exams visible by default (student side)
                StudentVisibilityVideo = true,
                ParentVisibilityAttendance = true,
                ParentVisibilityPayment = true,
                StudentVisibilityOnlineExamDefault = true,
                ParentVisibilityHomework = true,
                ParentVisibilityExamDefault = false, // AAM-BR-10: default hidden
                ParentVisibilityOnlineExamDefault = false, // mirrors ParentVisibilityExamDefault — opt-in only
                ParentVisibilityVideo = true,
                ShowPaymentInfoOnAttendanceScreen = true,
                ShowAttendanceHistoryOnAttendanceScreen = true,
                CreateAt = DateTime.UtcNow
            };

            await _unitOfWork.Users.AddConfigurationAsync(config);
            await _unitOfWork.SaveChangesAsync();

            // Create default prorated tiers (REQ-PAY-021)
            var defaultTiers = new List<TeacherProratedTier>
            {
                new() { TeacherConfigurationId = config.Id, TierNumber = 1, ThresholdDayStart = 1, ThresholdDayEnd = 10, FractionRate = 1.0000m, CreateAt = DateTime.UtcNow },
                new() { TeacherConfigurationId = config.Id, TierNumber = 2, ThresholdDayStart = 11, ThresholdDayEnd = 20, FractionRate = 0.6667m, CreateAt = DateTime.UtcNow },
                new() { TeacherConfigurationId = config.Id, TierNumber = 3, ThresholdDayStart = 21, ThresholdDayEnd = 31, FractionRate = 0.3333m, CreateAt = DateTime.UtcNow }
            };

            await _unitOfWork.Users.AddProratedTiersAsync(defaultTiers);
            await _unitOfWork.SaveChangesAsync();

            // Grant the new teacher access to ALL modules so the account is fully usable out of the
            // box (teachers infer their per-module permissions from module access). Without this, a
            // self-registered teacher hits a bare 403 on every [ModulePermission]-gated endpoint —
            // including their free-tier quota (e.g. adding their first student). Idempotent per module.
            var allModules = await _unitOfWork.GetRepository<Module, long>().GetAllAsync();
            foreach (var module in allModules)
                await _unitOfWork.ModuleTeacherRepo!.GrantModuleAsync(teacher.Id, module.Id);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var profile = await BuildTeacherProfileAsync(teacher.Id);
            return Result<TeacherProfileDto>.Success(profile, _localizer, "TeacherInitializedSuccess", HttpStatusCode.Created);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<TeacherProfileDto>> GetTeacherProfileAsync(long teacherId)
    {
        var teacher = await _unitOfWork.Users.GetTeacherByIdAsync(teacherId);

        if (teacher is null)
            return Result<TeacherProfileDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        var profile = await BuildTeacherProfileAsync(teacherId);
        return Result<TeacherProfileDto>.Success(profile, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<TeacherProfileDto>> UpdateTeacherProfileAsync(long teacherId, UpdateTeacherProfileDto dto)
    {
        var teacher = await _unitOfWork.Users.GetTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<TeacherProfileDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        var user = await _unitOfWork.Users.GetUserByIdAsync(teacher.UserId);
        if (user is null)
            return Result<TeacherProfileDto>.Failure(_localizer, "UserNotFound", HttpStatusCode.NotFound);

        // Validate FullName is not empty
        if (string.IsNullOrWhiteSpace(dto.FullName))
            return Result<TeacherProfileDto>.Failure(_localizer, "FullNameRequired", HttpStatusCode.BadRequest);

        // Validate language preference (system UI language — independent from code/session generation language)
        if (dto.LanguagePreference != "en" && dto.LanguagePreference != "ar")
            return Result<TeacherProfileDto>.Failure(_localizer, "InvalidLanguagePreference", HttpStatusCode.BadRequest);

        // Validate at least one subject source
        if (dto.SubjectIds.Count == 0 && string.IsNullOrWhiteSpace(dto.CustomSubject))
            return Result<TeacherProfileDto>.Failure(_localizer, "SubjectRequired", HttpStatusCode.BadRequest);

        // Validate all provided subject Ids exist and are active
        if (dto.SubjectIds.Count > 0)
        {
            foreach (var subjectId in dto.SubjectIds)
            {
                bool subjectExists = await _unitOfWork.Users.SubjectExistsAndActiveAsync(subjectId);
                if (!subjectExists)
                    return Result<TeacherProfileDto>.Failure(_localizer, "InvalidSubject", HttpStatusCode.BadRequest);
            }
        }

        // Validate capacity package if provided
        StudentCapacityPackage? selectedPackage = null;
        if (dto.StudentCapacityPackageId.HasValue)
        {
            selectedPackage = await _unitOfWork.Users.GetActiveCapacityPackageByIdAsync(dto.StudentCapacityPackageId.Value);
            if (selectedPackage is null)
                return Result<TeacherProfileDto>.Failure(_localizer, "InvalidCapacityPackage", HttpStatusCode.BadRequest);
        }

        // Capacity is admin-approved after onboarding: StudentCapacity drives the
        // per-student subscription price, so a configured teacher selecting a DIFFERENT
        // package must go through the capacity-increase request flow instead.
        // Re-sending the CURRENT package id (or none) stays a no-op — wire-compat for
        // clients that resubmit the whole profile object.
        if (selectedPackage is not null
            && teacher.IsConfigurationCompleted
            && teacher.StudentCapacityPackageId != selectedPackage.Id)
        {
            return Result<TeacherProfileDto>.Failure(
                _localizer, SubscriptionConstants.Messages.CapacityChangeRequiresApproval, HttpStatusCode.BadRequest);
        }

        // ── Transaction-safe: participates in outer tx if active ──
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Update User fields
            user.FullName = dto.FullName.Trim();
            await _unitOfWork.Users.UpdateAsync(user);

            // Update Teacher fields
            teacher.LanguagePreference = dto.LanguagePreference;
            teacher.CustomSubject = string.IsNullOrWhiteSpace(dto.CustomSubject) ? null : dto.CustomSubject.Trim();

            // Update capacity package and auto-set StudentCapacity from the package tier —
            // ONBOARDING ONLY. After configuration completes, capacity changes flow through
            // the admin-approved capacity-increase request (echoing the same package id
            // above must NOT overwrite an admin-granted capacity).
            if (selectedPackage is not null && !teacher.IsConfigurationCompleted)
            {
                teacher.StudentCapacityPackageId = selectedPackage.Id;
                // MaxStudents is null for the open-ended "3000+" tier — cap at the concrete
                // fallback (int.MaxValue would overflow capacity × rate pricing).
                teacher.StudentCapacity = selectedPackage.MaxStudents
                    ?? SubscriptionConstants.UnlimitedPackageFallbackCapacity;
            }

            await _unitOfWork.Users.UpdateTeacherAsync(teacher);

            // Replace subject associations: delete existing, add new
            var existingSubjects = await _unitOfWork.Users.GetTeacherSubjectsByTeacherIdAsync(teacherId);
            if (existingSubjects.Any())
                await _unitOfWork.Users.DeleteTeacherSubjectsAsync(existingSubjects);

            foreach (var subjectId in dto.SubjectIds)
            {
                var teacherSubject = new TeacherSubject
                {
                    TeacherId = teacherId,
                    SubjectId = subjectId,
                    CreateAt = DateTime.UtcNow
                };
                await _unitOfWork.Users.AddTeacherSubjectAsync(teacherSubject);
            }

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var profile = await BuildTeacherProfileAsync(teacherId);
            return Result<TeacherProfileDto>.Success(profile, _localizer, "ProfileUpdatedSuccess", HttpStatusCode.OK);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<TeacherPublicInfoDto>> GetTeacherByCodeAsync(string teacherCode)
    {
        if (string.IsNullOrWhiteSpace(teacherCode) || teacherCode.Length != 8)
            return Result<TeacherPublicInfoDto>.Failure(_localizer, "InvalidTeacherCode", HttpStatusCode.BadRequest);

        var teacher = await _unitOfWork.Users.GetActiveTeacherByCodeAsync(teacherCode);

        if (teacher is null)
            return Result<TeacherPublicInfoDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // Load related data for display
        var user = await _unitOfWork.Users.GetUserByIdAsync(teacher.UserId);

        var teacherSubjects = await _unitOfWork.Users.GetTeacherSubjectsByTeacherIdAsync(teacher.Id);

        string subjectName = teacher.CustomSubject ?? string.Empty;
        if (teacherSubjects.Any())
        {
            var firstSubject = await _unitOfWork.Users.GetSubjectByIdAsync(teacherSubjects.First().SubjectId);
            if (firstSubject is not null)
                subjectName = firstSubject.NameEn;
        }

        var dto = new TeacherPublicInfoDto
        {
            TeacherCode = teacher.TeacherCode,
            FullName = user?.FullName ?? string.Empty,
            SubjectName = subjectName
        };

        return Result<TeacherPublicInfoDto>.Success(dto, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<TeacherConfigurationDto>> SaveConfigurationAsync(long teacherId, UpdateTeacherConfigurationDto dto)
    {
        var teacher = await _unitOfWork.Users.GetTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<TeacherConfigurationDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        if (config is null)
            return Result<TeacherConfigurationDto>.Failure(_localizer, "ConfigurationNotFound", HttpStatusCode.NotFound);

        if (dto.IsProratedPaymentEnabled && dto.ProratedTiers.Count == 0)
            return Result<TeacherConfigurationDto>.Failure(_localizer, "ProratedTiersRequired", HttpStatusCode.BadRequest);

        if (dto.ProratedTiers.Count > 3)
            return Result<TeacherConfigurationDto>.Failure(_localizer, "MaxThreeProratedTiers", HttpStatusCode.BadRequest);

        // Validate the prorated tier list when proration is enabled and tiers are supplied.
        // NOTE (non-breaking): we deliberately do NOT require full-month coverage / no-gaps,
        // so the common last tier (e.g. days 21-30) and the app's default tiers
        // (1-10 @1.0000, 11-20 @0.6667, 21-31 @0.3333) still pass. Only the three checks below.
        if (dto.IsProratedPaymentEnabled && dto.ProratedTiers.Count > 0)
        {
            // (a) Each FractionRate is a fraction in (0, 1].
            if (dto.ProratedTiers.Any(t => t.FractionRate <= 0m || t.FractionRate > 1m))
                return Result<TeacherConfigurationDto>.Failure(_localizer, "ProratedTierFractionInvalid", HttpStatusCode.BadRequest);

            // (b) Each tier day range is valid: 1 <= Start <= End <= 31.
            if (dto.ProratedTiers.Any(t => t.ThresholdDayStart < 1 || t.ThresholdDayEnd > 31 || t.ThresholdDayStart > t.ThresholdDayEnd))
                return Result<TeacherConfigurationDto>.Failure(_localizer, "ProratedTierDayRangeInvalid", HttpStatusCode.BadRequest);

            // (c) Day ranges must not overlap across tiers (sorted by start, each next start > previous end).
            var orderedTiers = dto.ProratedTiers.OrderBy(t => t.ThresholdDayStart).ToList();
            for (int i = 1; i < orderedTiers.Count; i++)
            {
                if (orderedTiers[i].ThresholdDayStart <= orderedTiers[i - 1].ThresholdDayEnd)
                    return Result<TeacherConfigurationDto>.Failure(_localizer, "ProratedTiersOverlap", HttpStatusCode.BadRequest);
            }
        }

        // Capacity is admin-approved after onboarding (per-student pricing depends on it):
        // a configured teacher sending a DIFFERENT package id must use the capacity-increase
        // request flow. The same id (or none) is ignored — wire-compat for clients that
        // resubmit the whole configuration object.
        if (teacher.IsConfigurationCompleted
            && dto.StudentCapacityPackageId.HasValue
            && dto.StudentCapacityPackageId != teacher.StudentCapacityPackageId)
        {
            return Result<TeacherConfigurationDto>.Failure(
                _localizer, SubscriptionConstants.Messages.CapacityChangeRequiresApproval, HttpStatusCode.BadRequest);
        }

        // ── Transaction-safe ──
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // ONBOARDING ONLY (the post-completion different-package case already 400'd
            // above; the same-id case is deliberately ignored so an admin-granted
            // capacity is never overwritten by a config resubmit).
            if (dto.StudentCapacityPackageId.HasValue && !teacher.IsConfigurationCompleted)
            {
                var package = await _unitOfWork.Users.GetActiveCapacityPackageByIdAsync(dto.StudentCapacityPackageId.Value);
                if (package is null)
                    return Result<TeacherConfigurationDto>.Failure(_localizer, "InvalidCapacityPackage", HttpStatusCode.BadRequest);

                teacher.StudentCapacityPackageId = dto.StudentCapacityPackageId;
                // Auto-update StudentCapacity from the selected package tier.
                // MaxStudents is null for the open-ended "3000+" tier — cap at the concrete
                // fallback (int.MaxValue would overflow capacity × rate pricing).
                teacher.StudentCapacity = package.MaxStudents
                    ?? SubscriptionConstants.UnlimitedPackageFallbackCapacity;
                await _unitOfWork.Users.UpdateTeacherAsync(teacher);
            }

            // Update configuration fields
            config.StudentCodeGenerationMode = dto.StudentCodeGenerationMode;
            config.StudentCodeLanguage = dto.StudentCodeLanguage;
            config.SessionNameMode = dto.SessionNameMode;
            config.SessionNameLanguage = dto.SessionNameLanguage;
            config.IsProratedPaymentEnabled = dto.IsProratedPaymentEnabled;
            config.ConsecutiveAbsenceThreshold = dto.ConsecutiveAbsenceThreshold;
            config.ConsecutiveUnpaidThreshold = dto.ConsecutiveUnpaidThreshold;
            config.BarcodeDisplayMode = dto.BarcodeDisplayMode;
            config.StudentVisibilityAttendance = dto.StudentVisibilityAttendance;
            config.StudentVisibilityPayment = dto.StudentVisibilityPayment;
            config.StudentVisibilityHomework = dto.StudentVisibilityHomework;
            config.StudentVisibilityExamDefault = dto.StudentVisibilityExamDefault;
            config.StudentVisibilityVideo = dto.StudentVisibilityVideo;
            config.ParentVisibilityAttendance = dto.ParentVisibilityAttendance;
            config.ParentVisibilityPayment = dto.ParentVisibilityPayment;
            config.ParentVisibilityHomework = dto.ParentVisibilityHomework;
            config.ParentVisibilityExamDefault = dto.ParentVisibilityExamDefault;
            config.IsDeviceLockEnabled = dto.IsDeviceLockEnabled;
            config.ShowPaymentInfoOnAttendanceScreen = dto.ShowPaymentInfoOnAttendanceScreen;
            config.ShowAttendanceHistoryOnAttendanceScreen = dto.ShowAttendanceHistoryOnAttendanceScreen;
            config.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Users.UpdateConfigurationAsync(config);

            // Replace prorated tiers: delete existing, add new
            var existingTiers = await _unitOfWork.Users.GetProratedTiersByConfigIdAsync(config.Id);
            if (existingTiers.Any())
                await _unitOfWork.Users.DeleteProratedTiersAsync(existingTiers);

            foreach (var tierDto in dto.ProratedTiers)
            {
                var tier = new TeacherProratedTier
                {
                    TeacherConfigurationId = config.Id,
                    TierNumber = tierDto.TierNumber,
                    ThresholdDayStart = tierDto.ThresholdDayStart,
                    ThresholdDayEnd = tierDto.ThresholdDayEnd,
                    FractionRate = tierDto.FractionRate,
                    CreateAt = DateTime.UtcNow
                };
                await _unitOfWork.Users.AddProratedTierAsync(tier);
            }

            // Mark configuration as completed
            if (!teacher.IsConfigurationCompleted)
            {
                teacher.IsConfigurationCompleted = true;
                await _unitOfWork.Users.UpdateTeacherAsync(teacher);
            }

            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            var configResult = await GetConfigurationAsync(teacherId);
            if (configResult.IsSuccess && configResult.Data is not null)
                return Result<TeacherConfigurationDto>.Success(configResult.Data, _localizer, "ConfigurationSavedSuccess", HttpStatusCode.OK);

            return configResult;
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<TeacherConfigurationDto>> GetConfigurationAsync(long teacherId)
    {
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        if (config is null)
            return Result<TeacherConfigurationDto>.Failure(_localizer, "ConfigurationNotFound", HttpStatusCode.NotFound);

        var tiers = await _unitOfWork.Users.GetProratedTiersByConfigIdAsync(config.Id);

        var dto = new TeacherConfigurationDto
        {
            Id = config.Id,
            TeacherId = config.TeacherId,
            StudentCodeGenerationMode = config.StudentCodeGenerationMode,
            StudentCodeLanguage = config.StudentCodeLanguage,
            SessionNameMode = config.SessionNameMode,
            SessionNameLanguage = config.SessionNameLanguage,
            IsProratedPaymentEnabled = config.IsProratedPaymentEnabled,
            ConsecutiveAbsenceThreshold = config.ConsecutiveAbsenceThreshold,
            ConsecutiveUnpaidThreshold = config.ConsecutiveUnpaidThreshold,
            BarcodeDisplayMode = config.BarcodeDisplayMode,
            StudentVisibilityAttendance = config.StudentVisibilityAttendance,
            StudentVisibilityPayment = config.StudentVisibilityPayment,
            StudentVisibilityHomework = config.StudentVisibilityHomework,
            StudentVisibilityExamDefault = config.StudentVisibilityExamDefault,
            StudentVisibilityVideo = config.StudentVisibilityVideo,
            ParentVisibilityAttendance = config.ParentVisibilityAttendance,
            ParentVisibilityPayment = config.ParentVisibilityPayment,
            ParentVisibilityHomework = config.ParentVisibilityHomework,
            ParentVisibilityExamDefault = config.ParentVisibilityExamDefault,
            IsDeviceLockEnabled = config.IsDeviceLockEnabled,
            ShowPaymentInfoOnAttendanceScreen = config.ShowPaymentInfoOnAttendanceScreen,
            ShowAttendanceHistoryOnAttendanceScreen = config.ShowAttendanceHistoryOnAttendanceScreen,
            UpdatedAt = config.UpdatedAt,
            ProratedTiers = tiers.OrderBy(t => t.TierNumber).Select(t => new ProratedTierDto
            {
                TierNumber = t.TierNumber,
                ThresholdDayStart = t.ThresholdDayStart,
                ThresholdDayEnd = t.ThresholdDayEnd,
                FractionRate = t.FractionRate
            }).ToList()
        };

        return Result<TeacherConfigurationDto>.Success(dto, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<SubjectDto>>> GetAvailableSubjectsAsync()
    {
        var subjects = await _unitOfWork.Users.GetActiveSubjectsAsync();

        var dtos = subjects
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new SubjectDto
            {
                Id = s.Id,
                NameEn = s.NameEn,
                NameAr = s.NameAr,
                DisplayOrder = s.DisplayOrder
            })
            .ToList() as IReadOnlyList<SubjectDto>;

        return Result<IReadOnlyList<SubjectDto>>.Success(dtos, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<StudentCapacityPackageDto>>> GetCapacityPackagesAsync()
    {
        var packages = await _unitOfWork.Users.GetActiveCapacityPackagesAsync();

        var dtos = packages
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new StudentCapacityPackageDto
            {
                Id = p.Id,
                Name = p.Name,
                MinStudents = p.MinStudents,
                MaxStudents = p.MaxStudents,
                DisplayOrder = p.DisplayOrder
            })
            .ToList() as IReadOnlyList<StudentCapacityPackageDto>;

        return Result<IReadOnlyList<StudentCapacityPackageDto>>.Success(dtos, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    //public async Task<Result<TeacherSubscriptionDto?>> GetActiveSubscriptionAsync(long teacherId)
    //{
    //    // Find the most recent active or expiring-soon subscription
    //    var subscriptions = await _unitOfWork.Users.GetActiveSubscriptionsByTeacherIdAsync(teacherId);

    //    var activeSubscription = subscriptions
    //        .OrderByDescending(s => s.EndDate)
    //        .FirstOrDefault();

    //    if (activeSubscription is null)
    //        return Result<TeacherSubscriptionDto?>.Success(null, _localizer, "NoActiveSubscription", HttpStatusCode.OK);

    //    var dto = new TeacherSubscriptionDto
    //    {
    //        Id = activeSubscription.Id,
    //        SubscriptionStatus = activeSubscription.SubscriptionStatus.ToString(),
    //        StartDate = activeSubscription.StartDate,
    //        EndDate = activeSubscription.EndDate,
    //        DaysRemaining = Math.Max(0, (activeSubscription.EndDate.Date - DateTime.UtcNow.Date).Days)
    //    };

    //    return Result<TeacherSubscriptionDto?>.Success(dto, _localizer, "Success", HttpStatusCode.OK);
    //}
    /// <inheritdoc />
    public async Task<Result<TeacherSubscriptionDto?>> GetActiveSubscriptionAsync(long teacherId)
    {
        // Single-row lookup via the filtered unique index (BR-SUB-006).
        var currentSub = await _unitOfWork.Users.GetCurrentSubscriptionByTeacherIdAsync(teacherId);

        if (currentSub is null)
        {
            // No current row exists — teacher has never subscribed, or no row is IsCurrent.
            // Middleware treats this as Expired; the endpoint returns null data
            // with the "NoActiveSubscription" message.
            return Result<TeacherSubscriptionDto?>.Success(
                null, _localizer, "NoActiveSubscription", HttpStatusCode.OK);
        }

        var now = DateTime.UtcNow;

        // Derive status — the column no longer exists on the row (C-6 / D-08).
        var derivedStatus = SubscriptionStatusCalculator.Derive(currentSub, now);

        // If derived status is Expired, REQ-SUB-003 still requires returning the details
        // so the teacher can see when their subscription ended. Return the row with
        // Status = "Expired" — do NOT return null data here (different from the historical
        // behavior where there was no current row at all).
        var dto = new TeacherSubscriptionDto
        {
            Id = currentSub.Id,
            SubscriptionStatus = derivedStatus.ToString(),
            PlanType = currentSub.PlanType,
            StartDate = currentSub.StartDate,
            EndDate = currentSub.EndDate,
            DaysRemaining = SubscriptionStatusCalculator.DeriveDaysRemaining(currentSub, now)
        };

        return Result<TeacherSubscriptionDto?>.Success(dto, _localizer, "Success", HttpStatusCode.OK);
    }


    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<TeacherListItemDto>>>> GetTeachersAsync(
        PaginatedRequest request,
        string? accountStatus = null,
        string? subscriptionStatus = null)
    {
        // ── 1. Load all base data via named repo methods ───────────────────────
        var allTeachers = await _unitOfWork.Users.GetAllTeachersAsync();
        var allUsers = await _unitOfWork.Users.GetAllUsersAsync();
        var allSubscriptions = await _unitOfWork.Users.GetAllSubscriptionsAsync();
        var allTeacherSubjects = await _unitOfWork.Users.GetAllTeacherSubjectsAsync();
        var allSubjects = await _unitOfWork.Users.GetAllSubjectsAsync();

        // ── 2. Join teachers with users in memory ──────────────────────────────
        var joined = allTeachers
            .Select(teacher =>
            {
                var user = allUsers.FirstOrDefault(u => u.Id == teacher.UserId);

                // Build subject names for this teacher
                var subjectIds = allTeacherSubjects
                    .Where(ts => ts.TeacherId == teacher.Id)
                    .Select(ts => ts.SubjectId)
                    .ToList();

                var subjectNames = allSubjects
                    .Where(s => subjectIds.Contains(s.Id))
                    .Select(s => s.NameEn + " " + s.NameAr)
                    .ToList();

                // Combine all searchable text including custom subject
                var subjectSearchText = string.Join(" ", subjectNames);
                if (!string.IsNullOrWhiteSpace(teacher.CustomSubject))
                    subjectSearchText += " " + teacher.CustomSubject;

                // Get the CURRENT subscription (IsCurrent = true; at most one per teacher,
                // BR-SUB-006). Must match the teacher-detail endpoint (GetActiveSubscriptionAsync),
                // which reads the IsCurrent row. Ordering by EndDate alone picked a HISTORICAL row
                // (IsCurrent = false) after re-activations that create a new current row with an
                // equal/earlier end date — so the list showed a stale status (Derive treats a
                // non-current row as Expired) and a stale PlanType while the detail read correct.
                // Null when the teacher has no current row (never subscribed / all historical),
                // exactly as the detail returns "no subscription".
                var latestSub = allSubscriptions
                    .FirstOrDefault(s => s.TeacherId == teacher.Id && s.IsCurrent);

                return new
                {
                    Teacher = teacher,
                    User = user,
                    SubjectSearchText = subjectSearchText,
                    LatestSub = latestSub
                };
            })
            .ToList();

        // ── 3. Filter by account status ────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(accountStatus) &&
            Enum.TryParse<AccountStatus>(accountStatus, true, out var parsedAccountStatus))
        {
            joined = joined.Where(x => x.Teacher.AccountStatus == parsedAccountStatus).ToList();
        }

        // ── 4. Filter by subscription status ──────────────────────────────────
        //if (!string.IsNullOrWhiteSpace(subscriptionStatus) &&
        //    Enum.TryParse<SubscriptionStatus>(subscriptionStatus, true, out var parsedSubStatus))
        //{
        //    joined = joined
        //        .Where(x => x.LatestSub != null && x.LatestSub.SubscriptionStatus == parsedSubStatus)
        //        .ToList();
        //}
        if (!string.IsNullOrWhiteSpace(subscriptionStatus) &&
            Enum.TryParse<SubscriptionStatus>(subscriptionStatus, true, out var parsedSubStatus))
        {
            var filterNow = DateTime.UtcNow;
            joined = joined
                .Where(x => SubscriptionStatusCalculator.Derive(x.LatestSub, filterNow) == parsedSubStatus)
                .ToList();
        }
        // ── 5. Search — contains, case-insensitive, across all fields ─────────
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();

            joined = joined.Where(x =>
                // Teacher code
                x.Teacher.TeacherCode.ToLower().Contains(search) ||
                // Full name
                (x.User != null && x.User.FullName.ToLower().Contains(search)) ||
                // Username
                (x.User != null && x.User.Username.ToLower().Contains(search)) ||
                // Phone number
                (x.User != null && !string.IsNullOrWhiteSpace(x.User.PhoneNumber) &&
                 x.User.PhoneNumber.ToLower().Contains(search)) ||
                // Subject (predefined + custom)
                x.SubjectSearchText.ToLower().Contains(search)
            ).ToList();
        }

        // ── 6. Total count AFTER all filters ──────────────────────────────────
        var totalCount = joined.Count;

        bool isDesc = request.SortDirection == SortDirection.Desc;

        joined = request.SortBy switch
        {
            TeacherSortBy.Capacity => isDesc
                ? joined.OrderByDescending(x => x.Teacher.StudentCapacity).ToList()
                : joined.OrderBy(x => x.Teacher.StudentCapacity).ToList(),

            TeacherSortBy.Code => isDesc
                ? joined.OrderByDescending(x => x.Teacher.TeacherCode).ToList()
                : joined.OrderBy(x => x.Teacher.TeacherCode).ToList(),

            TeacherSortBy.Name => isDesc
                ? joined.OrderByDescending(x => x.User?.FullName ?? string.Empty).ToList()
                : joined.OrderBy(x => x.User?.FullName ?? string.Empty).ToList(),

            _ => isDesc
                ? joined.OrderByDescending(x => x.Teacher.CreateAt).ToList()
                : joined.OrderBy(x => x.Teacher.CreateAt).ToList()
        };

        // ── 8. Paginate ────────────────────────────────────────────────────────
        var paged = joined
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        // ── 9. Build DTOs ──────────────────────────────────────────────────────
        //var items = paged.Select(x => new TeacherListItemDto
        //{
        //    Id = x.Teacher.Id,
        //    FullName = x.User?.FullName ?? string.Empty,
        //    Username = x.User?.Username ?? string.Empty,
        //    TeacherCode = x.Teacher.TeacherCode,
        //    PhoneNumber = x.User?.PhoneNumber,
        //    StudentCapacity = x.Teacher.StudentCapacity,
        //    AccountStatus = x.Teacher.AccountStatus.ToString(),
        //    IsConfigurationCompleted = x.Teacher.IsConfigurationCompleted,
        //    SubscriptionStatus = x.LatestSub?.SubscriptionStatus.ToString(),
        //    SubscriptionEndDate = x.LatestSub?.EndDate,
        //    CreatedAt = x.Teacher.CreateAt
        //}).ToList();
        // ── 8b. Per-teacher student counts for the PAGE only (two GROUP BY queries) ──
        var pagedTeacherIds = paged.Select(x => x.Teacher.Id).ToList();
        var studentCounts = await _unitOfWork.Students
            .GetActiveStudentCountsAsync(pagedTeacherIds);
        var linkedCounts = await _unitOfWork.studentTeacherLinkRepo
            .GetActiveLinkedCountsAsync(pagedTeacherIds);

        // ── 9. Build DTOs (status DERIVED, not read from column) ─────────────
        var dtoNow = DateTime.UtcNow;
        var items = paged.Select(x => new TeacherListItemDto
        {
            Id = x.Teacher.Id,
            UserId = x.User?.Id ?? 0,
            FullName = x.User?.FullName ?? string.Empty,
            Username = x.User?.Username ?? string.Empty,
            TeacherCode = x.Teacher.TeacherCode,
            PhoneNumber = x.User?.PhoneNumber,
            StudentCapacity = x.Teacher.StudentCapacity,
            StudentCount = studentCounts.GetValueOrDefault(x.Teacher.Id, 0),
            LinkedStudentCount = linkedCounts.GetValueOrDefault(x.Teacher.Id, 0),
            AccountStatus = x.Teacher.AccountStatus.ToString(),
            IsConfigurationCompleted = x.Teacher.IsConfigurationCompleted,
            SubscriptionStatus = x.LatestSub is null
                ? null
                : SubscriptionStatusCalculator.Derive(x.LatestSub, dtoNow).ToString(),
            PlanType = x.LatestSub?.PlanType,
            SubscriptionEndDate = x.LatestSub?.EndDate,
            CreatedAt = x.Teacher.CreateAt
        }).ToList();

        // ── 10. Build response ─────────────────────────────────────────────────
        var response = new PaginatedResponse<List<TeacherListItemDto>>
        {
            totalCount = totalCount,
            page = request.Page,
            pageSize = request.PageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
            data = items
        };

        return Result<PaginatedResponse<List<TeacherListItemDto>>>.Success(
            response, _localizer, "Success", HttpStatusCode.OK);
    }

    // ══════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Builds a full TeacherProfileDto by loading all related data.
    /// All queries go through IUserRepo named methods — no raw expressions.
    /// </summary>
    private async Task<TeacherProfileDto> BuildTeacherProfileAsync(long teacherId)
    {
        var teacher = await _unitOfWork.Users.GetTeacherByIdAsync(teacherId);
        var user = await _unitOfWork.Users.GetUserByIdAsync(teacher!.UserId);
        var teacherSubjects = await _unitOfWork.Users.GetTeacherSubjectsByTeacherIdAsync(teacherId);

        var subjects = new List<SubjectDto>();
        foreach (var ts in teacherSubjects)
        {
            var subject = await _unitOfWork.Users.GetSubjectByIdAsync(ts.SubjectId);
            if (subject is not null)
            {
                subjects.Add(new SubjectDto
                {
                    Id = subject.Id,
                    NameEn = subject.NameEn,
                    NameAr = subject.NameAr,
                    DisplayOrder = subject.DisplayOrder
                });
            }
        }

        string? packageName = null;
        if (teacher!.StudentCapacityPackageId.HasValue)
        {
            var package = await _unitOfWork.Users.GetCapacityPackageByIdAsync(teacher.StudentCapacityPackageId.Value);
            packageName = package?.Name;
        }

        // Get active subscription
        var subscriptionResult = await GetActiveSubscriptionAsync(teacherId);
        bool isSubscribed = await _subscriptionGate.HasActiveSubscriptionAsync(teacherId);

        return new TeacherProfileDto
        {
            IsSubscribed = isSubscribed,
            Id = teacher.Id,
            UserId = teacher.UserId,
            TeacherCode = teacher.TeacherCode,
            FullName = user!.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            StudentCapacity = teacher.StudentCapacity,
            LanguagePreference = teacher.LanguagePreference,
            CustomSubject = teacher.CustomSubject,
            AccountStatus = teacher.AccountStatus.ToString(),
            IsConfigurationCompleted = teacher.IsConfigurationCompleted,
            CreatedAt = teacher.CreateAt,
            Subjects = subjects,
            CapacityPackageName = packageName,
            ActiveSubscription = subscriptionResult.Data,
            StudentCapacityPackageId = teacher.StudentCapacityPackageId,
        };
    }

    /// <inheritdoc />
    public async Task<Result<List<TeacherLookupItemDto>>> GetTeacherLookupAsync()
    {
        var rows = await _unitOfWork.Users.GetTeacherNameLookupAsync();

        var items = rows.Select(r => new TeacherLookupItemDto
        {
            Id = r.TeacherId,
            FullName = r.FullName
        }).ToList();

        return Result<List<TeacherLookupItemDto>>.Success(items, _localizer, "Success", HttpStatusCode.OK);
    }
}