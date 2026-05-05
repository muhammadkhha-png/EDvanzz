using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ExamHomework;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Application-layer contract for the Exams &amp; Homework Module (Module 6).
/// Covers the entire lifecycle of assignment templates, scopes, occurrences,
/// student obligations, status entry, grading, scanning, and audit history.
/// Reports are handled by <see cref="IExamHomeworkReportService"/>.
///
/// All methods return <see cref="Result{T}"/>. Failures use localized message keys
/// (see Messages_en.resx / Messages_ar.resx).
///
/// All write operations capture the acting user id from the caller (controller resolves
/// it from JWT via <c>ICurrentUserService</c>) for audit trail. The teacherId argument
/// is also resolved from JWT — assistants pass their owning tutor's id.
/// REQ-EXH-NFR-004: All data is multi-tenant scoped to teacherId.
/// </summary>
public interface IExamHomeworkService
{
    // ──────────────────────────────────────────────────────────────────
    // TEMPLATE LIFECYCLE
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new assignment template, materializes its first occurrence
    /// synchronously, and resolves+inserts obligations for every student in scope.
    /// REQ-EXH-001 through 010, 014/015, 020/021, REQ-EXH-007.
    /// </summary>
    Task<Result<AssignmentTemplateDto>> CreateTemplateAsync(
        long teacherId, long actingUserId, CreateAssignmentTemplateDto dto);

    /// <summary>
    /// Returns the paginated Assignment Overview list (REQ-EXH-033).
    /// Per-row aggregate counts are computed in O(2) queries (list + grouped summary)
    /// rather than O(N+1).
    /// </summary>
    Task<Result<PaginatedResponse<List<AssignmentOverviewItemDto>>>> GetOverviewAsync(
        long teacherId, AssignmentOverviewRequest request);

    /// <summary>
    /// Returns one template with its scopes hydrated to readable identifiers
    /// (student name, session name, group name). REQ-EXH-029, 034.
    /// </summary>
    Task<Result<AssignmentTemplateDto>> GetTemplateByIdAsync(long teacherId, long templateId);

    /// <summary>
    /// Edits an existing template (REQ-EXH-034).
    /// REQ-EXH-013: Recurrence pattern change is rejected when student data has been recorded.
    /// Snapshots on existing occurrences are NOT overwritten — they remain stable for
    /// historical reproducibility (decoupling decision documented in catalog §3.1 endpoint 4).
    /// </summary>
    Task<Result<AssignmentTemplateDto>> UpdateTemplateAsync(
        long teacherId, long actingUserId, long templateId, UpdateAssignmentTemplateDto dto);

    /// <summary>
    /// Hard-deletes a template, cascading to scopes/occurrences/obligations.
    /// Persists a JSON snapshot to <c>AssignmentDeletionLogs</c> first. REQ-EXH-037.
    /// </summary>
    Task<Result<bool>> DeleteTemplateAsync(
        long teacherId, long actingUserId, long templateId, bool confirm);

    /// <summary>
    /// Stops recurrence on a recurring template; preserves all existing occurrences.
    /// REQ-EXH-012, BR-EXH-002.
    /// </summary>
    Task<Result<StopRecurrenceResultDto>> StopRecurrenceAsync(
        long teacherId, long actingUserId, long templateId, StopRecurrenceDto dto);

    /// <summary>
    /// Lists all occurrences for a template, ordered by OccurrenceNumber. REQ-EXH-011.
    /// </summary>
    Task<Result<PaginatedResponse<List<OccurrenceSummaryItemDto>>>> GetOccurrencesAsync(
        long teacherId, long templateId, int page, int pageSize);

    // ──────────────────────────────────────────────────────────────────
    // SCOPE MANAGEMENT
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds new scope rows (session / group / individual student) to an existing template.
    /// Resolves new students against the next future occurrence and creates obligations.
    /// REQ-EXH-035.
    /// </summary>
    Task<Result<AddScopesResultDto>> AddScopesAsync(
        long teacherId, long actingUserId, long templateId, AddScopesDto dto);

    /// <summary>
    /// Removes a scope row from a template. Existing obligations from past/current
    /// occurrences are preserved (BR-EXH-002). Future occurrences will not include
    /// students that were only resolvable via this scope. REQ-EXH-034.
    /// </summary>
    Task<Result<bool>> RemoveScopeAsync(
        long teacherId, long actingUserId, long templateId, long scopeId);

    /// <summary>
    /// Adds specific students (IndividualStudent scope rows) to a template.
    /// Idempotent — re-running with the same payload is a no-op via the unique index.
    /// REQ-EXH-035, BR-EXH-005.
    /// </summary>
    Task<Result<AddStudentsResultDto>> AddStudentsToTemplateAsync(
        long teacherId, long actingUserId, long templateId, AddStudentsToTemplateDto dto);

    /// <summary>
    /// Returns paginated students who are NOT yet in this template's resolved scope.
    /// Used by the add-students picker UX. REQ-EXH-035.
    /// </summary>
    Task<Result<PaginatedResponse<List<EligibleStudentDto>>>> GetEligibleStudentsAsync(
        long teacherId, long templateId, EligibleStudentsRequest request);

    /// <summary>
    /// Removes a student from a template's scope across all future/current occurrences.
    /// If recorded data exists, requires <paramref name="force"/> = true. REQ-EXH-036.
    /// </summary>
    Task<Result<RemoveStudentResultDto>> RemoveStudentFromTemplateAsync(
        long teacherId, long actingUserId, long templateId, long teacherStudentId, bool force);

    /// <summary>
    /// Removes a single obligation row (excused-from-one-occurrence flow). REQ-EXH-036.
    /// </summary>
    Task<Result<bool>> RemoveObligationAsync(
        long teacherId, long actingUserId, long obligationId, bool force);

    // ──────────────────────────────────────────────────────────────────
    // OCCURRENCE / TRACKING
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Get one occurrence with snapshot fields. REQ-EXH-011, 029.</summary>
    Task<Result<OccurrenceDetailDto>> GetOccurrenceAsync(long teacherId, long occurrenceId);

    /// <summary>Tracking-view header counts in one grouped query. REQ-EXH-029.</summary>
    Task<Result<OccurrenceSummaryDto>> GetOccurrenceSummaryAsync(long teacherId, long occurrenceId);

    /// <summary>Tracking View list with all five filters. REQ-EXH-030/031/032.</summary>
    Task<Result<PaginatedResponse<List<TrackingViewRowDto>>>> GetTrackingViewAsync(
        long teacherId, long occurrenceId, TrackingViewRequest request);

    /// <summary>Type-ahead student picker for manual entry by name. REQ-EXH-023.</summary>
    Task<Result<List<StudentPickerRowDto>>> SearchStudentsInOccurrenceAsync(
        long teacherId, long occurrenceId, string query, int limit);

    // ──────────────────────────────────────────────────────────────────
    // STATUS / GRADE ENTRY  (Option B — split exam vs homework endpoints)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Manual exam status entry by name pick. REQ-EXH-023, 028.
    /// Validates that the occurrence is an Exam and that the supplied status is exam-valid.
    /// </summary>
    Task<Result<TrackingViewRowDto>> UpdateExamStatusAsync(
        long teacherId, long actingUserId, long occurrenceId, long teacherStudentId,
        UpdateExamStatusDto dto);

    /// <summary>
    /// Manual homework status entry by name pick. REQ-EXH-023, 028.
    /// Validates the occurrence is a Homework and the supplied status matches the
    /// snapshotted TrackingMode.
    /// </summary>
    Task<Result<TrackingViewRowDto>> UpdateHomeworkStatusAsync(
        long teacherId, long actingUserId, long occurrenceId, long teacherStudentId,
        UpdateHomeworkStatusDto dto);

    /// <summary>Manual exam status entry by student code. REQ-EXH-024.</summary>
    Task<Result<TrackingViewRowDto>> UpdateExamStatusByCodeAsync(
        long teacherId, long actingUserId, long occurrenceId, UpdateStatusByCodeDto dto);

    /// <summary>Manual homework status entry by student code. REQ-EXH-024.</summary>
    Task<Result<TrackingViewRowDto>> UpdateHomeworkStatusByCodeAsync(
        long teacherId, long actingUserId, long occurrenceId, UpdateStatusByCodeDto dto);

    /// <summary>Bulk multi-select status entry. REQ-EXH-025.</summary>
    Task<Result<BulkStatusResultDto>> BulkUpdateStatusAsync(
        long teacherId, long actingUserId, long occurrenceId, BulkUpdateStatusDto dto);

    /// <summary>
    /// Idempotent atomic barcode-scan mark.
    /// REQ-EXH-026, REQ-EXH-NFR-002. Server-stamps scannedAt — no client-supplied timestamp.
    /// </summary>
    Task<Result<ScanResultDto>> ScanBarcodeAsync(
        long teacherId, long actingUserId, long occurrenceId, ScanBarcodeDto dto);

    // ──────────────────────────────────────────────────────────────────
    // GRADE ENTRY VIEW
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Grade Entry View list — only obligations awaiting grade entry. REQ-EXH-026-A.</summary>
    Task<Result<GradeEntryViewDto>> GetPendingGradesAsync(
        long teacherId, long occurrenceId, GradeEntryRequest request);

    /// <summary>Enter or amend a grade for an obligation. REQ-EXH-026-D, 026-E, 027.</summary>
    Task<Result<TrackingViewRowDto>> EnterGradeAsync(
        long teacherId, long actingUserId, long obligationId, EnterGradeDto dto);

    // ──────────────────────────────────────────────────────────────────
    // AUDIT
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Paginated audit history for a single obligation. REQ-EXH-028.</summary>
    Task<Result<PaginatedResponse<List<ObligationAuditEntryDto>>>> GetObligationAuditLogAsync(
        long teacherId, long obligationId, int page, int pageSize);
}
