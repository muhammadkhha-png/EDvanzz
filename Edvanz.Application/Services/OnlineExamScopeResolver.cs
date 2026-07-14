using Edvanz.Application.Dtos;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;

namespace Edvanz.Application.Services;

/// <summary>
/// Default <see cref="IOnlineExamScopeResolver"/> implementation. Mirrors
/// <see cref="VideoScopeResolver"/> minus the individual-student branch (§1).
/// Stateless, scoped lifetime. Reuses <c>IPaymentRepo.GetStudentIdsBySessionAsync</c>
/// / <c>GetStudentIdsByGroupAsync</c> — the project's single source of truth for
/// "who is in this session/group right now" (§3.1).
/// </summary>
public sealed class OnlineExamScopeResolver : IOnlineExamScopeResolver
{
    private readonly IUnitOfWork _unitOfWork;

    public OnlineExamScopeResolver(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<HashSet<long>> ResolveFromInputsAsync(
        long teacherId, IEnumerable<OnlineExamScopeInputDto> scopes)
    {
        var ids = new HashSet<long>();

        foreach (var s in scopes)
        {
            IReadOnlyList<long> resolved = s.ScopeType switch
            {
                OnlineExamScopeType.Session when s.SessionId.HasValue
                    => await _unitOfWork.PaymentsRepo
                        .GetStudentIdsBySessionAsync(teacherId, s.SessionId.Value),

                OnlineExamScopeType.SessionGroup when s.SessionGroupId.HasValue
                    => await _unitOfWork.PaymentsRepo
                        .GetStudentIdsByGroupAsync(teacherId, s.SessionGroupId.Value),

                _ => Array.Empty<long>(),
            };

            foreach (var id in resolved) ids.Add(id); // union + dedup
        }

        return ids;
    }

    /// <inheritdoc />
    public async Task<HashSet<long>> ResolveFromPersistedScopesAsync(
        long teacherId, IEnumerable<OnlineExamScope> scopes)
    {
        var ids = new HashSet<long>();

        foreach (var s in scopes)
        {
            IReadOnlyList<long> resolved = s.ScopeType switch
            {
                OnlineExamScopeType.Session when s.SessionId.HasValue
                    => await _unitOfWork.PaymentsRepo
                        .GetStudentIdsBySessionAsync(teacherId, s.SessionId.Value),

                OnlineExamScopeType.SessionGroup when s.SessionGroupId.HasValue
                    => await _unitOfWork.PaymentsRepo
                        .GetStudentIdsByGroupAsync(teacherId, s.SessionGroupId.Value),

                _ => Array.Empty<long>(),
            };

            foreach (var id in resolved) ids.Add(id); // union + dedup — session∩group naturally collapses via HashSet
        }

        return ids;
    }
}