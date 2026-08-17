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
            await _unitOfWork.CommitAsync();

            return Result<CenterListItemDto>.Success(ToListItem(center, 0, 0, 0), _localizer, "CenterCreated");
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
            list.Add(ToListItem(c, full + managerial, full, managerial));
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
        return Result<CenterListItemDto>.Success(ToListItem(center, full + managerial, full, managerial), _localizer, "Success");
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

    private static CenterListItemDto ToListItem(Center c, int total, int full, int managerial) => new()
    {
        CenterId = c.Id,
        Name = c.Name,
        CenterCode = c.CenterCode,
        DefaultRevenueSharePercent = c.DefaultRevenueSharePercent,
        AccountStatus = c.AccountStatus,
        TeacherCount = total,
        FullTeacherCount = full,
        ManagerialTeacherCount = managerial,
        CreatedAt = c.CreateAt
    };
}
