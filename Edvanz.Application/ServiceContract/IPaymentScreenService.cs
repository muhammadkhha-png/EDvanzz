using System.Threading.Tasks;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Payment;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Screen-oriented (BFF-style) payment endpoints backing the frontend payment.json spec
/// (api/v1/*). Additive and reuse-first: this service composes existing IPaymentRepo named
/// methods and (for money movement, Phase 2) IPaymentService — it never re-implements
/// payment mutation logic. <c>teacherId</c> is always supplied by the controller from the JWT.
/// </summary>
public interface IPaymentScreenService
{
    /// <summary>
    /// Screen: SessionPaymentCollectedByMonth. Paginated ledger of collected payments for the
    /// selected month. DASH-1: <paramref name="month"/> accepts EITHER the unified <c>"YYYY-MM"</c>
    /// string (same value <c>tracking</c>/<c>students</c> take) OR the legacy integer month string
    /// (1-12) paired with <paramref name="year"/>; both omitted → the teacher's current local
    /// (Africa/Cairo) month/year. Reuses <c>IPaymentRepo.GetTransactionsByDateRangePagedAsync</c>.
    /// Returns 422 for an invalid month/year.
    /// </summary>
    Task<Result<CollectionsByMonthResponse>> GetCollectionsByMonthAsync(
        long teacherId, string? month, int? year, int page, int limit);

    /// <summary>
    /// Screen: AssistantWallet. Wallet card + paginated recent collections for one assistant.
    /// Reuses <c>GetAssistantWalletAsync</c> (tenant-scoped → 404 if not this teacher's) and
    /// <c>GetCollectorTransactionsPagedAsync</c>. Tutor-only at the controller.
    /// </summary>
    Task<Result<AssistantWalletScreenResponse>> GetAssistantWalletScreenAsync(
        long teacherId, long assistantId, int page, int limit);

    /// <summary>
    /// Screen: CollectPayment. Searchable/filterable (all|assigned|unassigned) paginated
    /// student list with payment status + per-tab counts. Reuses <c>GetCollectStudentsPagedAsync</c>.
    /// Returns 422 for an invalid filter.
    /// </summary>
    Task<Result<CollectStudentsResponse>> GetCollectStudentsAsync(
        long teacherId, string? filter, string? search, int page, int limit);

    /// <summary>
    /// Screen: PaymentTracking "View" cards. Paginated students filtered by status
    /// (paid|prorated|unpaid) for a month (YYYY-MM), with per-student month amounts and
    /// group totals. Reuses <c>GetStudentsByPaymentStatusPagedAsync</c>. 422 on bad month/status.
    /// </summary>
    Task<Result<StudentsByStatusResponse>> GetStudentsByStatusAsync(
        long teacherId, string? month, string? status, int page, int limit);

    /// <summary>
    /// Screen: SessionPaymentCollectedByYear. Per-student month-by-month collection matrix for a
    /// year, paginated over students. DASH-1: the year is derived from EITHER the unified
    /// <c>"YYYY-MM"</c> <paramref name="month"/> selector (its year component) OR the legacy
    /// <paramref name="year"/> integer; both omitted → the teacher's current local (Africa/Cairo)
    /// year. Reuses <c>GetYearlyCollectionsPagedAsync</c>. 422 on bad month/year.
    /// </summary>
    Task<Result<YearlyCollectionsResponse>> GetYearlyCollectionsAsync(
        long teacherId, string? month, int? year, int page, int limit);

    /// <summary>
    /// Screen: CollectPaymentSession. Resolves a student by QR/code/name → student + amount owed
    /// + paid state. Reuses <c>ResolveCollectLookupAsync</c>. 422 when no key given; 404 when unmatched.
    /// </summary>
    Task<Result<CollectLookupResponse>> ResolveLookupAsync(
        long teacherId, string? qr, string? code, string? name);

    /// <summary>
    /// Screen: PaymentTracking. Month aggregate (summary revenue, status breakdown, collected
    /// by assistant, collected by sessions) for the given YYYY-MM. Composed entirely from
    /// existing dashboard/collector/status repo methods. 422 on bad month.
    /// </summary>
    Task<Result<TrackingResponse>> GetTrackingAsync(long teacherId, string? month);

    /// <summary>
    /// Screen: CollectPayment "Mark N as Paid" (MONEY). Marks each student paid by routing through
    /// the existing <c>CollectPaymentAsync</c> (clears their earliest unpaid period). Idempotent via
    /// <paramref name="idempotencyKey"/>. 422 when no students selected.
    /// </summary>
    Task<Result<MarkPaidResponse>> MarkPaidAsync(
        long teacherId, long actingUserId, List<long> studentIds, string? idempotencyKey);

    /// <summary>
    /// Screen: CollectPaymentSession "Submit N students" (MONEY). Collects each {studentId, amount}
    /// via <c>CollectPaymentAsync</c>. Idempotent via <paramref name="idempotencyKey"/>. 409 on empty batch.
    /// </summary>
    Task<Result<SubmitCollectionResponse>> SubmitCollectionAsync(
        long teacherId, long actingUserId, string? month, long? classSessionId,
        List<SubmitCollectionItem> students, string? idempotencyKey);

    /// <summary>
    /// Screen: AssistantWallet "Withdraw" (MONEY, tutor-only). Partial wallet withdrawal delegating
    /// to <c>IPaymentService.WithdrawFromWalletAsync</c>. Idempotent via <paramref name="idempotencyKey"/>.
    /// 404 unknown wallet, 409 insufficient balance / concurrent update, 422 non-positive amount.
    /// </summary>
    Task<Result<WalletWithdrawResponse>> WithdrawAsync(
        long teacherId, long assistantId, decimal? amount, long actingUserId, string? idempotencyKey);
}
