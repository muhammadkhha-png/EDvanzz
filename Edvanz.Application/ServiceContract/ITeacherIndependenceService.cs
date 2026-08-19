using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Center;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// The formal request/approval flow for a center-owned teacher to LEAVE the center and become an
/// independent (standalone) teacher. Teacher-side: submit / view-my / cancel. Admin-side: queue /
/// approve (detach) / reject. Mirrors the center-subscription request flow.
/// </summary>
public interface ITeacherIndependenceService
{
    // ── Teacher side (the center-owned teacher, resolved from JWT) ──
    Task<Result<TeacherIndependenceRequestDto>> SubmitAsync(long teacherId, long teacherUserId, SubmitIndependenceRequestDto dto);
    Task<Result<TeacherIndependenceRequestDto?>> GetMyRequestAsync(long teacherId);
    Task<Result<string>> CancelAsync(long teacherId, long teacherUserId);

    // ── Admin side (SuperAdmin) ──
    Task<Result<List<IndependenceRequestQueueItemDto>>> GetPendingRequestsAsync();
    /// <summary>Approve = detach the teacher from the center (clear CenterId + center-plan/revenue
    /// overrides) so they become a standalone teacher who then subscribes on their own.</summary>
    Task<Result<string>> ApproveAsync(long adminUserId, long requestId);
    Task<Result<string>> RejectAsync(long adminUserId, long requestId, RejectIndependenceRequestDto dto);
}
