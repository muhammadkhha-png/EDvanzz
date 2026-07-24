using Edvanz.Application.Dtos.Session;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// API controller for the Session Module (Module 2).
/// Manages sessions, session groups, session links (membership), and student assignment.
/// All endpoints are teacher-scoped via the teacherId route parameter.
/// 
/// All endpoint documentation follows the existing project pattern:
/// WHAT IT DOES → TABLES READ/WRITTEN → SAMPLE REQUEST → SAMPLE RESPONSE.
/// </summary>
[ServiceFilter(typeof(Edvanz.API.Filters.TenantScopeFilter))]
public class SessionController : ApiBaseController
{
    private readonly ISessionService _sessionService;
    private readonly Edvanz.Application.IservicesContract.ICurrentUserService _currentUser;
    private readonly Edvanz.Domain.Interfaces.IUnitOfWork _unitOfWork;

    public SessionController(
        ISessionService sessionService,
        Edvanz.Application.IservicesContract.ICurrentUserService currentUser,
        Edvanz.Domain.Interfaces.IUnitOfWork unitOfWork)
    {
        _sessionService = sessionService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// The acting teacher, resolved from the JWT (Teacher → own id, Assistant → owning teacher).
    /// The optional <paramref name="explicitTeacherId"/> from the route is honoured ONLY for
    /// SuperAdmin (support access); for everyone else it is ignored so the token is the sole tenant
    /// differentiator — endpoints work whether or not the URL carries the id.
    /// </summary>
    private async Task<long?> ResolveTeacherIdAsync(long? explicitTeacherId = null)
    {
        if (string.Equals(_currentUser.Role, "SuperAdmin", StringComparison.Ordinal))
            return explicitTeacherId;

        var userId = _currentUser.UserId;
        if (userId is null) return null;

        long? id = (await _unitOfWork.Users.GetTeacherByUserIdAsync(userId.Value))?.Id;
        if (id is null)
        {
            var asst = await _unitOfWork.AssistantRepo.GetAssistantWithUserIdAsync(userId.Value);
            id = asst?.TeacherAccountId;
        }
        return id;
    }

    private IActionResult TeacherNotResolvedResult() =>
        new ObjectResult(new { success = false, message = "Teacher could not be resolved from token." })
        { StatusCode = StatusCodes.Status404NotFound };

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 1: CREATE SESSION
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Creates a new session under the teacher's account.
    //   REQ-SES-001: No system-imposed cap on sessions.
    //   REQ-SES-002: Session name is auto-generated if not provided (when config is Auto).
    //   Validates: teacher exists, name uniqueness (BR-SES-001), occurrence config,
    //   date range (REQ-SES-014), group exists if provided.
    //
    // TABLES WRITTEN: Sessions
    // TABLES READ: Teachers, TeacherConfigurations, Sessions (name uniqueness), SessionGroups
    //
    // SAMPLE REQUEST:
    //   POST /api/session
    //   {
    //     "teacherId": 1,
    //     "sessionName": null,
    //     "occurrenceType": "Weekly",
    //     "selectedDays": [0, 3],
    //     "paymentType": "Monthly",
    //     "sessionAmount": 250.00,
    //     "startDate": "2026-04-01",
    //     "endDate": "2026-07-01",
    //     "startTime": "17:00",
    //     "durationMinutes": 90,
    //     "sessionGroupId": null
    //   }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Session.SessionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionDto dto)
    {
        var result = await _sessionService.CreateSessionAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 2: GET SESSION BY ID
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Retrieves a single session by Id, scoped to the teacher.
    //   Includes student count, group name, and linked session info.
    //
    // TABLES READ: Sessions, TeacherStudents (count), SessionLinks, SessionGroups
    //
    // SAMPLE REQUEST:
    //   GET /api/session/1/sessions/5
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("sessions/{sessionId:long}")]
    [HttpGet("{teacherId:long}/sessions/{sessionId:long}")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Session.SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionById(
        [FromRoute] long sessionId,
        [FromRoute] long? teacherId = null)
    {
        var id = await ResolveTeacherIdAsync(teacherId);
        if (id is null) return TeacherNotResolvedResult();
        var result = await _sessionService.GetSessionByIdAsync(id.Value, sessionId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 3: UPDATE SESSION
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Updates an existing session's configuration.
    //   REQ-SES-005: name editable. REQ-SES-012: Payment editable.
    //   REQ-SES-009: OccurrenceType NOT editable if session has assignments or links.
    //
    // TABLES WRITTEN: Sessions
    // TABLES READ: Sessions, TeacherStudents (for constraint check), SessionLinks, SessionGroups
    //
    // SAMPLE REQUEST:
    //   PUT /api/session/1/sessions/5
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("sessions/{sessionId:long}")]
    [HttpPut("{teacherId:long}/sessions/{sessionId:long}")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Session.SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSession(
        [FromRoute] long sessionId,
        [FromBody] UpdateSessionDto dto,
        [FromRoute] long? teacherId = null)
    {
        var id = await ResolveTeacherIdAsync(teacherId);
        if (id is null) return TeacherNotResolvedResult();
        var result = await _sessionService.UpdateSessionAsync(id.Value, sessionId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 4: GET SESSION DELETE CONFIRMATION
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns data needed for the deletion confirmation dialog.
    //   REQ-SES-040: Session name, student count, warnings.
    //   REQ-SES-047: Lists names of linked sessions that will lose membership.
    //
    // TABLES READ: Sessions, TeacherStudents (count), SessionLinks
    //
    // SAMPLE REQUEST:
    //   GET /api/session/1/sessions/5/delete-confirmation
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("sessions/{sessionId:long}/delete-confirmation")]
    [HttpGet("{teacherId:long}/sessions/{sessionId:long}/delete-confirmation")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Session.SessionDeleteConfirmationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeleteConfirmation(
        [FromRoute] long sessionId,
        [FromRoute] long? teacherId = null)
    {
        var id = await ResolveTeacherIdAsync(teacherId);
        if (id is null) return TeacherNotResolvedResult();
        var result = await _sessionService.GetDeleteConfirmationAsync(id.Value, sessionId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5: DELETE SESSION
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Permanently deletes a session. Hard delete — no recovery (REQ-SES-041).
    //   REQ-SES-042: Unassigns all students (SessionId → null via DB NoAction).
    //   REQ-SES-043: Removes all membership links.
    //   BR-SES-004: Irreversible.
    //
    // TABLES WRITTEN: Sessions (delete), SessionLinks (NoAction), TeacherStudents (NoAction SetNull)
    //
    // SAMPLE REQUEST:
    //   DELETE /api/session/1/sessions/5
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("sessions/{sessionId:long}")]
    [HttpDelete("{teacherId:long}/sessions/{sessionId:long}")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSession(
        [FromRoute] long sessionId,
        [FromRoute] long? teacherId = null)
    {
        var id = await ResolveTeacherIdAsync(teacherId);
        if (id is null) return TeacherNotResolvedResult();
        var result = await _sessionService.DeleteSessionAsync(id.Value, sessionId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 6: DUPLICATE SESSION
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Creates a copy of an existing session with a new auto-generated name
    //   and placeholder dates. REQ-SES-046.
    //
    // TABLES WRITTEN: Sessions
    // TABLES READ: Sessions, TeacherConfigurations
    //
    // SAMPLE REQUEST:
    //   POST /api/session/1/sessions/5/duplicate
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("sessions/{sessionId:long}/duplicate")]
    [HttpPost("{teacherId:long}/sessions/{sessionId:long}/duplicate")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Session.SessionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DuplicateSession(
        [FromRoute] long sessionId,
        [FromRoute] long? teacherId = null)
    {
        var id = await ResolveTeacherIdAsync(teacherId);
        if (id is null) return TeacherNotResolvedResult();
        var result = await _sessionService.DuplicateSessionAsync(id.Value, sessionId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 7: GET SESSION LIST (PAGINATED + SEARCH + FILTER + SORT)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns a paginated list of the teacher's sessions.
    //   REQ-SES-044: Search by session name.
    //   REQ-SES-045: Filter by group, occurrence type, active/expired.
    //   REQ-SES-NFR-001: Must load in under 2 seconds.
    //
    // QUERY PARAMETERS:
    //   - page, pageSize, search, groupId, occurrenceType, activeOnly, expiredOnly, sortBy, sortDirection
    //
    // TABLES READ: Sessions, TeacherStudents (counts), SessionLinks, SessionGroups
    //
    // SAMPLE REQUEST:
    //   GET /api/session/1/sessions?page=1&pageSize=20&search=Monday&activeOnly=true
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("sessions")]
    [HttpGet("{teacherId:long}/sessions")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.PaginatedResponse<System.Collections.Generic.List<Edvanz.Application.Dtos.Session.SessionDto>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionList(
        [FromQuery] SessionListRequest request,
        [FromRoute] long? teacherId = null)
    {
        var id = await ResolveTeacherIdAsync(teacherId);
        if (id is null) return TeacherNotResolvedResult();
        var result = await _sessionService.GetSessionListAsync(id.Value, request);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 8: CREATE SESSION GROUP
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Creates a named session group. REQ-SES-024/025.
    //
    // TABLES WRITTEN: SessionGroups
    // TABLES READ: Teachers, SessionGroups (name uniqueness)
    //
    // SAMPLE REQUEST:
    //   POST /api/session/groups
    //   { "teacherId": 1, "groupName": "Prep Year 1" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("groups")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Session.SessionGroupDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateGroup([FromBody] CreateSessionGroupDto dto)
    {
        var result = await _sessionService.CreateGroupAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 9: GET SESSION GROUPS
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns all session groups for a teacher with session counts.
    //   REQ-SES-027: Group box layout data.
    //
    // TABLES READ: SessionGroups, Sessions (counts)
    //
    // SAMPLE REQUEST:
    //   GET /api/session/1/groups
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("groups")]
    [HttpGet("{teacherId:long}/groups")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<System.Collections.Generic.List<Edvanz.Application.Dtos.Session.SessionGroupDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGroups([FromRoute] long? teacherId = null)
    {
        var id = await ResolveTeacherIdAsync(teacherId);
        if (id is null) return TeacherNotResolvedResult();
        var result = await _sessionService.GetGroupsAsync(id.Value);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 10: RENAME SESSION GROUP
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Renames an existing session group. REQ-SES-031.
    //
    // TABLES WRITTEN: SessionGroups
    // TABLES READ: SessionGroups (uniqueness), Sessions (count)
    //
    // SAMPLE REQUEST:
    //   PUT /api/session/1/groups/3
    //   { "groupName": "Prep Year 1 — Updated" }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("groups/{groupId:long}")]
    [HttpPut("{teacherId:long}/groups/{groupId:long}")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Session.SessionGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RenameGroup(
        [FromRoute] long groupId,
        [FromBody] RenameSessionGroupDto dto,
        [FromRoute] long? teacherId = null)
    {
        var id = await ResolveTeacherIdAsync(teacherId);
        if (id is null) return TeacherNotResolvedResult();
        var result = await _sessionService.RenameGroupAsync(id.Value, groupId, dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 11: DELETE SESSION GROUP
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Deletes a session group. Sessions become ungrouped (REQ-SES-031).
    //   Does NOT delete sessions within the group.
    //
    // TABLES WRITTEN: SessionGroups (delete), Sessions (NoAction SetNull on SessionGroupId)
    //
    // SAMPLE REQUEST:
    //   DELETE /api/session/1/groups/3
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("groups/{groupId:long}")]
    [HttpDelete("{teacherId:long}/groups/{groupId:long}")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGroup(
        [FromRoute] long groupId,
        [FromRoute] long? teacherId = null)
    {
        var id = await ResolveTeacherIdAsync(teacherId);
        if (id is null) return TeacherNotResolvedResult();
        var result = await _sessionService.DeleteGroupAsync(id.Value, groupId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 12: CREATE SESSION LINK (MEMBERSHIP)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Creates a symmetric link between two sessions (REQ-SES-032).
    //   BR-SES-003: Sessions must have identical occurrence type and day config.
    //   REQ-SES-036: Symmetric — one action links both directions.
    //
    // TABLES WRITTEN: SessionLinks
    // TABLES READ: Sessions (validation), SessionLinks (duplicate check)
    //
    // SAMPLE REQUEST:
    //   POST /api/session/links
    //   { "teacherId": 1, "sessionIdA": 5, "sessionIdB": 8 }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("links")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateLink([FromBody] CreateSessionLinkDto dto)
    {
        var result = await _sessionService.CreateLinkAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 13: REMOVE SESSION LINK
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Removes a link between two sessions (REQ-SES-037).
    //   Does not affect sessions or student assignments.
    //
    // TABLES WRITTEN: SessionLinks (delete)
    // TABLES READ: Sessions (ownership check), SessionLinks
    //
    // SAMPLE REQUEST:
    //   DELETE /api/session/1/links/5/8
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("links/{sessionIdA:long}/{sessionIdB:long}")]
    [HttpDelete("{teacherId:long}/links/{sessionIdA:long}/{sessionIdB:long}")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveLink(
        [FromRoute] long sessionIdA,
        [FromRoute] long sessionIdB,
        [FromRoute] long? teacherId = null)
    {
        var id = await ResolveTeacherIdAsync(teacherId);
        if (id is null) return TeacherNotResolvedResult();
        var result = await _sessionService.RemoveLinkAsync(id.Value, sessionIdA, sessionIdB);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 14: ASSIGN STUDENTS TO SESSION
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Assigns multiple students to a session in one action (REQ-SES-017).
    //   Returns warnings for students already assigned elsewhere (REQ-SES-018).
    //   The client presents warnings to the tutor. Confirmed reassignments are
    //   submitted via Endpoint 15 (ConfirmReassign).
    //
    // TABLES WRITTEN: TeacherStudents (SessionId update for non-conflicting)
    // TABLES READ: Sessions, TeacherStudents
    //
    // SAMPLE REQUEST:
    //   POST /api/session/assign-students
    //   { "teacherId": 1, "sessionId": 5, "studentIds": [10, 11, 12, 13] }
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("assign-students")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<Edvanz.Application.Dtos.Session.AssignStudentsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignStudents([FromBody] AssignStudentsToSessionDto dto)
    {
        var result = await _sessionService.AssignStudentsAsync(dto);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 15: CONFIRM REASSIGN STUDENTS
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Confirms reassignment for students that were flagged with warnings
    //   in Endpoint 14. REQ-SES-019: Override previous session assignment.
    //
    // TABLES WRITTEN: TeacherStudents (SessionId update)
    // TABLES READ: Sessions, TeacherStudents
    //
    // SAMPLE REQUEST:
    //   POST /api/session/1/sessions/5/confirm-reassign
    //   [10, 13]
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("sessions/{sessionId:long}/confirm-reassign")]
    [HttpPost("{teacherId:long}/sessions/{sessionId:long}/confirm-reassign")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmReassign(
        [FromRoute] long sessionId,
        [FromBody] List<long> studentIds,
        [FromRoute] long? teacherId = null)
    {
        var id = await ResolveTeacherIdAsync(teacherId);
        if (id is null) return TeacherNotResolvedResult();
        var result = await _sessionService.ConfirmReassignStudentsAsync(id.Value, sessionId, studentIds);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 16: UNASSIGN STUDENT FROM SESSION
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Removes a single student's session assignment (sets SessionId to null).
    //
    // TABLES WRITTEN: TeacherStudents (SessionId → null)
    // TABLES READ: TeacherStudents
    //
    // SAMPLE REQUEST:
    //   DELETE /api/session/1/sessions/5/students/10
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("sessions/{sessionId:long}/students/{studentId:long}")]
    [HttpDelete("{teacherId:long}/sessions/{sessionId:long}/students/{studentId:long}")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnassignStudent(
        [FromRoute] long sessionId,
        [FromRoute] long studentId,
        [FromRoute] long? teacherId = null)
    {
        var id = await ResolveTeacherIdAsync(teacherId);
        if (id is null) return TeacherNotResolvedResult();
        var result = await _sessionService.UnassignStudentAsync(id.Value, sessionId, studentId);
        return ToResponse(result);
    }


    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 7b: GET SESSION LOOKUP (Id + Name only, not paginated)
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Returns every session belonging to the teacher as {id, sessionName} pairs.
    //   No pagination, no search/filter — for select/dropdown controls.
    //
    // SAMPLE REQUEST (Teacher/Assistant, teacherId from JWT):
    //   GET /api/session/sessions/lookup
    //
    // SAMPLE REQUEST (SuperAdmin, explicit teacherId):
    //   GET /api/session/1/sessions/lookup
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("sessions/lookup")]
    [HttpGet("{teacherId:long}/sessions/lookup")]
    [ProducesResponseType(typeof(Edvanz.Application.Dtos.Result<System.Collections.Generic.List<Edvanz.Application.Dtos.Session.SessionLookupItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionLookup([FromRoute] long? teacherId = null)
    {
        var id = await ResolveTeacherIdAsync(teacherId);
        if (id is null) return TeacherNotResolvedResult();

        var result = await _sessionService.GetSessionLookupAsync(id.Value);
        return ToResponse(result);
    }
}