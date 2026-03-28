using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Teacher;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Application.ServiceContract;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements all Teacher module operations.
/// Follows the Result pattern for operation outcomes.
/// All database access goes through IUnitOfWork + IGenericRepo.
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
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public TeacherService(
        IUnitOfWork unitOfWork,
        ITeacherCodeGenerator codeGenerator,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _codeGenerator = codeGenerator;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<Result<TeacherProfileDto>> InitializeTeacherAsync(CreateTeacherDto dto)
    {
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var teacherRepo = _unitOfWork.GetRepository<Teacher, long>();
        var teacherSubjectRepo = _unitOfWork.GetRepository<TeacherSubject, long>();
        var configRepo = _unitOfWork.GetRepository<TeacherConfiguration, long>();
        var proratedTierRepo = _unitOfWork.GetRepository<TeacherProratedTier, long>();

        // Validate the user exists and is of type Teacher
        var user = await userRepo.FindAsync(u => u.Id == dto.UserId && u.UserType == UserType.Teacher);
        if (user is null)
            return Result<TeacherProfileDto>.Failure(_localizer, "UserNotFound", HttpStatusCode.NotFound);

        // Ensure no duplicate Teacher record for this user
        bool teacherExists = await teacherRepo.AnyAsync(t => t.UserId == dto.UserId);
        if (teacherExists)
            return Result<TeacherProfileDto>.Failure(_localizer, "TeacherAlreadyInitialized", HttpStatusCode.Conflict);

        // Validate subjects exist if provided
        if (dto.SubjectIds.Count > 0)
        {
            var subjectRepo = _unitOfWork.GetRepository<Subject, long>();
            foreach (var subjectId in dto.SubjectIds)
            {
                bool subjectExists = await subjectRepo.AnyAsync(s => s.Id == subjectId && s.IsActive);
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

            await teacherRepo.AddAsync(teacher);
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
                await teacherSubjectRepo.AddAsync(teacherSubject);
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
                StudentVisibilityExamDefault = false, // AAM-BR-10: default hidden
                ParentVisibilityAttendance = true,
                ParentVisibilityPayment = true,
                ParentVisibilityHomework = true,
                ParentVisibilityExamDefault = false, // AAM-BR-10: default hidden
                CreateAt = DateTime.UtcNow
            };

            await configRepo.AddAsync(config);
            await _unitOfWork.SaveChangesAsync();

            // Create default prorated tiers (REQ-PAY-021)
            var defaultTiers = new List<TeacherProratedTier>
            {
                new() { TeacherConfigurationId = config.Id, TierNumber = 1, ThresholdDayStart = 1, ThresholdDayEnd = 10, FractionRate = 1.0000m, CreateAt = DateTime.UtcNow },
                new() { TeacherConfigurationId = config.Id, TierNumber = 2, ThresholdDayStart = 11, ThresholdDayEnd = 20, FractionRate = 0.6667m, CreateAt = DateTime.UtcNow },
                new() { TeacherConfigurationId = config.Id, TierNumber = 3, ThresholdDayStart = 21, ThresholdDayEnd = 31, FractionRate = 0.3333m, CreateAt = DateTime.UtcNow }
            };

            await proratedTierRepo.AddRangeAsync(defaultTiers);
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
        var teacherRepo = _unitOfWork.GetRepository<Teacher, long>();
        var teacher = await teacherRepo.FindAsync(t => t.Id == teacherId);

        if (teacher is null)
            return Result<TeacherProfileDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        var profile = await BuildTeacherProfileAsync(teacherId);
        return Result<TeacherProfileDto>.Success(profile, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<TeacherProfileDto>> UpdateTeacherProfileAsync(long teacherId, UpdateTeacherProfileDto dto)
    {
        var teacherRepo = _unitOfWork.GetRepository<Teacher, long>();
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var teacherSubjectRepo = _unitOfWork.GetRepository<TeacherSubject, long>();

        var teacher = await teacherRepo.FindAsync(t => t.Id == teacherId);
        if (teacher is null)
            return Result<TeacherProfileDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        var user = await userRepo.FindAsync(u => u.Id == teacher.UserId);
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
            var subjectRepo = _unitOfWork.GetRepository<Subject, long>();
            foreach (var subjectId in dto.SubjectIds)
            {
                bool subjectExists = await subjectRepo.AnyAsync(s => s.Id == subjectId && s.IsActive);
                if (!subjectExists)
                    return Result<TeacherProfileDto>.Failure(_localizer, "InvalidSubject", HttpStatusCode.BadRequest);
            }
        }

        // Validate capacity package if provided
        StudentCapacityPackage? selectedPackage = null;
        if (dto.StudentCapacityPackageId.HasValue)
        {
            var packageRepo = _unitOfWork.GetRepository<StudentCapacityPackage, long>();
            selectedPackage = await packageRepo.FindAsync(p => p.Id == dto.StudentCapacityPackageId.Value && p.IsActive);
            if (selectedPackage is null)
                return Result<TeacherProfileDto>.Failure(_localizer, "InvalidCapacityPackage", HttpStatusCode.BadRequest);
        }

        // ── Transaction-safe: participates in outer tx if active ──
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Update User fields
            user.FullName = dto.FullName.Trim();
            await userRepo.UpdateAsync(user);

            // Update Teacher fields
            teacher.LanguagePreference = dto.LanguagePreference;
            teacher.CustomSubject = string.IsNullOrWhiteSpace(dto.CustomSubject) ? null : dto.CustomSubject.Trim();

            // Update capacity package and auto-set StudentCapacity from the package tier
            if (selectedPackage is not null)
            {
                teacher.StudentCapacityPackageId = selectedPackage.Id;
                // MaxStudents is null for the "3000+" tier — use int.MaxValue as effective capacity
                teacher.StudentCapacity = selectedPackage.MaxStudents ?? int.MaxValue;
            }

            await teacherRepo.UpdateAsync(teacher);

            // Replace subject associations: delete existing, add new
            var existingSubjects = await teacherSubjectRepo.GetAsync(ts => ts.TeacherId == teacherId);
            if (existingSubjects.Any())
                await teacherSubjectRepo.DeleteRangeAsync(existingSubjects);

            foreach (var subjectId in dto.SubjectIds)
            {
                var teacherSubject = new TeacherSubject
                {
                    TeacherId = teacherId,
                    SubjectId = subjectId,
                    CreateAt = DateTime.UtcNow
                };
                await teacherSubjectRepo.AddAsync(teacherSubject);
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

        var teacherRepo = _unitOfWork.GetRepository<Teacher, long>();
        var teacher = await teacherRepo.FindAsync(t => t.TeacherCode == teacherCode && t.AccountStatus == AccountStatus.Active);

        if (teacher is null)
            return Result<TeacherPublicInfoDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // Load related data for display
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var user = await userRepo.FindAsync(u => u.Id == teacher.UserId);

        var teacherSubjectRepo = _unitOfWork.GetRepository<TeacherSubject, long>();
        var subjectRepo = _unitOfWork.GetRepository<Subject, long>();
        var teacherSubjects = await teacherSubjectRepo.GetAsync(ts => ts.TeacherId == teacher.Id);

        string subjectName = teacher.CustomSubject ?? string.Empty;
        if (teacherSubjects.Any())
        {
            var firstSubject = await subjectRepo.FindAsync(s => s.Id == teacherSubjects.First().SubjectId);
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
        var teacherRepo = _unitOfWork.GetRepository<Teacher, long>();
        var configRepo = _unitOfWork.GetRepository<TeacherConfiguration, long>();
        var proratedTierRepo = _unitOfWork.GetRepository<TeacherProratedTier, long>();

        var teacher = await teacherRepo.FindAsync(t => t.Id == teacherId);
        if (teacher is null)
            return Result<TeacherConfigurationDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        var config = await configRepo.FindAsync(c => c.TeacherId == teacherId);
        if (config is null)
            return Result<TeacherConfigurationDto>.Failure(_localizer, "ConfigurationNotFound", HttpStatusCode.NotFound);

        if (dto.IsProratedPaymentEnabled && dto.ProratedTiers.Count == 0)
            return Result<TeacherConfigurationDto>.Failure(_localizer, "ProratedTiersRequired", HttpStatusCode.BadRequest);

        if (dto.ProratedTiers.Count > 3)
            return Result<TeacherConfigurationDto>.Failure(_localizer, "MaxThreeProratedTiers", HttpStatusCode.BadRequest);

        // ── Transaction-safe ──
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync();

        try
        {
            if (dto.StudentCapacityPackageId.HasValue)
            {
                var packageRepo = _unitOfWork.GetRepository<StudentCapacityPackage, long>();
                var package = await packageRepo.FindAsync(p => p.Id == dto.StudentCapacityPackageId.Value && p.IsActive);
                if (package is null)
                    return Result<TeacherConfigurationDto>.Failure(_localizer, "InvalidCapacityPackage", HttpStatusCode.BadRequest);

                teacher.StudentCapacityPackageId = dto.StudentCapacityPackageId;
                // Auto-update StudentCapacity from the selected package tier
                // MaxStudents is null for the "3000+" tier — use int.MaxValue as effective capacity
                teacher.StudentCapacity = package.MaxStudents ?? int.MaxValue;
                await teacherRepo.UpdateAsync(teacher);
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
            config.ParentVisibilityAttendance = dto.ParentVisibilityAttendance;
            config.ParentVisibilityPayment = dto.ParentVisibilityPayment;
            config.ParentVisibilityHomework = dto.ParentVisibilityHomework;
            config.ParentVisibilityExamDefault = dto.ParentVisibilityExamDefault;
            config.UpdatedAt = DateTime.UtcNow;

            await configRepo.UpdateAsync(config);

            // Replace prorated tiers: delete existing, add new
            var existingTiers = await proratedTierRepo.GetAsync(pt => pt.TeacherConfigurationId == config.Id);
            if (existingTiers.Any())
                await proratedTierRepo.DeleteRangeAsync(existingTiers);

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
                await proratedTierRepo.AddAsync(tier);
            }

            // Mark configuration as completed
            if (!teacher.IsConfigurationCompleted)
            {
                teacher.IsConfigurationCompleted = true;
                await teacherRepo.UpdateAsync(teacher);
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
        var configRepo = _unitOfWork.GetRepository<TeacherConfiguration, long>();
        var proratedTierRepo = _unitOfWork.GetRepository<TeacherProratedTier, long>();

        var config = await configRepo.FindAsync(c => c.TeacherId == teacherId);
        if (config is null)
            return Result<TeacherConfigurationDto>.Failure(_localizer, "ConfigurationNotFound", HttpStatusCode.NotFound);

        var tiers = await proratedTierRepo.GetAsync(pt => pt.TeacherConfigurationId == config.Id);

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
            ParentVisibilityAttendance = config.ParentVisibilityAttendance,
            ParentVisibilityPayment = config.ParentVisibilityPayment,
            ParentVisibilityHomework = config.ParentVisibilityHomework,
            ParentVisibilityExamDefault = config.ParentVisibilityExamDefault,
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
        var subjectRepo = _unitOfWork.GetRepository<Subject, long>();
        var subjects = await subjectRepo.GetAsync(s => s.IsActive);

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
        var packageRepo = _unitOfWork.GetRepository<StudentCapacityPackage, long>();
        var packages = await packageRepo.GetAsync(p => p.IsActive);

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
    public async Task<Result<TeacherSubscriptionDto?>> GetActiveSubscriptionAsync(long teacherId)
    {
        var subscriptionRepo = _unitOfWork.GetRepository<TeacherSubscription, long>();

        // Find the most recent active or expiring-soon subscription
        var subscriptions = await subscriptionRepo.GetAsync(s =>
            s.TeacherId == teacherId &&
            (s.SubscriptionStatus == SubscriptionStatus.Active || s.SubscriptionStatus == SubscriptionStatus.ExpiringSoon));

        var activeSubscription = subscriptions
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefault();

        if (activeSubscription is null)
            return Result<TeacherSubscriptionDto?>.Success(null, _localizer, "NoActiveSubscription", HttpStatusCode.OK);

        var dto = new TeacherSubscriptionDto
        {
            Id = activeSubscription.Id,
            SubscriptionStatus = activeSubscription.SubscriptionStatus.ToString(),
            StartDate = activeSubscription.StartDate,
            EndDate = activeSubscription.EndDate,
            DaysRemaining = Math.Max(0, (activeSubscription.EndDate.Date - DateTime.UtcNow.Date).Days)
        };

        return Result<TeacherSubscriptionDto?>.Success(dto, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<TeacherListItemDto>>>> GetTeachersAsync(
        PaginatedRequest request,
        string? accountStatus = null,
        string? subscriptionStatus = null)
    {
        var teacherRepo = _unitOfWork.GetRepository<Teacher, long>();
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var subscriptionRepo = _unitOfWork.GetRepository<TeacherSubscription, long>();
        var teacherSubjectRepo = _unitOfWork.GetRepository<TeacherSubject, long>();
        var subjectRepo = _unitOfWork.GetRepository<Subject, long>();

        // ── 1. Load all base data ──────────────────────────────────────────────
        var allTeachers = await teacherRepo.GetAsync(t => true);
        var allUsers = await userRepo.GetAsync(u => true);
        var allSubscriptions = await subscriptionRepo.GetAsync(s => true);
        var allTeacherSubjects = await teacherSubjectRepo.GetAsync(ts => true);
        var allSubjects = await subjectRepo.GetAsync(s => true);

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

                // Get latest subscription
                var latestSub = allSubscriptions
                    .Where(s => s.TeacherId == teacher.Id)
                    .OrderByDescending(s => s.EndDate)
                    .FirstOrDefault();

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
        if (!string.IsNullOrWhiteSpace(subscriptionStatus) &&
            Enum.TryParse<SubscriptionStatus>(subscriptionStatus, true, out var parsedSubStatus))
        {
            joined = joined
                .Where(x => x.LatestSub != null && x.LatestSub.SubscriptionStatus == parsedSubStatus)
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
        var items = paged.Select(x => new TeacherListItemDto
        {
            Id = x.Teacher.Id,
            FullName = x.User?.FullName ?? string.Empty,
            Username = x.User?.Username ?? string.Empty,
            TeacherCode = x.Teacher.TeacherCode,
            PhoneNumber = x.User?.PhoneNumber,
            StudentCapacity = x.Teacher.StudentCapacity,
            AccountStatus = x.Teacher.AccountStatus.ToString(),
            IsConfigurationCompleted = x.Teacher.IsConfigurationCompleted,
            SubscriptionStatus = x.LatestSub?.SubscriptionStatus.ToString(),
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
    /// </summary>
    private async Task<TeacherProfileDto> BuildTeacherProfileAsync(long teacherId)
    {
        var teacherRepo = _unitOfWork.GetRepository<Teacher, long>();
        var userRepo = _unitOfWork.GetRepository<User, long>();
        var teacherSubjectRepo = _unitOfWork.GetRepository<TeacherSubject, long>();
        var subjectRepo = _unitOfWork.GetRepository<Subject, long>();
        var packageRepo = _unitOfWork.GetRepository<StudentCapacityPackage, long>();

        var teacher = await teacherRepo.FindAsync(t => t.Id == teacherId);
        var user = await userRepo.FindAsync(u => u.Id == teacher!.UserId);
        var teacherSubjects = await teacherSubjectRepo.GetAsync(ts => ts.TeacherId == teacherId);

        var subjects = new List<SubjectDto>();
        foreach (var ts in teacherSubjects)
        {
            var subject = await subjectRepo.FindAsync(s => s.Id == ts.SubjectId);
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
            var package = await packageRepo.FindAsync(p => p.Id == teacher.StudentCapacityPackageId.Value);
            packageName = package?.Name;
        }

        // Get active subscription
        var subscriptionResult = await GetActiveSubscriptionAsync(teacherId);

        return new TeacherProfileDto
        {
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
            ActiveSubscription = subscriptionResult.Data
        };
    }
}