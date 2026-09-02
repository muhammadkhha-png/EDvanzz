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
/// Assistants holding <c>Student / Edit</c> reach these endpoints on their tutor's behalf, which
/// is why phone numbers are returned MASKED.
/// </summary>
public interface ITeacherParentPortalService
{
    /// <summary>Paged inbox of PENDING parent requests, newest first.</summary>
    Task<Result<PaginatedResponse<List<ParentPortalRequestListItemDto>>>> GetPendingRequestsAsync(
        long teacherId, int page, int pageSize);

    /// <summary>
    /// Approves one pending request — the grant becomes Active and the parent's device gains
    /// read access on its next call. Idempotent-ish: an already-Active row succeeds unchanged; a
    /// terminal row returns <c>ParentPortalRequestNotFound</c>.
    /// </summary>
    Task<Result<ParentPortalFollowerListItemDto>> ApproveRequestAsync(
        long teacherId, long requestId, long actingUserId);

    /// <summary>Rejects one pending request. The row is kept for audit and stops blocking the live-row unique index.</summary>
    Task<Result<ParentPortalFollowerListItemDto>> RejectRequestAsync(
        long teacherId, long requestId, long actingUserId);

    /// <summary>Approves or rejects many requests in one transaction. Foreign or already-resolved ids are reported as skipped, not as errors.</summary>
    Task<Result<ParentPortalBulkResultDto>> BulkResolveAsync(
        long teacherId, ParentPortalBulkActionDto dto, long actingUserId);

    /// <summary>Everyone (Active or Pending) currently following one roster student.</summary>
    Task<Result<List<ParentPortalFollowerListItemDto>>> GetFollowersAsync(long teacherId, long teacherStudentId);

    /// <summary>Ends a follower's access (teacher-initiated). Recorded with the acting user so the parent is told the teacher removed it.</summary>
    Task<Result<bool>> RevokeFollowerAsync(long teacherId, long accessId, long actingUserId);

    /// <summary>Counters for the parent-portal settings screen.</summary>
    Task<Result<ParentPortalSummaryDto>> GetSummaryAsync(long teacherId);
}
