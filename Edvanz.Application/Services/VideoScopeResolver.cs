using Edvanz.Application.Dtos.VideoContentManagement;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;

namespace Edvanz.Application.Services;

/// <summary>
/// Default <see cref="IVideoScopeResolver"/> implementation. Resolves
/// <see cref="VideoScopeInputDto"/> rows or persisted
/// <see cref="VideoScope"/> rows to a deduplicated set of student ids.
///
/// Stateless. Scoped lifetime — same as <see cref="AssignmentScopeResolver"/>,
/// the Module 6 analog this mirrors.
///
/// REUSE: delegates session and group expansion to
/// <c>IPaymentRepo.GetStudentIdsBySessionAsync</c> /
/// <c>GetStudentIdsByGroupAsync</c>. These are the project's single source
/// of truth for "who is in this session/group right now". Re-running the
/// resolver after the underlying session membership changes always returns
/// the current correct set, which is the BR-VCM-01 expectation (scope is
/// re-resolved on every access — no caching).
/// </summary>
public sealed class VideoScopeResolver : IVideoScopeResolver
{
    private readonly IUnitOfWork _unitOfWork;

    public VideoScopeResolver(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<HashSet<long>> ResolveFromInputsAsync(
        long teacherId, IEnumerable<VideoScopeInputDto> scopes)
    {
        var ids = new HashSet<long>();

        foreach (var s in scopes)
        {
            IReadOnlyList<long> resolved = s.ScopeType switch
            {
               

                VideoScopeType.Session when s.SessionId.HasValue
                    => await _unitOfWork.PaymentsRepo
                        .GetStudentIdsBySessionAsync(teacherId, s.SessionId.Value),

                VideoScopeType.SessionGroup when s.SessionGroupId.HasValue
                    => await _unitOfWork.PaymentsRepo
                        .GetStudentIdsByGroupAsync(teacherId, s.SessionGroupId.Value),

                _ => Array.Empty<long>(),
            };

            foreach (var id in resolved) ids.Add(id);
        }

        return ids;
    }

    /// <inheritdoc />
    public async Task<HashSet<long>> ResolveFromPersistedScopesAsync(
        long teacherId, IEnumerable<VideoScope> scopes)
    {
        var ids = new HashSet<long>();

        foreach (var s in scopes)
        {
            IReadOnlyList<long> resolved = s.ScopeType switch
            {
              

                VideoScopeType.Session when s.SessionId.HasValue
                    => await _unitOfWork.PaymentsRepo
                        .GetStudentIdsBySessionAsync(teacherId, s.SessionId.Value),

                VideoScopeType.SessionGroup when s.SessionGroupId.HasValue
                    => await _unitOfWork.PaymentsRepo
                        .GetStudentIdsByGroupAsync(teacherId, s.SessionGroupId.Value),

                _ => Array.Empty<long>(),
            };

            foreach (var id in resolved) ids.Add(id);
        }

        return ids;
    }

}
