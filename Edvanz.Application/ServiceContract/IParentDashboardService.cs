using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ParentUser;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Aggregates the Parent's per-teacher child dashboard (Parent Module requirements §9) into a
/// single call, resolved by (TeacherCode, StudentCode) rather than internal ids (§3). Mirrors
/// <see cref="IStudentTeacherHomeService"/>'s aggregation pattern for the student side: resolves
/// the caller once (JWT parent → owned child under the named teacher), then fills each section
/// behind its own <c>TeacherConfiguration</c> Parent-visibility flag, reusing the existing
/// per-module student/parent services and repo methods. A module that errors degrades to
/// visible + empty for that section, never a whole-call 500.
/// </summary>
public interface IParentDashboardService
{
    /// <summary>
    /// Builds the consolidated dashboard for the JWT-resolved parent's child under the named
    /// teacher. Ownership is enforced via <c>IUserRepo.ResolveOwnedChildIdByTeacherStudentAsync</c>
    /// — codes are pure address resolution and never bypass the same ownership check the
    /// id-based Parent endpoints already apply.
    /// </summary>
    Task<Result<ParentChildTeacherDashboardDto>> GetTeacherDashboardAsync(
        long parentUserId, string teacherCode, string studentCode);
}
