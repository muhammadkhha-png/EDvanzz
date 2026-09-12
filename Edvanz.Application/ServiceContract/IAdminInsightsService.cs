using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AdminInsights;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// The SuperAdmin insights surface: who is actually using Edvanz, how often, how deeply, and who on
/// the account is doing the work — plus the sales attribution and internal notes that turn those
/// numbers into a conversation someone can have.
///
/// Reads come from the pre-computed <c>TeacherUsageSnapshots</c> table, rebuilt nightly by
/// <see cref="ITeacherUsageRollupService"/>. Nothing here aggregates raw domain tables at request
/// time.
/// </summary>
public interface IAdminInsightsService
{
    /// <summary>
    /// THE LANDING PAGE — how the subscribed base is doing, and where the product is not landing.
    /// Scoped to subscribers by default; pass false to fold in free and expired accounts.
    /// </summary>
    Task<Result<AdminNumbersDto>> GetNumbersAsync(bool subscribedOnly);

    /// <summary>
    /// THE CALL LIST — the admin landing page. One ranked, DEDUPLICATED list answering the only
    /// question that screen exists for: which teachers do I contact today, and what do I say?
    ///
    /// Each teacher appears exactly once, under their single most urgent reason, with the evidence
    /// in plain words and a concrete action. Replaces the nine parallel insight cards, which held
    /// 241 entries across 171 teachers with the same people on several lists, no ordering, and
    /// nothing saying what to do.
    /// </summary>
    Task<Result<CallListDto>> GetCallListAsync(string? reasonKey, int take);

    /// <summary>
    /// The platform numbers — distributions and module adoption. Secondary by design: this is a
    /// once-a-month question, and it used to crowd the daily one off its own page.
    /// </summary>
    Task<Result<AdminOverviewDto>> GetOverviewAsync();

    /// <summary>One page of the usage grid, filtered and sorted in SQL.</summary>
    Task<Result<PaginatedResponse<List<TeacherUsageListItemDto>>>> GetUsageGridAsync(
        TeacherUsageQueryRequest request);

    /// <summary>Everything the Teacher 360 usage tab draws for one teacher.</summary>
    Task<Result<TeacherUsageDetailDto>> GetTeacherUsageAsync(long teacherId);

    /// <summary>
    /// The full named list behind one insight card, paged — what "see all 34" opens.
    /// </summary>
    Task<Result<PaginatedResponse<List<TeacherUsageListItemDto>>>> GetInsightTeachersAsync(
        string insightKey, int page, int pageSize);

    /// <summary>
    /// Recomputes one teacher's usage on demand, instead of waiting for tonight's run. Used after a
    /// support action, or when an admin distrusts a number in front of them.
    /// </summary>
    Task<Result<TeacherUsageDetailDto>> RecomputeTeacherUsageAsync(long teacherId, int days);

    /// <summary>
    /// Exports the CURRENTLY FILTERED teachers as a CSV, with everything needed to identify and
    /// contact each one — including their admin notes.
    ///
    /// Deliberately exports the whole filtered set, not the page on screen: the point of an export
    /// is to hand a rep their call list, and a list truncated at 25 rows is worse than none. Bounded
    /// by <c>AdminInsightsConstants.CsvExportMaxRows</c>.
    /// </summary>
    Task<Result<byte[]>> ExportTeachersCsvAsync(TeacherUsageQueryRequest request);

    // ── The console ────────────────────────────────────────────────────────────

    /// <summary>
    /// THE DASHBOARD — one call behind the whole landing page: what changed yesterday (with names),
    /// how the business is moving, whether subscribers are using it, feature by feature, the money,
    /// and the platform footer.
    ///
    /// Everything is scoped to INDEPENDENT teachers; centre-owned teachers get their own card and
    /// their money stays on the Centres page. <paramref name="windowDays"/> (7 / 30 / 90) governs
    /// every "newly" figure and every delta.
    /// </summary>
    Task<Result<AdminDashboardDto>> GetDashboardAsync(int windowDays);

    /// <summary>Registrations, new subscriptions and live subscribers per period — 12 weeks or 12 months.</summary>
    Task<Result<AdminTrendsDto>> GetTrendsAsync(string? granularity);

    /// <summary>
    /// The named people behind ANY dashboard number, paged. One endpoint for every card, because a
    /// count someone cannot open is a number to study rather than one to work.
    ///
    /// <paramref name="key"/> is a <c>AdminSegmentKey</c> name; the two parameterised families carry
    /// their argument after a colon (<c>ModuleUsing:Videos</c>, <c>Renewed:2026-08</c>).
    /// </summary>
    /// <param name="search">
    /// Optional. Narrows the list to matching name, teacher code, username or phone, Arabic
    /// variants folded. A drill-down can run to hundreds of people — someone looking for one of
    /// them should not have to page through the rest.
    /// </param>
    Task<Result<PaginatedResponse<List<AdminSegmentTeacherDto>>>> GetSegmentAsync(
        string key, int windowDays, int page, int pageSize, string? search);

    /// <summary>
    /// The WHOLE segment as a CSV — the same people, the same order, the same filter, with the
    /// contact details and account state a rep needs on the phone.
    ///
    /// Exports everything rather than the page on screen: the point of an export is to hand
    /// someone their call list, and a list cut off at fifteen rows is worse than none.
    /// </summary>
    Task<Result<byte[]>> ExportSegmentCsvAsync(string key, int windowDays, string? search);

    /// <summary>Renewed vs churned per month, plus the trial-conversion split.</summary>
    Task<Result<AdminRenewalsDto>> GetRenewalsAsync(int months);

    /// <summary>
    /// Global search across teachers, roster students, student app accounts and assistants.
    /// Grouped top-few per kind; Arabic variants folded on every group.
    /// </summary>
    Task<Result<AdminSearchDto>> SearchAsync(string? query, int takePerGroup);

    /// <summary>
    /// Sign-in history for everyone on a teacher's account. Teachers carry only a last-login —
    /// the platform has never recorded a per-login row for them — and the response says so.
    /// </summary>
    Task<Result<AdminTeacherLoginsDto>> GetTeacherLoginsAsync(long teacherId);

    /// <summary>What the teacher's account contains, read-only: classes, content, accounts.</summary>
    Task<Result<AdminTeacherSnapshotDto>> GetTeacherSnapshotAsync(long teacherId);

    // ── Sales attribution ──────────────────────────────────────────────────────

    /// <summary>Every sales rep with their book of accounts rolled up by outcome.</summary>
    Task<Result<List<SalesRepDto>>> GetSalesRepsAsync(bool includeInactive);

    /// <summary>Adds a sales rep.</summary>
    Task<Result<SalesRepDto>> CreateSalesRepAsync(SaveSalesRepRequest request);

    /// <summary>Updates a sales rep. Deactivating keeps their name on the teachers they brought in.</summary>
    Task<Result<SalesRepDto>> UpdateSalesRepAsync(long salesRepId, SaveSalesRepRequest request);

    /// <summary>Assigns or clears a teacher's sales attribution and acquisition source.</summary>
    Task<Result<TeacherUsageListItemDto>> AssignSalesRepAsync(long teacherId, AssignSalesRepRequest request);

    // ── Internal notes ─────────────────────────────────────────────────────────

    /// <summary>A teacher's notes, pinned first then newest.</summary>
    Task<Result<List<AdminNoteDto>>> GetNotesAsync(long teacherId);

    /// <summary>Adds a note, stamped with the acting admin's id and name.</summary>
    Task<Result<AdminNoteDto>> CreateNoteAsync(long teacherId, CreateAdminNoteRequest request, long authorUserId);

    /// <summary>Soft-deletes a note. The row survives for audit.</summary>
    Task<Result<bool>> DeleteNoteAsync(long teacherId, long noteId);
}
