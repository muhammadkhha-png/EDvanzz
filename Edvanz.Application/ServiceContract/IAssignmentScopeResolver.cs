using Edvanz.Application.Dtos.ExamHomework;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Shared scope-resolution helper for Module 6. Resolves <see cref="ScopeInputDto"/>
/// rows (or already-persisted <c>AssignmentScope</c> rows) to a deduplicated set of
/// <c>TeacherStudentId</c>s.
///
/// EXTRACTION RATIONALE:
/// Originally a private helper on <c>ExamHomeworkService</c>. Promoted to a public
/// contract so both the HTTP-driven service AND the Hangfire materializer can reuse
/// the same single source of truth for "who's in this scope". REQ-EXH-003 demands
/// dedupe across overlapping targets — implementing it twice would invite drift.
///
/// REUSES <c>IPaymentRepo.GetStudentIdsBySessionAsync</c> and
/// <c>GetStudentIdsByGroupAsync</c> as the underlying student lookups, matching the
/// project's "single source of truth for who's in a session" rule.
/// </summary>
public interface IAssignmentScopeResolver
{
    /// <summary>
    /// Resolves a list of input DTOs (used by Create and AddScopes flows) to a
    /// deduplicated set of student ids belonging to the teacher.
    /// </summary>
    Task<HashSet<long>> ResolveFromInputsAsync(
        long teacherId, IEnumerable<ScopeInputDto> scopes);

    /// <summary>
    /// Resolves persisted <c>AssignmentScope</c> rows to a deduplicated set of
    /// student ids. Used by the Hangfire materializer when generating the next
    /// occurrence of a recurring template — the scope has already been persisted,
    /// we just need to expand it as of the new occurrence's date.
    /// </summary>
    Task<HashSet<long>> ResolveFromPersistedScopesAsync(
        long teacherId, IEnumerable<Edvanz.Domain.Entities.AssignmentScope> scopes);
}

/// <summary>
/// Default implementation. Stateless, scoped lifetime — same as every other Application
/// service. Depends only on <see cref="IUnitOfWork"/>.
/// </summary>
public class AssignmentScopeResolver : IAssignmentScopeResolver
{
    private readonly IUnitOfWork _unitOfWork;

    public AssignmentScopeResolver(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<HashSet<long>> ResolveFromInputsAsync(
        long teacherId, IEnumerable<ScopeInputDto> scopes)
    {
        var ids = new HashSet<long>();

        foreach (var s in scopes)
        {
            IReadOnlyList<long> resolved = s.ScopeType switch
            {
                AssignmentScopeType.IndividualStudent
                    => new[] { s.TeacherStudentId!.Value },

                AssignmentScopeType.Session
                    => await _unitOfWork.PaymentsRepo
                        .GetStudentIdsBySessionAsync(teacherId, s.SessionId!.Value),

                AssignmentScopeType.SessionGroup
                    => await _unitOfWork.PaymentsRepo
                        .GetStudentIdsByGroupAsync(teacherId, s.SessionGroupId!.Value),

                _ => Array.Empty<long>(),
            };

            foreach (var id in resolved) ids.Add(id);
        }

        return ids;
    }

    /// <inheritdoc />
    public async Task<HashSet<long>> ResolveFromPersistedScopesAsync(
        long teacherId, IEnumerable<Edvanz.Domain.Entities.AssignmentScope> scopes)
    {
        var ids = new HashSet<long>();

        foreach (var s in scopes)
        {
            IReadOnlyList<long> resolved = s.ScopeType switch
            {
                AssignmentScopeType.IndividualStudent when s.TeacherStudentId.HasValue
                    => new[] { s.TeacherStudentId.Value },

                AssignmentScopeType.Session when s.SessionId.HasValue
                    => await _unitOfWork.PaymentsRepo
                        .GetStudentIdsBySessionAsync(teacherId, s.SessionId.Value),

                AssignmentScopeType.SessionGroup when s.SessionGroupId.HasValue
                    => await _unitOfWork.PaymentsRepo
                        .GetStudentIdsByGroupAsync(teacherId, s.SessionGroupId.Value),

                _ => Array.Empty<long>(),
            };

            foreach (var id in resolved) ids.Add(id);
        }

        return ids;
    }
}
