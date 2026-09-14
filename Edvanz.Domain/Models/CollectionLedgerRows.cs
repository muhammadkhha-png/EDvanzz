using System.Text.Json.Serialization;

namespace Edvanz.Domain.Models;

/// <summary>Which table a collections-ledger row came from.</summary>
public enum LedgerRowKind : byte
{
    /// <summary>A monthly-subscription payment (<c>PaymentTransactions</c>).</summary>
    Fee = 1,

    /// <summary>A "Books &amp; fees" payment (<c>EventPaymentTransactions</c>).</summary>
    Extras = 2
}

/// <summary>
/// The collections-ledger scope filter — what the tutor's segmented control selects.
///
/// <para><b><see cref="Fees"/> IS 0 AND IS THE DEFAULT, DELIBERATELY.</b> Every deployed app build
/// omits this parameter. Defaulting to <see cref="All"/> would grow their ledger rows they cannot
/// label, with an <c>id</c> format they do not expect (extras ids are prefixed <c>"extras-"</c>),
/// while <c>dailyNets</c> and <c>amountTiers</c> silently changed under them. With <see cref="Fees"/>
/// as the default, an old build is byte-identical; the new build — which ships the segmented control
/// in the same release — sends <c>kind=all</c> explicitly. Do NOT change this default.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LedgerKindFilter : byte
{
    /// <summary>Monthly subscriptions only. The wire default.</summary>
    Fees = 0,

    /// <summary>"Books &amp; fees" only.</summary>
    Extras = 1,

    /// <summary>Both, interleaved by collection instant.</summary>
    All = 2
}

/// <summary>
/// One day bucket of collected cash. A NAMED type because a SQL union needs an identical member
/// shape on every branch — anonymous types cannot be concatenated in SQL (the
/// <c>VideoAudiencePair</c> precedent in <c>Queries/VideoAudienceQueries.cs</c>).
/// </summary>
public sealed class LedgerDayBucketRow
{
    public DateTime Day { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>One collected amount, for the amount-tier histogram. Named for the same union reason.</summary>
public sealed class LedgerAmountRow
{
    public decimal Amount { get; set; }
}

/// <summary>
/// The flat projection both ledger branches produce so they can be concatenated, ordered and paged
/// in ONE SQL statement.
///
/// <para><b>Concatenate with <c>Concat</c>, NEVER <c>Union</c>.</b> <c>Union</c> dedupes, and two
/// students paying the same amount on the same day are distinct rows that must both count.
/// (<c>VideoAudienceQueries</c> deliberately uses <c>Union</c> for the opposite reason — there a
/// student reachable through two scopes must count once.)</para>
///
/// <para>It carries everything an EXTRAS row needs to render completely. Fee rows are then hydrated
/// by a follow-up primary-key seek on the page's ids, because <c>Include</c> cannot be applied to a
/// concatenated query — which is also what lets the shipped fee-row payload stay byte-identical.</para>
/// </summary>
public sealed class CollectionLedgerSourceRow
{
    public long Id { get; set; }

    /// <summary>
    /// Also a required ORDER BY tiebreaker: fee id 5 and extras id 5 can share a <c>CollectedAt</c>
    /// tick, and without this SQL Server may order them differently between two page reads —
    /// repeating one row and dropping another across a slice boundary.
    /// </summary>
    public LedgerRowKind Kind { get; set; }

    public long? TeacherStudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }

    public decimal AmountPaid { get; set; }
    public DateTime CollectedAt { get; set; }
    public long? CollectedByUserId { get; set; }

    /// <summary>Extras only — the item this payment was for.</summary>
    public long? PaymentEventId { get; set; }

    /// <summary>Extras only — the item's name, denormalized on the transaction.</summary>
    public string? ExtrasItemName { get; set; }

    public string? Note { get; set; }
    public byte PaymentMethod { get; set; }
}
