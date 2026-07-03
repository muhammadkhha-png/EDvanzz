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
    /// given month (1-12) + year. Reuses <c>IPaymentRepo.GetTransactionsByDateRangePagedAsync</c>.
    /// Returns 422 for an invalid month/year.
    /// </summary>
    Task<Result<CollectionsByMonthResponse>> GetCollectionsByMonthAsync(
        long teacherId, int month, int year, int page, int limit);

    /// <summary>
    /// Screen: AssistantWallet. Wallet card + paginated recent collections for one assistant.
    /// Reuses <c>GetAssistantWalletAsync</c> (tenant-scoped → 404 if not this teacher's) and
    /// <c>GetCollectorTransactionsPagedAsync</c>. Tutor-only at the controller.
    /// </summary>
    Task<Result<AssistantWalletScreenResponse>> GetAssistantWalletScreenAsync(
        long teacherId, long assistantId, int page, int limit);
}
