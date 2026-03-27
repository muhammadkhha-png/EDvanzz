using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Teacher;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// API endpoints for the Teacher module.
/// Handles teacher initialization, profile retrieval, configuration management,
/// and lookup data for subjects and capacity packages.
/// 
/// Note: Registration, login, password update, and deletion endpoints live in the
/// User module controller. That controller routes to ITeacherService.InitializeTeacherAsync
/// after creating the User record when UserType is Teacher.
/// </summary>
public class TeacherController : ApiBaseController
{
    private readonly ITeacherService _teacherService;

    public TeacherController(ITeacherService teacherService)
    {
        _teacherService = teacherService;
    }

    // ══════════════════════════════════════════════════
    // TEACHER PROFILE
    // ══════════════════════════════════════════════════

    /// <summary>
    /// Initializes a Teacher record after user registration.
    /// Creates the teacher, generates the unique 8-digit TeacherCode,
    /// links subjects, and creates default configuration.
    /// Called by the User module after creating the User record with UserType = Teacher.
    /// </summary>
    /// <remarks>
    /// AAM-FR-03.3: Auto-generates unique 8-digit TeacherCode.
    /// AAM-FR-03.5: Associates subject(s) with the teacher.
    /// AAM-BR-04 / AAM-NFR-05: Creates TeacherConfiguration with system defaults.
    /// </remarks>
    /// <param name="dto">Teacher initialization data.</param>
    /// <returns>The created teacher profile including the generated TeacherCode.</returns>
    [HttpPost("initialize")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InitializeTeacher([FromBody] CreateTeacherDto dto)
    {
        var result = await _teacherService.InitializeTeacherAsync(dto);
        return ToResponse(result);
    }

    /// <summary>
    /// Retrieves the full teacher profile including subjects, capacity package, and active subscription.
    /// </summary>
    /// <param name="teacherId">The Teacher's Id (from the Teachers table, not the UserId).</param>
    /// <returns>The complete teacher profile.</returns>
    [HttpGet("{teacherId:long}/profile")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTeacherProfile([FromRoute] long teacherId)
    {
        var result = await _teacherService.GetTeacherProfileAsync(teacherId);
        return ToResponse(result);
    }

    /// <summary>
    /// Retrieves minimal teacher info by their unique 8-digit TeacherCode.
    /// Used by students when adding a teacher to their account.
    /// Returns only the teacher's name and subject — no sensitive data exposed.
    /// </summary>
    /// <remarks>
    /// AAM-FR-05.5: Students provide TeacherCode to link to a teacher.
    /// AAM-FR-05.6: Displays teacher's full name and subject on student dashboard.
    /// </remarks>
    /// <param name="teacherCode">The unique 8-digit teacher code.</param>
    /// <returns>Teacher's public info (name and subject).</returns>
    [HttpGet("by-code/{teacherCode}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTeacherByCode([FromRoute] string teacherCode)
    {
        var result = await _teacherService.GetTeacherByCodeAsync(teacherCode);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════
    // TEACHER CONFIGURATION (AAM-FR-04)
    // ══════════════════════════════════════════════════

    /// <summary>
    /// Retrieves the current teacher configuration settings.
    /// </summary>
    /// <param name="teacherId">The Teacher's Id.</param>
    /// <returns>All current configuration values.</returns>
    [HttpGet("{teacherId:long}/configuration")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConfiguration([FromRoute] long teacherId)
    {
        var result = await _teacherService.GetConfigurationAsync(teacherId);
        return ToResponse(result);
    }

    /// <summary>
    /// Saves or updates all teacher configuration settings.
    /// Covers student code generation, session naming, prorated payment tiers,
    /// alert thresholds, barcode display, and student/parent visibility.
    /// Marks IsConfigurationCompleted = true on first save.
    /// </summary>
    /// <remarks>
    /// AAM-FR-04.2 through 04.9: All configuration settings.
    /// AAM-FR-03.6: Accessible from account settings at any time.
    /// AAM-NFR-05: Skippable during registration; defaults apply.
    /// </remarks>
    /// <param name="teacherId">The Teacher's Id.</param>
    /// <param name="dto">Configuration data to apply.</param>
    /// <returns>The updated configuration values.</returns>
    [HttpPut("{teacherId:long}/configuration")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveConfiguration(
        [FromRoute] long teacherId,
        [FromBody] UpdateTeacherConfigurationDto dto)
    {
        var result = await _teacherService.SaveConfigurationAsync(teacherId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════
    // SUBSCRIPTION
    // ══════════════════════════════════════════════════

    /// <summary>
    /// Retrieves the teacher's current active subscription details.
    /// Returns start date, end date, days remaining, and status.
    /// Returns null data (with success = true) if no active subscription exists.
    /// </summary>
    /// <remarks>
    /// REQ-SUB-003: Displays subscription info in the teacher's account settings.
    /// </remarks>
    /// <param name="teacherId">The Teacher's Id.</param>
    /// <returns>Active subscription details, or null if none.</returns>
    [HttpGet("{teacherId:long}/subscription")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveSubscription([FromRoute] long teacherId)
    {
        var result = await _teacherService.GetActiveSubscriptionAsync(teacherId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════
    // LOOKUP DATA
    // ══════════════════════════════════════════════════

    /// <summary>
    /// Retrieves all active subjects available for teacher registration.
    /// Includes all Egyptian Ministry of Education subjects, ordered by DisplayOrder.
    /// </summary>
    /// <remarks>
    /// AAM-FR-03.5: Subject selection during registration.
    /// </remarks>
    /// <returns>List of active subjects with bilingual names.</returns>
    [HttpGet("subjects")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableSubjects()
    {
        var result = await _teacherService.GetAvailableSubjectsAsync();
        return ToResponse(result);
    }

    /// <summary>
    /// Retrieves all active student capacity packages.
    /// Returns the 7 tiers from "Up to 300" through "3000+", ordered by DisplayOrder.
    /// </summary>
    /// <remarks>
    /// AAM-FR-04.1: Capacity package selection during configuration.
    /// </remarks>
    /// <returns>List of active capacity packages.</returns>
    [HttpGet("capacity-packages")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCapacityPackages()
    {
        var result = await _teacherService.GetCapacityPackagesAsync();
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════
    // TEACHER LIST (PAGINATED)
    // ══════════════════════════════════════════════════

    /// <summary>
    /// Retrieves a paginated list of teachers with search, sort, and filter support.
    /// Used by the Super Admin dashboard to browse and manage teacher accounts.
    /// </summary>
    /// <remarks>
    /// REQ-ADM-026: Teacher list with name, username, student count, subscription status.
    /// REQ-ADM-027: Searchable by name/username, filterable by status.
    /// REQ-ADM-028: Sortable by name, subscription date, student count, last login.
    /// </remarks>
    /// <param name="request">Pagination parameters (page, pageSize, search, sortBy, sortDirection).</param>
    /// <param name="accountStatus">Optional filter: Active, Inactive, or Suspended.</param>
    /// <param name="subscriptionStatus">Optional filter: Active, ExpiringSoon, or Expired.</param>
    /// <returns>Paginated list of teacher summaries.</returns>
    [HttpGet("list")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeachers(
        [FromQuery] PaginatedRequest request,
        [FromQuery] string? accountStatus = null,
        [FromQuery] string? subscriptionStatus = null)
    {
        var result = await _teacherService.GetTeachersAsync(request, accountStatus, subscriptionStatus);
        return ToResponse(result);
    }
}