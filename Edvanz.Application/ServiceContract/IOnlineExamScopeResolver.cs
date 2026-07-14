using Edvanz.Application.Dtos;
using Edvanz.Domain.Entities;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Resolves persisted <see cref="OnlineExamScope"/> rows to a deduplicated set of
/// <c>TeacherStudentId</c>s. Mirrors <see cref="IVideoScopeResolver"/> minus the
/// individual-student branch (§1). Public Application contract, not Infrastructure —
/// orchestrates named repo methods rather than raw EF queries.
///
/// REUSES <c>IPaymentRepo.GetStudentIdsBySessionAsync</c> /
/// <c>GetStudentIdsByGroupAsync</c> — the project's single source of truth for
/// "who is in this session/group right now" (§3.1, BR-VCM-01 pattern). Never
/// materialized/cached — re-resolved live on every call.
///
/// PHASE NOTE: only the persisted-entity overload is declared here. The input-DTO
/// overload (used by create/append-scope flows) is added in Phase 2 once
/// <c>OnlineExamScopeInputDto</c> exists — not invented ahead of the DTOs.
/// </summary>
public interface IOnlineExamScopeResolver
{
    Task<HashSet<long>> ResolveFromInputsAsync(
         long teacherId, IEnumerable<OnlineExamScopeInputDto> scopes);

    Task<HashSet<long>> ResolveFromPersistedScopesAsync(
        long teacherId, IEnumerable<OnlineExamScope> scopes);
}