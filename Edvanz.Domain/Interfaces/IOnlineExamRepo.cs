using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Extended repository for the Online Exam Module — teacher-side aggregate
/// (<see cref="OnlineExam"/> + its Questions/Options/Scopes children).
///
/// Inherits <see cref="IGenericRepo{T, Tkey}"/> over <see cref="OnlineExam"/> for
/// basic CRUD on the primary entity — same pattern as <c>IExamHomeworkRepo</c> /
/// <c>IVideoAssetRepo</c>. Child entities are accessed via named methods; no raw
/// predicates cross into the Application layer.
///
/// PHASE NOTE: this interface carries only the CRUD-support surface needed for the
/// Phase 1 migration gate. Query methods for §3.1 (live assigned set), §3.2 (count
/// reconciliation), §3.3 (grouped list), and the questions±IsCorrect projections
/// land in Phase 2 against verified signatures — not invented here.
/// </summary>
public interface IOnlineExamRepo : IGenericRepo<OnlineExam, long>
{
    /// <summary>Loads an exam scoped to its owning teacher. Returns null on mismatch/not-found.</summary>
    Task<OnlineExam?> GetByIdAndTeacherAsync(long onlineExamId, long teacherId);

    Task AddQuestionsRangeAsync(IEnumerable<OnlineExamQuestion> questions);

    Task AddScopesRangeAsync(IEnumerable<OnlineExamScope> scopes);

    /// <summary>
    /// Purges an exam's full graph leaf-first (§3.7), in the caller's ambient
    /// transaction: StudentQuestionAnswerOptions → StudentQuestionAnswers →
    /// StudentOnlineExamReports → OnlineExamQuestionOptions → OnlineExamQuestions →
    /// OnlineExamScopes → OnlineExam. Pattern: <c>ExamHomeworkRepo.PurgeExamGraphAsync</c>.
    /// Every child FK is NoAction, so nothing cascades on its own — this method owns
    /// the full teardown via set-based <c>ExecuteDeleteAsync</c> calls.
    /// </summary>
    Task PurgeExamGraphAsync(long onlineExamId);
    // ══════════════════════════════════════════════════════════════════════
    // §3.1 — LIVE ASSIGNED SET (BR-VCM-01 pattern: composable, never materialized)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Composable query resolving the current assigned-student set for an exam —
    /// union of the Session branch (OnlineExamScopes⋈TeacherStudents on SessionId)
    /// and the SessionGroup branch (OnlineExamScopes⋈Sessions⋈TeacherStudents).
    /// Caller composes further (Count/Contains/Except) — never enumerated here.
    /// Reuses the <c>VideoAssetRepo.GetResolvedStudentIdsForVideoQuery</c> shape.
    /// </summary>
    IQueryable<long> BuildAssignedStudentIdsQuery(long onlineExamId, long teacherId);

    // ══════════════════════════════════════════════════════════════════════
    // §3.3 — GROUPED SCOPE FETCH (list page, 2 queries not 2N)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Batch scope fetch across a page of exam ids, Session/SessionGroup eager-loaded.</summary>
    Task<IReadOnlyList<OnlineExamScope>> GetScopesByExamIdsAsync(IEnumerable<long> onlineExamIds);

    // ══════════════════════════════════════════════════════════════════════
    // QUESTIONS + OPTIONS — WITH / WITHOUT IsCorrect
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Teacher-facing: full projection including IsCorrect (T10/T12).</summary>
    Task<IReadOnlyList<OnlineExamQuestionRow>> GetQuestionsForTeacherAsync(long onlineExamId);

    /// <summary>Student-facing: dedicated projection with no IsCorrect field on the shape at all (S2).</summary>
    Task<IReadOnlyList<StudentOnlineExamQuestionRow>> GetQuestionsForStudentAsync(long onlineExamId);

    /// <summary>Sum of Question.Degree — the exam's live-computed total grade (never stored, do-not-reintroduce #6).</summary>
    Task<decimal> GetTotalDegreeAsync(long onlineExamId);

    /// <summary>Session/SessionGroup ownership check for a scope target (mirrors IVideoAssetRepo.IsScopeTargetOwnedByTeacherAsync).</summary>
    Task<bool> IsScopeTargetOwnedByTeacherAsync(long teacherId, OnlineExamScopeType scopeType, long targetId);

    Task<(IReadOnlyList<OnlineExam> Items, int TotalCount)> GetPagedForTeacherAsync(
        long teacherId, OnlineExamStatus? status, string? search, int page, int pageSize);

    /// <summary>§3.3 T4 batch — one grouped query for a page's assigned counts, not one call per row.</summary>
    Task<Dictionary<long, int>> GetAssignedCountsByExamIdsAsync(IEnumerable<long> onlineExamIds, long teacherId);

    /// <summary>T7 grid — assigned-student identities with name/code, materialized (small sets only — one exam's roster).</summary>
    Task<IReadOnlyList<AssignedStudentRow>> GetAssignedStudentsAsync(long onlineExamId, long teacherId);

    /// <summary>T13 replace-all questions (Draft-only) — leaf-first delete then insert, in the service's transaction.</summary>
    Task ReplaceQuestionsAsync(long onlineExamId, IEnumerable<OnlineExamQuestion> newQuestions);
    /// <summary>
    /// §T6 overview — per-scope assigned counts in 2 grouped queries (one for Session
    /// scopes, one for SessionGroup scopes), not one query per scope row.
    /// </summary>
    Task<Dictionary<long, int>> GetAssignedCountsPerScopeAsync(long onlineExamId, long teacherId);
    /// <summary>Student → Session/SessionGroup context, for reverse scope resolution (student sees "my exams").</summary>
    Task<(long? SessionId, long? SessionGroupId)> GetStudentSessionContextAsync(long teacherStudentId);

    /// <summary>
    /// Reverse of the teacher-side resolver: exams (any non-Draft status) whose scope
    /// includes this student's session or session-group, for this teacher. Questions
    /// eager-loaded (small per-exam cardinality) so the caller can compute Count/ΣDegree
    /// without extra round trips.
    /// </summary>
    Task<IReadOnlyList<OnlineExam>> GetExamsAssignedToStudentAsync(
        long teacherId, long? studentSessionId, long? studentSessionGroupId);
    /// <summary>T5s ownership guard — teacherStudentId must belong to this teacher.</summary>
    Task<bool> IsTeacherStudentOwnedByTeacherAsync(long teacherStudentId, long teacherId);
}