using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ParentPortal;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// TEACHER side of the parent portal: the pending-request inbox, approve / reject (single and
/// bulk), the per-student follower list, revocation, and the settings-screen counters.
///
/// TENANCY: <c>teacherId</c> is always supplied by the controller from the JWT
/// (<c>ResolveTeacherIdAsync</c>) and every repo call is tenant-scoped, so a grant id belonging to
/// another teacher resolves to nothing — never to another tenant's row (CLAUDE.md §3.3 / BUG-12).
/// Assistants holding <c>Student / Edit</c> reach these endpoints on their tutor's behalf.
///
/// Parent phone numbers are returned IN FULL (changed 2026-09-02, was masked): a teacher deciding
/// whether to let a stranger see a child's data has to recognize the number and be able to ring it
/// back, and they normally hold the same number on the roster anyway.
/// </summary>
public interface ITeacherParentPortalService
{
    /// <summary>Paged inbox of PENDING parent requests, newest first.</summary>
    Task<Result<PaginatedResponse<List<ParentPortalRequestListItemDto>>>> GetPendingRequestsAsync(
        long teacherId, int page, int pageSize);

    /// <summary>
    /// Approves one pending request — the grant becomes Active (<c>Origin = TeacherApproved</c>)
    /// and the parent gains read access on their next call. Because trust follows the PHONE, that
    /// number now also admits the parent from any OTHER device for this student without troubling
    /// the teacher again. Idempotent-ish: an already-Active row succeeds unchanged; a terminal row
    /// returns <c>ParentPortalRequestNotFound</c>.
    /// </summary>
    /// <param name="teacherId">Acting tenant, resolved from the JWT by the controller.</param>
    /// <param name="requestId">Grant id from the inbox.</param>
    /// <param name="actingUserId">User.Id recorded as the approver (teacher or assistant).</param>
    /// <param name="dto">
    /// Optional. <c>savePhoneToStudent</c> also writes the approved number onto the student's
    /// roster record when that record has none — never overwriting an existing one. Pass null for
    /// a plain approval.
    /// </param>
    Task<Result<ParentPortalApproveResultDto>> ApproveRequestAsync(
        long teacherId, long requestId, long actingUserId, ParentPortalApproveRequestDto? dto);

    /// <summary>Rejects one pending request. The row is kept for audit and stops blocking the live-row unique index.</summary>
    Task<Result<ParentPortalFollowerListItemDto>> RejectRequestAsync(
        long teacherId, long requestId, long actingUserId);

    /// <summary>Approves or rejects many requests in one transaction. Foreign or already-resolved ids are reported as skipped, not as errors.</summary>
    Task<Result<ParentPortalBulkResultDto>> BulkResolveAsync(
        long teacherId, ParentPortalBulkActionDto dto, long actingUserId);

    /// <summary>Everyone (Active or Pending) currently following one roster student.</summary>
    Task<Result<List<ParentPortalFollowerListItemDto>>> GetFollowersAsync(long teacherId, long teacherStudentId);

    /// <summary>
    /// Ends a follower's access (teacher-initiated), recorded with the acting user so the parent
    /// is told the TEACHER removed it.
    ///
    /// PHONE-WIDE, not row-wide: it revokes every live grant sharing that (student, phone), and
    /// returns how many devices were cut off. A single-row revoke would silently do nothing —
    /// the trusted-phone rule would re-admit the parent through a surviving sibling row.
    ///
    /// REFUSES with 409 <c>ParentPortalRevokeBlockedRosterPhone</c> when the number is the one
    /// saved on the student's own record: revoking it could not hold (the roster-phone rule would
    /// auto-approve them again on the next submit), so the teacher is told to clear it from the
    /// student first rather than having a revoke button quietly edit their roster.
    /// </summary>
    Task<Result<ParentPortalRevokeResultDto>> RevokeFollowerAsync(
        long teacherId, long accessId, long actingUserId);

    /// <summary>Counters for the parent-portal settings screen.</summary>
    Task<Result<ParentPortalSummaryDto>> GetSummaryAsync(long teacherId);
}
