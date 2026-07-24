using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Session;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Defines the contract for the Session Module (Module 2) operations.
/// Manages teacher-scoped sessions: CRUD, groups, membership links,
/// student assignment, search, filter, and session name generation.
/// 
/// All methods are async. All return Result&lt;T&gt; for consistent error handling.
/// All database access goes through IUnitOfWork.
/// </summary>
public interface ISessionService
{
    // ══════════════════════════════════════════════
    // SESSION CRUD
    // ══════════════════════════════════════════════

    /// <summary>
    /// Creates a new session under the teacher's account.
    /// REQ-SES-001: Unlimited sessions per teacher.
    /// REQ-SES-002: Manual or auto-generated session name.
    /// REQ-SES-006: All mandatory fields validated.
    /// BR-SES-001: Unique session name per teacher.
    /// </summary>
    Task<Result<SessionDto>> CreateSessionAsync(CreateSessionDto dto);

    /// <summary>
    /// Retrieves a single session by Id, scoped to the teacher.
    /// Includes student count, group name, and linked session info.
    /// </summary>
    Task<Result<SessionDto>> GetSessionByIdAsync(long teacherId, long sessionId);

    /// <summary>
    /// Updates an existing session.
    /// REQ-SES-005: name editable. REQ-SES-012: Payment editable.
    /// REQ-SES-009: OccurrenceType not editable if assignments/links exist.
    /// </summary>
    Task<Result<SessionDto>> UpdateSessionAsync(long teacherId, long sessionId, UpdateSessionDto dto);

    /// <summary>
    /// Retrieves deletion confirmation data for a session.
    /// REQ-SES-040/047: Student count and affected linked session names.
    /// </summary>
    Task<Result<SessionDeleteConfirmationDto>> GetDeleteConfirmationAsync(long teacherId, long sessionId);

    /// <summary>
    /// Permanently deletes a session. Hard delete — no recovery.
    /// REQ-SES-041: Permanent removal.
    /// REQ-SES-042: Unassigns all students (SessionId set to null).
    /// REQ-SES-043: Removes all membership links.
    /// </summary>
    Task<Result<bool>> DeleteSessionAsync(long teacherId, long sessionId);

    /// <summary>
    /// Duplicates an existing session with a new name and blank dates.
    /// REQ-SES-046: Pre-filled configuration, new name, blank start/end dates.
    /// </summary>
    Task<Result<SessionDto>> DuplicateSessionAsync(long teacherId, long sessionId);

    // ══════════════════════════════════════════════
    // SESSION LIST (SEARCH + FILTER + PAGINATION)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Retrieves a paginated, searchable, filterable list of the teacher's sessions.
    /// REQ-SES-044: Search by session name.
    /// REQ-SES-045: Filter by group, occurrence type, active/expired status.
    /// REQ-SES-NFR-001: Must load in under 2 seconds.
    /// </summary>
    Task<Result<PaginatedResponse<List<SessionDto>>>> GetSessionListAsync(
        long teacherId, SessionListRequest request);

    // ══════════════════════════════════════════════
    // SESSION GROUPS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Creates a new session group.
    /// REQ-SES-024/025.
    /// </summary>
    Task<Result<SessionGroupDto>> CreateGroupAsync(CreateSessionGroupDto dto);

    /// <summary>
    /// Retrieves all session groups for a teacher.
    /// REQ-SES-027: Group list for drill-down navigation.
    /// </summary>
    Task<Result<List<SessionGroupDto>>> GetGroupsAsync(long teacherId);

    /// <summary>
    /// Renames an existing session group.
    /// REQ-SES-031: Groups are renamable.
    /// </summary>
    Task<Result<SessionGroupDto>> RenameGroupAsync(long teacherId, long groupId, RenameSessionGroupDto dto);

    /// <summary>
    /// Deletes a session group. Sessions become ungrouped.
    /// REQ-SES-031: Deleting does not delete sessions.
    /// </summary>
    Task<Result<bool>> DeleteGroupAsync(long teacherId, long groupId);

    // ══════════════════════════════════════════════
    // SESSION LINKING (MEMBERSHIP)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Creates a symmetric link between two sessions.
    /// REQ-SES-032: Membership linking.
    /// BR-SES-003: Only sessions with identical occurrence configuration can be linked.
    /// </summary>
    Task<Result<bool>> CreateLinkAsync(CreateSessionLinkDto dto);

    /// <summary>
    /// Removes a link between two sessions.
    /// REQ-SES-037: Does not affect sessions or student assignments.
    /// </summary>
    Task<Result<bool>> RemoveLinkAsync(long teacherId, long sessionIdA, long sessionIdB);

    // ══════════════════════════════════════════════
    // STUDENT ASSIGNMENT
    // ══════════════════════════════════════════════

    /// <summary>
    /// Assigns students to a session, returning warnings for already-assigned students.
    /// REQ-SES-016/017: Assign from session module, multiple via checkboxes.
    /// REQ-SES-018: Warning for students already in another session.
    /// </summary>
    Task<Result<AssignStudentsResultDto>> AssignStudentsAsync(AssignStudentsToSessionDto dto);

    /// <summary>
    /// Confirms reassignment of students that were already assigned to another session.
    /// REQ-SES-019: Override previous session assignment.
    /// </summary>
    Task<Result<int>> ConfirmReassignStudentsAsync(long teacherId, long sessionId, List<long> studentIds);

    /// <summary>
    /// Removes a student's session assignment (unassigns from session).
    /// Sets TeacherStudent.SessionId to null.
    /// </summary>
    Task<Result<bool>> UnassignStudentAsync(long teacherId, long sessionId, long studentId);
    /// <summary>
    /// Returns every session (Id + SessionName only) belonging to the teacher, ordered by name.
    /// Not paginated, not filtered — intended for select/dropdown controls, not the sessions screen.
    /// </summary>
    Task<Result<List<SessionLookupItemDto>>> GetSessionLookupAsync(long teacherId);
}