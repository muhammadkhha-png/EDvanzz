using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// One admin extension of a subscription: days added to a period that was already running.
///
/// WHY IT EXISTS: the admin panel offers "Extend by days" directly beside "Activate subscription",
/// and the two are used interchangeably to keep a paying teacher going. But they leave completely
/// different traces. Activating INSERTS a subscription row, so it is visible as a renewal, carries
/// its own dates, and can be priced. Extending MUTATES the row that is already there — it moved
/// <c>EndDate</c> and overwrote <c>CreatedByUserId</c>, and that was the entire record.
///
/// The consequences were all silent. A teacher renewed by extension looked, to every report, like
/// someone whose subscription simply never ended: the renewal figures under-counted, the money was
/// nowhere, and the subscription history the teacher sees showed one long period rather than the
/// months they actually paid for. Nothing recorded who granted the days, when, or why — so a
/// three-day goodwill extension and a paid month were indistinguishable afterwards.
///
/// This row is that record. It never affects the gate — the subscription's own dates still decide
/// access — it exists so an extension can be counted, priced and explained after the fact.
/// </summary>
public class SubscriptionExtension : BaseEntity
{
    /// <summary>The teacher whose subscription was extended. Denormalised so the renewal queries
    /// never have to join through the subscription row to scope by tenant.</summary>
    public long TeacherId { get; set; }

    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// The subscription row that was moved. No FK <c>OnDelete</c> cascade — an extension is audit,
    /// and audit outlives the thing it describes (CLAUDE.md §4.2).
    /// </summary>
    public long TeacherSubscriptionId { get; set; }

    public TeacherSubscription TeacherSubscription { get; set; } = null!;

    /// <summary>Days added. Always positive — the extend endpoint rejects anything else.</summary>
    public int DaysAdded { get; set; }

    /// <summary>Where the period ended before this extension.</summary>
    public DateTime PreviousEndDate { get; set; }

    /// <summary>Where it ends after. The pair is kept rather than recomputed, so a later
    /// extension or an end-date override cannot rewrite what this one did.</summary>
    public DateTime NewEndDate { get; set; }

    /// <summary>
    /// What the teacher paid for these days, when the admin says so. NULL means unstated — which is
    /// the honest record for a goodwill extension, and is NOT the same as zero. Money figures count
    /// only the stated ones.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? AmountPaidEGP { get; set; }

    /// <summary>Why, in the admin's own words. The difference between "paid for September" and
    /// "compensation for the outage" is invisible in the dates alone.</summary>
    [MaxLength(500)]
    public string? Note { get; set; }

    /// <summary>The admin who granted the days. Plain column, no FK — matches how the rest of the
    /// admin audit records an actor.</summary>
    public long ExtendedByUserId { get; set; }
}
