using Edvanz.Application.Dtos.VideoContentManagement;
using Edvanz.Domain.Entities;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Resolves <see cref="VideoScopeInputDto"/> rows (or already-persisted
/// <c>VideoScope</c> rows) to a deduplicated set of <c>TeacherStudentId</c>s.
///
/// EXTRACTION RATIONALE:
/// Mirrors <see cref="IAssignmentScopeResolver"/> — Module 6's pattern. The
/// resolver is a public Application contract (not Infrastructure) because:
/// <list type="number">
///   <item>It orchestrates named repo methods rather than running raw EF
///         queries — pure Application logic.</item>
///   <item>Two callers will use it: the HTTP service (Story A scope-assign,
///         Story D PUT-replace, Story F analytics) AND the
///         <c>POST /start</c> access check. Implementing scope resolution
///         twice would invite drift on a security-critical path.</item>
/// </list>
///
/// REUSES <c>IPaymentRepo.GetStudentIdsBySessionAsync</c> and
/// <c>IPaymentRepo.GetStudentIdsByGroupAsync</c> — same as
/// <see cref="IAssignmentScopeResolver"/>. These are the project's single
/// source of truth for "who is in this session/group right now". Re-running
/// the resolver after the underlying session membership changes always
/// produces the current correct set, which is the BR-VCM-01 expectation
/// (scope is re-resolved on every access, no caching).
///
/// Stateless. Scoped lifetime, registered alongside other Application
/// services in <c>Program.cs</c>.
/// </summary>
public interface IVideoScopeResolver
{
    /// <summary>
    /// Resolves a list of input DTOs (used by the create / append-scope /
    /// PUT-replace flows) to a deduplicated set of student ids belonging to
    /// the teacher.
    ///
    /// VALIDATION NOTE: this method does NOT verify scope-target ownership.
    /// The caller (service layer) calls
    /// <see cref="IVideoAssetRepo.IsScopeTargetOwnedByTeacherAsync"/> on
    /// each input row BEFORE calling this resolver, returning
    /// <c>ScopeTargetNotFoundOrForeign</c> on any failure.
    /// </summary>
    Task<HashSet<long>> ResolveFromInputsAsync(
        long teacherId, IEnumerable<VideoScopeInputDto> scopes);

    /// <summary>
    /// Resolves persisted <see cref="VideoScope"/> rows to a deduplicated
    /// set of student ids. Used by the analytics report (Story F) and the
    /// student access-check pre-flight when the scope is already on disk.
    /// </summary>
    Task<HashSet<long>> ResolveFromPersistedScopesAsync(
        long teacherId, IEnumerable<VideoScope> scopes);

    /// <summary>
    /// Resolves persisted <see cref="VideoUnitScope"/> rows (collection-level
    /// Target Scope) to a deduplicated set of student ids. Same resolution
    /// rules as <see cref="ResolveFromPersistedScopesAsync"/> — kept as a
    /// separate overload rather than a shared base type because the two
    /// entities are distinct EF Core aggregates.
    /// </summary>
    Task<HashSet<long>> ResolveFromPersistedUnitScopesAsync(
        long teacherId, IEnumerable<VideoUnitScope> scopes);
}
