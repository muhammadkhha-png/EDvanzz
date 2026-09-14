using Edvanz.Domain.Models;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories.Queries;

/// <summary>
/// THE single definition of the unified collections ledger's source set: monthly-subscription
/// payments (<c>PaymentTransactions</c>) and "Books &amp; fees" payments
/// (<c>EventPaymentTransactions</c>), concatenated into one orderable, pageable, groupable query.
///
/// <para><b>Why this exists.</b> Books &amp; fees cash has always credited
/// <c>AssistantWallet.CurrentBalance</c> but appeared in NO ledger, because every ledger read went
/// to <c>PaymentTransactions</c> alone. A collector's balance therefore exceeded the sum of the rows
/// their own ledger listed — precisely the defect class <c>HeldSinceAt</c> and the "rows sum exactly
/// to the held balance" design exist to prevent.</para>
///
/// <para><b>Why not write extras as real <c>PaymentTransaction</c> rows.</b> Seven load-bearing
/// aggregates would silently change meaning: <c>GetCashCollectedInRangeAsync</c> feeds
/// <c>CenterRevenueService</c> (real money billed to centers); <c>GetCashCollectedBreakdownAsync</c>
/// and <c>GetCollectionAmountTiersAsync</c> both group <c>PaymentTransactionAllocation</c> rows,
/// which an extras payment has none of; the per-session cards are <c>SessionId</c>-keyed;
/// <c>UpdatePaymentCounterAsync</c> advances paid/unpaid PERIOD counts; and — the money bug —
/// <c>PerOpIdempotentPaymentTransport.reconcile</c> matches a queued FEE op against a student's
/// transactions by same-day-and-amount, so an extras row living among them could make a real fee
/// collection reconcile away as "already landed".</para>
///
/// <para><b>Extend this file — never re-implement the union in a repo method.</b> The day headers,
/// the amount tiers, the page slice and the total must all measure the same set, or a card says one
/// number and opens a list showing another.</para>
/// </summary>
internal static class CollectionLedgerQueries
{
    /// <summary>
    /// The fee branch, filtered exactly like the original <c>BuildTransactionsInRangeQuery</c>.
    /// </summary>
    private static IQueryable<CollectionLedgerSourceRow> FeeRows(
        EdvanzDbContext ctx,
        long teacherId,
        DateTime startInclusive, DateTime endInclusive,
        long? sessionId, long? collectedByUserId,
        string? normalizedTerm)
    {
        var q = ctx.PaymentTransactions
            .Where(t => t.TeacherId == teacherId
                && t.CollectedAt >= startInclusive
                && t.CollectedAt <= endInclusive
                && !t.IsDeleted);

        if (sessionId.HasValue)
            q = q.Where(t => t.SessionId == sessionId.Value);
        if (collectedByUserId.HasValue)
            q = q.Where(t => t.CollectedByUserId == collectedByUserId.Value);

        // Applied BEFORE the concat so the Arabic-normalizing UDF never runs over a union.
        if (!string.IsNullOrEmpty(normalizedTerm))
            q = q.Where(t =>
                (t.StudentName != null && EF.Functions.Like(DbSearch.ArabicNormalize(t.StudentName), $"%{normalizedTerm}%"))
                || (t.StudentCode != null && EF.Functions.Like(DbSearch.ArabicNormalize(t.StudentCode), $"%{normalizedTerm}%")));

        return q.Select(t => new CollectionLedgerSourceRow
        {
            Id = t.Id,
            Kind = LedgerRowKind.Fee,
            TeacherStudentId = t.TeacherStudentId,
            StudentName = t.StudentName,
            StudentCode = t.StudentCode,
            AmountPaid = t.AmountPaid,
            CollectedAt = t.CollectedAt,
            CollectedByUserId = t.CollectedByUserId,
            PaymentEventId = null,
            ExtrasItemName = null,
            Note = t.CollectionNote,
            PaymentMethod = (byte)t.PaymentMethod
        });
    }

    /// <summary>
    /// The extras branch. <c>!IsDeleted</c> comes from the entity's global query filter.
    ///
    /// <para><b>A session-scoped ledger is fees-only, by nature.</b> An extras obligation is
    /// student-scoped and its transaction carries no <c>SessionId</c> (deliberately — a ninth
    /// denormalized session-name column would have to join
    /// <c>ISessionRepo.PropagateSessionNameAsync</c> and its CI gate). So when the caller scopes to
    /// one session, this branch contributes nothing rather than guessing.</para>
    /// </summary>
    private static IQueryable<CollectionLedgerSourceRow> ExtrasRows(
        EdvanzDbContext ctx,
        long teacherId,
        DateTime startInclusive, DateTime endInclusive,
        long? collectedByUserId,
        string? normalizedTerm)
    {
        var q = ctx.EventPaymentTransactions
            .Where(t => t.TeacherId == teacherId
                && t.CollectedAt >= startInclusive
                && t.CollectedAt <= endInclusive);

        if (collectedByUserId.HasValue)
            q = q.Where(t => t.CollectedByUserId == collectedByUserId.Value);

        if (!string.IsNullOrEmpty(normalizedTerm))
            q = q.Where(t =>
                (t.StudentName != null && EF.Functions.Like(DbSearch.ArabicNormalize(t.StudentName), $"%{normalizedTerm}%"))
                || (t.StudentCode != null && EF.Functions.Like(DbSearch.ArabicNormalize(t.StudentCode), $"%{normalizedTerm}%")));

        return q.Select(t => new CollectionLedgerSourceRow
        {
            Id = t.Id,
            Kind = LedgerRowKind.Extras,
            TeacherStudentId = t.TeacherStudentId,
            StudentName = t.StudentName,
            StudentCode = t.StudentCode,
            AmountPaid = t.AmountPaid,
            CollectedAt = t.CollectedAt,
            CollectedByUserId = t.CollectedByUserId,
            PaymentEventId = t.PaymentEventId,
            ExtrasItemName = t.EventName,
            Note = t.CollectionNote,
            PaymentMethod = (byte)t.PaymentMethod
        });
    }

    /// <summary>
    /// The ledger's source set for the requested scope.
    ///
    /// <para><b>CONCAT, NEVER UNION.</b> <c>Union</c> dedupes, and two students paying the same
    /// amount at the same instant are distinct rows that must both count. (<c>VideoAudienceQueries</c>
    /// deliberately uses <c>Union</c> for the opposite reason: a student reachable through two scopes
    /// must count once.) Getting this wrong loses money silently.</para>
    /// </summary>
    internal static IQueryable<CollectionLedgerSourceRow> LedgerRows(
        EdvanzDbContext ctx,
        long teacherId,
        DateTime startInclusive, DateTime endInclusive,
        long? sessionId, long? collectedByUserId,
        string? normalizedTerm,
        LedgerKindFilter kind)
    {
        var fees = FeeRows(ctx, teacherId, startInclusive, endInclusive, sessionId, collectedByUserId, normalizedTerm);

        // A session filter makes the extras branch inapplicable, whatever the caller asked for.
        if (kind == LedgerKindFilter.Fees || sessionId.HasValue)
            return fees;

        var extras = ExtrasRows(ctx, teacherId, startInclusive, endInclusive, collectedByUserId, normalizedTerm);

        return kind == LedgerKindFilter.Extras ? extras : fees.Concat(extras);
    }

    /// <summary>
    /// The ledger's source set in its canonical order: newest collection first.
    ///
    /// <para><b>The <c>Kind</c> tiebreak is applied ONLY when the query is genuinely a union.</b>
    /// Fee id 5 and extras id 5 can share a <c>CollectedAt</c> to the tick, so across the union
    /// <c>Id</c> alone is no longer unique and a slice boundary could repeat one row and drop
    /// another. Within a SINGLE source <c>Id</c> is unique, and ordering by a constant there emits a
    /// literal <c>(SELECT 1) DESC</c> sort key that the fee path — the hot, already-deployed one —
    /// has no reason to carry past the optimizer. So the single-source ordering stays exactly what
    /// it was before this feature existed.</para>
    /// </summary>
    internal static IQueryable<CollectionLedgerSourceRow> OrderedLedgerRows(
        EdvanzDbContext ctx,
        long teacherId,
        DateTime startInclusive, DateTime endInclusive,
        long? sessionId, long? collectedByUserId,
        string? normalizedTerm,
        LedgerKindFilter kind)
    {
        var rows = LedgerRows(ctx, teacherId, startInclusive, endInclusive, sessionId, collectedByUserId, normalizedTerm, kind);

        // Mirrors LedgerRows' own short-circuits: a session filter makes the extras branch
        // inapplicable, so that is a single source too.
        bool isUnion = kind == LedgerKindFilter.All && !sessionId.HasValue;

        return isUnion
            ? rows.OrderByDescending(r => r.CollectedAt)
                  .ThenByDescending(r => r.Kind)
                  .ThenByDescending(r => r.Id)
            : rows.OrderByDescending(r => r.CollectedAt)
                  .ThenByDescending(r => r.Id);
    }

    /// <summary>
    /// Per-local-day money buckets over the whole scope, in ONE statement.
    ///
    /// <para>Each branch projects to <see cref="LedgerDayBucketRow"/> BEFORE the concat — a named
    /// type, because a SQL union needs an identical member shape on every branch — so the
    /// <c>GROUP BY</c> runs once over the combined set rather than twice with a merge in memory.</para>
    /// </summary>
    internal static IQueryable<LedgerDayBucketRow> DayBuckets(
        EdvanzDbContext ctx,
        long teacherId,
        DateTime startInclusive, DateTime endInclusive,
        long? sessionId, long? collectedByUserId,
        string? normalizedTerm,
        LedgerKindFilter kind,
        int localOffsetHours)
    {
        return LedgerRows(ctx, teacherId, startInclusive, endInclusive, sessionId, collectedByUserId, normalizedTerm, kind)
            .Select(r => new LedgerDayBucketRow
            {
                Day = r.CollectedAt.AddHours(localOffsetHours).Date,
                Amount = r.AmountPaid
            });
    }

    /// <summary>
    /// The amounts feeding the "how many paid X" tier cards.
    ///
    /// <para>The fee side groups per-period settlement SLICES
    /// (<c>PaymentTransactionAllocation.AmountApplied</c>), so a 600 payment clearing two months
    /// counts as two 300s. The extras side has no allocations — one item is one slice — so it
    /// contributes its own amount. That semantic difference is exactly why the caller hides the tier
    /// strip when the scope is "all": merged, a "300 → 14" bucket would mean two different things.</para>
    /// </summary>
    internal static IQueryable<LedgerAmountRow> TierAmounts(
        EdvanzDbContext ctx,
        long teacherId,
        DateTime startInclusive, DateTime endInclusive,
        long? collectedByUserId,
        string? normalizedTerm,
        LedgerKindFilter kind)
    {
        var feeSlices = ctx.PaymentTransactionAllocations
            .Where(a => a.TeacherId == teacherId
                && !a.PaymentTransaction.IsDeleted
                && a.PaymentTransaction.CollectedAt >= startInclusive
                && a.PaymentTransaction.CollectedAt <= endInclusive
                && a.AmountApplied > 0m);

        if (collectedByUserId.HasValue)
            feeSlices = feeSlices.Where(a => a.PaymentTransaction.CollectedByUserId == collectedByUserId.Value);

        if (!string.IsNullOrEmpty(normalizedTerm))
            feeSlices = feeSlices.Where(a =>
                (a.PaymentTransaction.StudentName != null
                    && EF.Functions.Like(DbSearch.ArabicNormalize(a.PaymentTransaction.StudentName), $"%{normalizedTerm}%"))
                || (a.PaymentTransaction.StudentCode != null
                    && EF.Functions.Like(DbSearch.ArabicNormalize(a.PaymentTransaction.StudentCode), $"%{normalizedTerm}%")));

        var fees = feeSlices.Select(a => new LedgerAmountRow { Amount = a.AmountApplied });

        if (kind == LedgerKindFilter.Fees)
            return fees;

        var extras = ExtrasRows(ctx, teacherId, startInclusive, endInclusive, collectedByUserId, normalizedTerm)
            .Where(r => r.AmountPaid > 0m)
            .Select(r => new LedgerAmountRow { Amount = r.AmountPaid });

        return kind == LedgerKindFilter.Extras ? extras : fees.Concat(extras);
    }
}
