using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Teacher;

namespace Edvanz.Application.ServiceContract;

// If Result<T> is not resolving, verify that Edvanz.Application.Dtos.Result<T> exists
// and that Microsoft.Extensions.Localization NuGet package is restored.

/// <summary>
/// Defines the contract for Teacher module operations.
/// Called by the User module after routing based on UserType.
/// All methods are async per system architecture requirements.
/// Lives in the Application layer because it depends on Application DTOs.
/// </summary>
public interface ITeacherService
{
    /// <summary>
    /// Initializes the Teacher record, generates the TeacherCode, and creates default configuration.
    /// Called after the User record is created during registration.
    /// AAM-FR-03.3: Auto-generates unique 8-digit TeacherCode.
    /// AAM-FR-03.5: Associates subject(s) with the teacher.
    /// AAM-BR-04 / AAM-NFR-05: Creates TeacherConfiguration with system defaults.
    /// </summary>
    /// <param name="dto">Registration data including userId, subject selection, and optional custom subject.</param>
    /// <returns>Result containing the created teacher's profile including the generated TeacherCode.</returns>
    Task<Result<TeacherProfileDto>> InitializeTeacherAsync(CreateTeacherDto dto);

    /// <summary>
    /// Retrieves the full teacher profile including configuration status and subscription info.
    /// </summary>
    /// <param name="teacherId">The Teacher's Id (not the UserId).</param>
    /// <returns>Result containing the teacher profile DTO.</returns>
    Task<Result<TeacherProfileDto>> GetTeacherProfileAsync(long teacherId);

    /// <summary>
    /// Updates the teacher's own profile information.
    /// AAM-FR-02.3: Language preference changeable from settings.
    /// AAM-FR-03.4: Full name in Arabic or English.
    /// AAM-FR-03.5: Subject selection updatable.
    /// AAM-BR-05: TeacherCode is NOT updatable (immutable).
    /// </summary>
    /// <param name="teacherId">The Teacher's Id.</param>
    /// <param name="dto">Profile fields to update.</param>
    /// <returns>Result containing the updated teacher profile.</returns>
    Task<Result<TeacherProfileDto>> UpdateTeacherProfileAsync(long teacherId, UpdateTeacherProfileDto dto);

    /// <summary>
    /// Retrieves a teacher by their unique 8-digit TeacherCode.
    /// Used by students when adding a teacher (AAM-FR-05.5).
    /// </summary>
    /// <param name="teacherCode">The unique 8-digit teacher code.</param>
    /// <returns>Result containing basic teacher info (name, subject) if found.</returns>
    Task<Result<TeacherPublicInfoDto>> GetTeacherByCodeAsync(string teacherCode);

    /// <summary>
    /// Saves or updates all teacher configuration settings.
    /// AAM-FR-04.2 through 04.9: Student code generation, session naming, prorated payment,
    /// alert thresholds, barcode display, and visibility settings.
    /// AAM-FR-03.6: Accessible from account settings at any time after registration.
    /// </summary>
    /// <param name="teacherId">The Teacher's Id.</param>
    /// <param name="dto">Configuration data to apply.</param>
    /// <returns>Result containing the updated configuration DTO.</returns>
    Task<Result<TeacherConfigurationDto>> SaveConfigurationAsync(long teacherId, UpdateTeacherConfigurationDto dto);

    /// <summary>
    /// Retrieves the current teacher configuration.
    /// </summary>
    /// <param name="teacherId">The Teacher's Id.</param>
    /// <returns>Result containing the current configuration DTO.</returns>
    Task<Result<TeacherConfigurationDto>> GetConfigurationAsync(long teacherId);

    /// <summary>
    /// Retrieves all active subjects available for selection during registration.
    /// AAM-FR-03.5: Includes all Egyptian Ministry of Education subjects.
    /// </summary>
    /// <returns>Result containing the list of active subjects.</returns>
    Task<Result<IReadOnlyList<SubjectDto>>> GetAvailableSubjectsAsync();

    /// <summary>
    /// Retrieves all active student capacity packages.
    /// AAM-FR-04.1: 7 tiers from "Up to 300" through "3000+".
    /// </summary>
    /// <returns>Result containing the list of active capacity packages.</returns>
    Task<Result<IReadOnlyList<StudentCapacityPackageDto>>> GetCapacityPackagesAsync();

    /// <summary>
    /// Retrieves the teacher's current active subscription.
    /// REQ-SUB-003: Shows start date, end date, days remaining, and status.
    /// </summary>
    /// <param name="teacherId">The Teacher's Id.</param>
    /// <returns>Result containing subscription details, or null data if no active subscription exists.</returns>
    Task<Result<TeacherSubscriptionDto?>> GetActiveSubscriptionAsync(long teacherId);

    /// <summary>
    /// Retrieves a paginated list of teachers with search, sort, and filter support.
    /// REQ-ADM-026 through 028: Super Admin dashboard teacher list with
    /// searching by name/username, sorting by name/subscription/student count,
    /// and filtering by subscription status and account status.
    /// </summary>
    /// <param name="request">Pagination, search, and sort parameters.</param>
    /// <param name="accountStatus">Optional filter by account status (Active, Inactive, Suspended).</param>
    /// <param name="subscriptionStatus">Optional filter by subscription status (Active, ExpiringSoon, Expired).</param>
    /// <returns>Result containing paginated teacher list.</returns>
    Task<Result<PaginatedResponse<List<TeacherListItemDto>>>> GetTeachersAsync(
        PaginatedRequest request,
        string? accountStatus = null,
        string? subscriptionStatus = null);
}