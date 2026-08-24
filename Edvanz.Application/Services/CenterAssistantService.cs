using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Center;
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
/// Center management of its assistants. A center assistant is a real login (UserType.CenterAssistant)
/// that operates ALL the center's teachers by "acting as" one per request. Kept separate from the
/// teacher-owned Assistant. Granular per-assistant permissions are deferred (v1: role-sufficient).
/// </summary>
public class CenterAssistantService : ICenterAssistantService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly IPasswordService _passwordService;
    private readonly IservicesContract.IUserAuthInvalidationService _authInvalidation;

    public CenterAssistantService(
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer,
        IPasswordService passwordService,
        IservicesContract.IUserAuthInvalidationService authInvalidation)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _passwordService = passwordService;
        _authInvalidation = authInvalidation;
    }

    /// <inheritdoc />
    public async Task<Result<CenterAssistantListItemDto>> CreateAsync(long centerId, long actingUserId, CreateCenterAssistantDto dto)
    {
        var center = await _unitOfWork.Centers.GetCenterByIdAsync(centerId);
        if (center == null)
            return Result<CenterAssistantListItemDto>.Failure(_localizer, "CenterNotFound", HttpStatusCode.NotFound);
        if (string.IsNullOrWhiteSpace(dto.FullName))
            return Result<CenterAssistantListItemDto>.Failure(_localizer, "AssistantNameRequired");
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            return Result<CenterAssistantListItemDto>.Failure(_localizer, "InvalidCredentials");

        var existing = await _unitOfWork.Users.FindExistingUserByCredentialsAsync(
            dto.PhoneNumber ?? string.Empty, dto.Username, dto.Email);
        if (existing != null)
        {
            if (existing.Username == dto.Username)
                return Result<CenterAssistantListItemDto>.Failure(_localizer, "repeatedUserName");
            if (!string.IsNullOrEmpty(dto.PhoneNumber) && existing.PhoneNumber == dto.PhoneNumber)
                return Result<CenterAssistantListItemDto>.Failure(_localizer, "repeatedPhoneNumber");
            if (!string.IsNullOrEmpty(dto.Email) && existing.Email == dto.Email)
                return Result<CenterAssistantListItemDto>.Failure(_localizer, "repeatedEmail");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var user = new User
            {
                UserType = UserType.CenterAssistant,
                FullName = dto.FullName.Trim(),
                Username = dto.Username,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                PasswordHashed = _passwordService.HashPassword(dto.Password),
                IsActive = true,
                CreateAt = DateTime.UtcNow,
                CreateByUserId = actingUserId
            };
            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var assistant = new CenterAssistant
            {
                UserId = user.Id,
                CenterId = centerId,
                LanguagePreference = dto.LanguagePreference,
                AccountStatus = AccountStatus.Active,
                UpdatedAt = DateTime.UtcNow,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.GetRepository<CenterAssistant, long>().AddAsync(assistant);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            return Result<CenterAssistantListItemDto>.Success(ToItem(assistant, user), _localizer, "CenterAssistantCreated");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<CenterAssistantListItemDto>.Failure(_localizer, "ServerError");
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<CenterAssistantListItemDto>>> GetAssistantsAsync(long centerId)
    {
        var assistants = await _unitOfWork.Centers.GetCenterAssistantsByCenterAsync(centerId);
        var list = assistants.Select(a => ToItem(a, a.User)).ToList();
        return Result<List<CenterAssistantListItemDto>>.Success(list, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<string>> DeactivateAsync(long centerId, long centerAssistantId)
        => await SetActiveAsync(centerId, centerAssistantId, active: false, "CenterAssistantDeactivated");

    /// <inheritdoc />
    public async Task<Result<string>> ReactivateAsync(long centerId, long centerAssistantId)
        => await SetActiveAsync(centerId, centerAssistantId, active: true, "Success");

    private async Task<Result<string>> SetActiveAsync(long centerId, long centerAssistantId, bool active, string successKey)
    {
        var assistant = await _unitOfWork.Centers.GetCenterAssistantByIdAsync(centerAssistantId);
        if (assistant == null || assistant.CenterId != centerId)
            return Result<string>.Failure(_localizer, "AssistantNotFound", HttpStatusCode.NotFound);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            assistant.AccountStatus = active ? AccountStatus.Active : AccountStatus.Inactive;
            assistant.DeactivatedAt = active ? null : DateTime.UtcNow;
            assistant.UpdatedAt = DateTime.UtcNow;
            if (assistant.User != null) assistant.User.IsActive = active; // toggles the login
            // Deactivation must also end the assistant's LIVE sessions (stamp bump joins the
            // transaction — see the SecurityStamp rule); without it the cached auth snapshot kept a
            // deactivated assistant working until cache expiry.
            if (!active && assistant.User != null)
                await _authInvalidation.InvalidateUserAsync(assistant.User.Id);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            return Result<string>.Success("ok", _localizer, successKey);
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<string>.Failure(_localizer, "ServerError");
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> ResetPasswordAsync(long centerId, long centerAssistantId, ResetCenterAssistantPasswordDto dto)
    {
        if (!string.Equals(dto.NewPassword, dto.ConfirmPassword, StringComparison.Ordinal))
            return Result<string>.Failure(_localizer, "PasswordConfirmationMismatch");
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
            return Result<string>.Failure(_localizer, "PasswordTooShort");

        var assistant = await _unitOfWork.Centers.GetCenterAssistantByIdAsync(centerAssistantId);
        if (assistant == null || assistant.CenterId != centerId)
            return Result<string>.Failure(_localizer, "AssistantNotFound", HttpStatusCode.NotFound);

        var user = assistant.User ?? await _unitOfWork.Users.GetUserByIdAsync(assistant.UserId);
        if (user == null)
            return Result<string>.Failure(_localizer, "ServerError");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            user.PasswordHashed = _passwordService.HashPassword(dto.NewPassword);
            user.SecurityStamp = Guid.NewGuid().ToString();
            // A center-driven reset always revokes the assistant's live sessions (mirrors the
            // center-teacher reset), so a shared/old device is signed out.
            var tokens = _unitOfWork.RefreshTokenRepo.GetByUserId(user.Id);
            await _unitOfWork.GetRepository<RefreshToken, long>().DeleteRangeAsync(tokens);
            await _authInvalidation.InvalidateUserAsync(user.Id);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            return Result<string>.Success("ok", _localizer, "CenterAssistantPasswordReset");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            return Result<string>.Failure(_localizer, "ServerError");
        }
    }

    private static CenterAssistantListItemDto ToItem(CenterAssistant a, User? user) => new()
    {
        CenterAssistantId = a.Id,
        UserId = a.UserId,
        FullName = user?.FullName ?? string.Empty,
        Username = user?.Username ?? string.Empty,
        PhoneNumber = user?.PhoneNumber,
        AccountStatus = a.AccountStatus
    };
}
