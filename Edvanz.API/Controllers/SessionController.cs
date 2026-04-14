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
public class SessionController : ApiBaseController
{
    private readonly ISessionService _sessionService;

    public SessionController(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

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
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
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
    [HttpGet("{teacherId:long}/sessions/{sessionId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionById(
        [FromRoute] long teacherId,
        [FromRoute] long sessionId)
    {
        var result = await _sessionService.GetSessionByIdAsync(teacherId, sessionId);
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
    [HttpPut("{teacherId:long}/sessions/{sessionId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSession(
        [FromRoute] long teacherId,
        [FromRoute] long sessionId,
        [FromBody] UpdateSessionDto dto)
    {
        var result = await _sessionService.UpdateSessionAsync(teacherId, sessionId, dto);
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
    [HttpGet("{teacherId:long}/sessions/{sessionId:long}/delete-confirmation")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeleteConfirmation(
        [FromRoute] long teacherId,
        [FromRoute] long sessionId)
    {
        var result = await _sessionService.GetDeleteConfirmationAsync(teacherId, sessionId);
        return ToResponse(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENDPOINT 5: DELETE SESSION
    // ══════════════════════════════════════════════════════════════════════════
    //
    // WHAT IT DOES:
    //   Permanently deletes a session. Hard delete — no recovery (REQ-SES-041).
    //   REQ-SES-042: Unassigns all students (SessionId → null via DB cascade).
    //   REQ-SES-043: Removes all membership links.
    //   BR-SES-004: Irreversible.
    //
    // TABLES WRITTEN: Sessions (delete), SessionLinks (cascade), TeacherStudents (cascade SetNull)
    //
    // SAMPLE REQUEST:
    //   DELETE /api/session/1/sessions/5
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("{teacherId:long}/sessions/{sessionId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSession(
        [FromRoute] long teacherId,
        [FromRoute] long sessionId)
    {
        var result = await _sessionService.DeleteSessionAsync(teacherId, sessionId);
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
    [HttpPost("{teacherId:long}/sessions/{sessionId:long}/duplicate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DuplicateSession(
        [FromRoute] long teacherId,
        [FromRoute] long sessionId)
    {
        var result = await _sessionService.DuplicateSessionAsync(teacherId, sessionId);
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
    [HttpGet("{teacherId:long}/sessions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionList(
        [FromRoute] long teacherId,
        [FromQuery] SessionListRequest request)
    {
        var result = await _sessionService.GetSessionListAsync(teacherId, request);
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
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
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
    [HttpGet("{teacherId:long}/groups")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGroups([FromRoute] long teacherId)
    {
        var result = await _sessionService.GetGroupsAsync(teacherId);
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
    [HttpPut("{teacherId:long}/groups/{groupId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RenameGroup(
        [FromRoute] long teacherId,
        [FromRoute] long groupId,
        [FromBody] RenameSessionGroupDto dto)
    {
        var result = await _sessionService.RenameGroupAsync(teacherId, groupId, dto);
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
    // TABLES WRITTEN: SessionGroups (delete), Sessions (cascade SetNull on SessionGroupId)
    //
    // SAMPLE REQUEST:
    //   DELETE /api/session/1/groups/3
    //
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("{teacherId:long}/groups/{groupId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGroup(
        [FromRoute] long teacherId,
        [FromRoute] long groupId)
    {
        var result = await _sessionService.DeleteGroupAsync(teacherId, groupId);
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
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
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
    [HttpDelete("{teacherId:long}/links/{sessionIdA:long}/{sessionIdB:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveLink(
        [FromRoute] long teacherId,
        [FromRoute] long sessionIdA,
        [FromRoute] long sessionIdB)
    {
        var result = await _sessionService.RemoveLinkAsync(teacherId, sessionIdA, sessionIdB);
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
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
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
    [HttpPost("{teacherId:long}/sessions/{sessionId:long}/confirm-reassign")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmReassign(
        [FromRoute] long teacherId,
        [FromRoute] long sessionId,
        [FromBody] List<long> studentIds)
    {
        var result = await _sessionService.ConfirmReassignStudentsAsync(teacherId, sessionId, studentIds);
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
    [HttpDelete("{teacherId:long}/sessions/{sessionId:long}/students/{studentId:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnassignStudent(
        [FromRoute] long teacherId,
        [FromRoute] long sessionId,
        [FromRoute] long studentId)
    {
        var result = await _sessionService.UnassignStudentAsync(teacherId, sessionId, studentId);
        return ToResponse(result);
    }
}