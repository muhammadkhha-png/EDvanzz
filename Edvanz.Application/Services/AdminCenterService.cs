using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Center;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Edvanz.Domain.ServiceContract;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// SuperAdmin provisioning of Center accounts. Creating a center creates a login <see cref="User"/>
/// (UserType.Center, IsActive=true), a <see cref="Center"/> row with an auto 8-digit code, and the
/// default revenue-share %. Mirrors the teacher-creation transaction discipline.
/// </summary>
public class AdminCenterService : IAdminCenterService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly IPasswordService _passwordService;
    private readonly ICenterCodeGenerator _centerCodeGenerator;

    public AdminCenterService(
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer,
        IPasswordService passwordService,
        ICenterCodeGenerator centerCodeGenerator)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _passwordService = passwordService;
        _centerCodeGenerator = centerCodeGenerator;
    }

    /// <inheritdoc />
    public async Task<Result<CenterListItemDto>> CreateCenterAsync(long adminUserId, CreateCenterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<CenterListItemDto>.Failure(_localizer, "CenterNameRequired");
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            return Result<CenterListItemDto>.Failure(_localizer, "InvalidCredentials");

        var existing = await _unitOfWork.Users.FindExistingUserByCredentialsAsync(
            dto.PhoneNumber ?? string.Empty, dto.Username, dto.Email);
        if (existing != null)
        {
            if (existing.Username == dto.Username)
                return Result<CenterListItemDto>.Failure(_localizer, "repeatedUserName");
            if (!string.IsNullOrEmpty(dto.PhoneNumber) && existing.PhoneNumber == dto.PhoneNumber)
                return Result<CenterListItemDto>.Failure(_localizer, "repeatedPhoneNumber");
            if (!string.IsNullOrEmpty(dto.Email) && existing.Email == dto.Email)
                return Result<CenterListItemDto>.Failure(_localizer, "repeatedEmail");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var user = new User
            {
                UserType = UserType.Center,
                FullName = string.IsNullOrWhiteSpace(dto.FullName) ? dto.Name : dto.FullName,
                Username = dto.Username,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                PasswordHashed = _passwordService.HashPassword(dto.Password),
                IsActive = true,
                CreateAt = DateTime.UtcNow,
                CreateByUserId = adminUserId
            };
            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var code = await _centerCodeGenerator.GenerateUniqueCodeAsync();

            var center = new Center
            {
                UserId = user.Id,
                Name = dto.Name.Trim(),
                CenterCode = code,
                // The center sets its own revenue-share % later (PUT /api/center/settings). Start at 0.
                DefaultRevenueSharePercent = 0m,
                LanguagePreference = dto.LanguagePreference,
                AccountStatus = AccountStatus.Active,
                CreatedByUserId = adminUserId,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.Centers.AddAsync(center);
            await _unitOfWork.SaveChangesAsync();

            // Seed the center's DEFAULT configuration (+ the 3 default prorated tiers) up front, exactly
            // like a teacher gets one at InitializeTeacherAsync. This guarantees every center has a config
            // row from creation, so the settings/apply paths never have to lazy-create one (removing the
            // only concurrent-first-touch race on IX_CenterConfigurations_CenterId). Same tenant tx.
            var config = new CenterConfiguration
            {
                CenterId = center.Id,
                StudentCodeGenerationMode = center.StudentCodeGenerationMode,
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
                StudentVisibilityExamDefault = true,
                StudentVisibilityOnlineExamDefault = true,
                StudentVisibilityVideo = true,
                ParentVisibilityAttendance = true,
                ParentVisibilityPayment = true,
                ParentVisibilityHomework = true,
                ParentVisibilityExamDefault = false,
                ParentVisibilityOnlineExamDefault = false,
                ParentVisibilityVideo = true,
                IsDeviceLockEnabled = false,
                ShowPaymentInfoOnAttendanceScreen = true,
                ShowAttendanceHistoryOnAttendanceScreen = true,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.Centers.AddConfigurationAsync(config);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.Centers.AddProratedTiersAsync(new List<CenterProratedTier>
            {
                new() { CenterConfigurationId = config.Id, TierNumber = 1, ThresholdDayStart = 1, ThresholdDayEnd = 10, FractionRate = 1.0000m, CreateAt = DateTime.UtcNow },
                new() { CenterConfigurationId = config.Id, TierNumber = 2, ThresholdDayStart = 11, ThresholdDayEnd = 20, FractionRate = 0.6667m, CreateAt = DateTime.UtcNow },
                new() { CenterConfigurationId = config.Id, TierNumber = 3, ThresholdDayStart = 21, ThresholdDayEnd = 31, FractionRate = 0.3333m, CreateAt = DateTime.UtcNow }
            });
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            return Result<CenterListItemDto>.Success(ToListItem(center, user, 0, 0, 0, 0), _localizer, "CenterCreated");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<CenterListItemDto>.Failure(_localizer, "ServerError");
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<CenterListItemDto>>> GetCentersAsync()
    {
        var centers = await _unitOfWork.Centers.GetAsync(c => c.DeletedAt == null);

        var list = new List<CenterListItemDto>();
        foreach (var c in centers.OrderByDescending(c => c.Id))
        {
            var full = await _unitOfWork.Centers.CountActiveTeachersByPlanAsync(c.Id, SubscriptionPlanType.Full);
            var managerial = await _unitOfWork.Centers.CountActiveTeachersByPlanAsync(c.Id, SubscriptionPlanType.Managerial);
            var managerialPlus = await _unitOfWork.Centers.CountActiveTeachersByPlanAsync(c.Id, SubscriptionPlanType.ManagerialPlus);
            // The login User carries the username + last-login/last-activity surfaced by the dashboard.
            var user = await _unitOfWork.Users.GetUserByIdAsync(c.UserId);
            list.Add(ToListItem(c, user, full + managerial + managerialPlus, full, managerial, managerialPlus));
        }

        return Result<List<CenterListItemDto>>.Success(list, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<CenterListItemDto>> GetCenterByIdAsync(long centerId)
    {
        var center = await _unitOfWork.Centers.GetCenterByIdAsync(centerId);
        if (center == null)
            return Result<CenterListItemDto>.Failure(_localizer, "CenterNotFound", HttpStatusCode.NotFound);

        var full = await _unitOfWork.Centers.CountActiveTeachersByPlanAsync(centerId, SubscriptionPlanType.Full);
        var managerial = await _unitOfWork.Centers.CountActiveTeachersByPlanAsync(centerId, SubscriptionPlanType.Managerial);
        var managerialPlus = await _unitOfWork.Centers.CountActiveTeachersByPlanAsync(centerId, SubscriptionPlanType.ManagerialPlus);
        var user = await _unitOfWork.Users.GetUserByIdAsync(center.UserId);
        return Result<CenterListItemDto>.Success(
            ToListItem(center, user, full + managerial + managerialPlus, full, managerial, managerialPlus),
            _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<string>> DeactivateCenterAsync(long adminUserId, long centerId)
    {
        var center = await _unitOfWork.Centers.GetCenterByIdAsync(centerId);
        if (center == null)
            return Result<string>.Failure(_localizer, "CenterNotFound", HttpStatusCode.NotFound);

        var user = await _unitOfWork.Users.GetUserByIdAsync(center.UserId);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            center.AccountStatus = AccountStatus.Inactive;
            center.DeactivatedAt = DateTime.UtcNow;
            if (user != null) user.IsActive = false; // blocks the center login
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            return Result<string>.Success("ok", _localizer, "CenterDeactivated");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<string>.Failure(_localizer, "ServerError");
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> ReactivateCenterAsync(long adminUserId, long centerId)
    {
        var center = await _unitOfWork.Centers.GetCenterByIdAsync(centerId);
        if (center == null)
            return Result<string>.Failure(_localizer, "CenterNotFound", HttpStatusCode.NotFound);

        var user = await _unitOfWork.Users.GetUserByIdAsync(center.UserId);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            center.AccountStatus = AccountStatus.Active;
            center.DeactivatedAt = null;
            if (user != null) user.IsActive = true; // restores the center login
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

    private static CenterListItemDto ToListItem(
        Center c, User? user, int total, int full, int managerial, int managerialPlus) => new()
    {
        CenterId = c.Id,
        Name = c.Name,
        CenterCode = c.CenterCode,
        DefaultRevenueSharePercent = c.DefaultRevenueSharePercent,
        AccountStatus = c.AccountStatus,
        TeacherCount = total,
        FullTeacherCount = full,
        ManagerialTeacherCount = managerial,
        ManagerialPlusTeacherCount = managerialPlus,
        CreatedAt = c.CreateAt,
        UserId = c.UserId,
        Username = user?.Username,
        LastLoginAt = user?.LastLoginAt,
        LastActivityAt = user?.LastActivityAt
    };
}
