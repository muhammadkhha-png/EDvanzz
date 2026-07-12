using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.TeacherLinks;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Teacher-side operations of the student-teacher link request/approval flow:
/// exposing the shareable teacher code, reviewing incoming link requests
/// (accept / reject), listing linked students, and removing one or many links.
///
/// The student-side half (sending requests, cancelling, listing own teachers)
/// lives on <see cref="IStudentUserService"/>.
///
/// All methods take <c>teacherId</c> resolved from the JWT by the controller
/// (assistants resolve to their owning tutor) — never from the route or body.
/// </summary>
public interface ITeacherStudentLinkService
{
    /// <summary>
    /// Returns the teacher's unique 8-digit TeacherCode — the only credential a
    /// student needs to SEND a link request (the teacher approves afterwards, so
    /// exposing the code is safe by design).
    /// </summary>
    Task<Result<TeacherCodeDto>> GetMyTeacherCodeAsync(long teacherId);

    /// <summary>
    /// Pages the Pending link requests addressed to this teacher, newest first.
    /// Each item carries the requesting account identity plus a suggested roster
    /// match when the student supplied a student code that resolves.
    /// </summary>
    Task<Result<PaginatedResponse<List<TeacherLinkRequestListItemDto>>>> GetPendingLinkRequestsAsync(
        long teacherId, int page, int pageSize);

    /// <summary>
    /// Accepts a Pending request, binding it to a TeacherStudent roster record
    /// (explicit selection, or auto-matched from the request's student code) and
    /// activating the link. Fails 422 when no roster record can be resolved and
    /// 409 when the record is already claimed by another student account.
    /// Notifies the student (inbox + push) post-commit, best-effort.
    /// </summary>
    /// <param name="actingUserId">User.Id of the teacher/assistant performing the accept (audit).</param>
    Task<Result<LinkedStudentListItemDto>> AcceptLinkRequestAsync(
        long teacherId, long linkId, long actingUserId, AcceptLinkRequestDto dto);

    /// <summary>
    /// Rejects a Pending request (terminal, kept for audit — the student may send
    /// a new request later). Notifies the student post-commit, best-effort.
    /// </summary>
    /// <param name="actingUserId">User.Id of the teacher/assistant performing the reject (audit).</param>
    Task<Result<bool>> RejectLinkRequestAsync(long teacherId, long linkId, long actingUserId);

    /// <summary>
    /// Pages the teacher's linked students (Active links), newest first, with both
    /// the account identity and the bound roster record.
    /// </summary>
    Task<Result<PaginatedResponse<List<LinkedStudentListItemDto>>>> GetLinkedStudentsAsync(
        long teacherId, int page, int pageSize);

    /// <summary>
    /// Removes one or many linked students (LinkStatus = RemovedByTeacher).
    /// Ids that are not owned by this teacher or not Active are skipped and
    /// reported back. Notifies each affected student post-commit, best-effort.
    /// </summary>
    /// <param name="actingUserId">User.Id of the teacher/assistant performing the removal — persisted to RemovedByUserId for audit.</param>
    Task<Result<RemoveLinkedStudentsResultDto>> RemoveLinkedStudentsAsync(
        long teacherId, long actingUserId, RemoveLinkedStudentsDto dto);
}