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
            await _unitOfWork.CommitAsync();

            // Build response
            var profile = await BuildTeacherProfileAsync(teacher.Id);
            return Result<TeacherProfileDto>.Success(profile, _localizer, "TeacherInitializedSuccess", HttpStatusCode.Created);
        }
        catch
        {
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
                subjectName = firstSubject.NameEn; // Caller should determine language preference
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

        // Validate prorated tiers
        if (dto.IsProratedPaymentEnabled && dto.ProratedTiers.Count == 0)
            return Result<TeacherConfigurationDto>.Failure(_localizer, "ProratedTiersRequired", HttpStatusCode.BadRequest);

        if (dto.ProratedTiers.Count > 3)
            return Result<TeacherConfigurationDto>.Failure(_localizer, "MaxThreeProratedTiers", HttpStatusCode.BadRequest);

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Update capacity package on Teacher if provided
            if (dto.StudentCapacityPackageId.HasValue)
            {
                var packageRepo = _unitOfWork.GetRepository<StudentCapacityPackage, long>();
                var package = await packageRepo.FindAsync(p => p.Id == dto.StudentCapacityPackageId.Value && p.IsActive);
                if (package is null)
                    return Result<TeacherConfigurationDto>.Failure(_localizer, "InvalidCapacityPackage", HttpStatusCode.BadRequest);

                teacher.StudentCapacityPackageId = dto.StudentCapacityPackageId;
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
            await _unitOfWork.CommitAsync();

            // Build response with save-specific success message
            var configResult = await GetConfigurationAsync(teacherId);
            if (configResult.IsSuccess && configResult.Data is not null)
                return Result<TeacherConfigurationDto>.Success(configResult.Data, _localizer, "ConfigurationSavedSuccess", HttpStatusCode.OK);

            return configResult;
        }
        catch
        {
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

        // Build filtered query using IQueryable (no EF Core dependency needed)
        var query = teacherRepo.GetQueryable();

        // Filter by account status
        if (!string.IsNullOrWhiteSpace(accountStatus) &&
            Enum.TryParse<AccountStatus>(accountStatus, true, out var parsedAccountStatus))
        {
            query = query.Where(t => t.AccountStatus == parsedAccountStatus);
        }

        // Search by teacher code (name/username search done post-query via User table)
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(t => t.TeacherCode.Contains(search));
        }

        // Sort
        query = request.SortBy?.ToLower() switch
        {
            "capacity" => request.IsDescending
                ? query.OrderByDescending(t => t.StudentCapacity)
                : query.OrderBy(t => t.StudentCapacity),
            "createdat" => request.IsDescending
                ? query.OrderByDescending(t => t.CreateAt)
                : query.OrderBy(t => t.CreateAt),
            "code" => request.IsDescending
                ? query.OrderByDescending(t => t.TeacherCode)
                : query.OrderBy(t => t.TeacherCode),
            _ => query.OrderByDescending(t => t.CreateAt)
        };

        // Get total count and paginated data through repository
        var totalCount = await teacherRepo.CountAsync(
            string.IsNullOrWhiteSpace(accountStatus)
                ? null
                : t => t.AccountStatus == parsedAccountStatus);

        var teachers = await teacherRepo.GetPagedAsync(query, request.Page, request.PageSize);

        // Build DTOs with user info and subscription
        var items = new List<TeacherListItemDto>();
        foreach (var teacher in teachers)
        {
            var user = await userRepo.FindAsync(u => u.Id == teacher.UserId);

            // Name/username search filter (applied post-query because User is a separate table)
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim().ToLower();
                bool matchesTeacherCode = teacher.TeacherCode.ToLower().Contains(search);
                bool matchesUser = user is not null &&
                    (user.FullName.ToLower().Contains(search) ||
                     user.Username.ToLower().Contains(search));

                if (!matchesTeacherCode && !matchesUser)
                    continue;
            }

            // Get latest subscription for this teacher
            var teacherSubs = await subscriptionRepo.GetAsync(s => s.TeacherId == teacher.Id);
            var latestSub = teacherSubs
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefault();

            // Filter by subscription status if requested
            if (!string.IsNullOrWhiteSpace(subscriptionStatus) &&
                Enum.TryParse<SubscriptionStatus>(subscriptionStatus, true, out var parsedSubStatus))
            {
                if (latestSub is null || latestSub.SubscriptionStatus != parsedSubStatus)
                    continue;
            }

            items.Add(new TeacherListItemDto
            {
                Id = teacher.Id,
                FullName = user?.FullName ?? string.Empty,
                Username = user?.Username ?? string.Empty,
                TeacherCode = teacher.TeacherCode,
                PhoneNumber = user?.PhoneNumber,
                StudentCapacity = teacher.StudentCapacity,
                AccountStatus = teacher.AccountStatus.ToString(),
                IsConfigurationCompleted = teacher.IsConfigurationCompleted,
                SubscriptionStatus = latestSub?.SubscriptionStatus.ToString(),
                SubscriptionEndDate = latestSub?.EndDate,
                CreatedAt = teacher.CreateAt
            });
        }

        var response = new PaginatedResponse<List<TeacherListItemDto>>
        {
            totalCount = totalCount,
            page = request.Page,
            pageSize = request.PageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
            data = items
        };

        return Result<PaginatedResponse<List<TeacherListItemDto>>>.Success(response, _localizer, "Success", HttpStatusCode.OK);
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