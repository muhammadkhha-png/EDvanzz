using System.Text.Json.Serialization;
using Edvanz.Application.Json;
using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Payment;

// ══════════════════════════════════════════════════════════════════════════
// PAYMENT COLLECTION DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for collecting a payment from a student.
/// REQ-PAY-001: Supports all four collection methods.
/// REQ-PAY-012: Captures all required metadata.
/// </summary>
public class CollectPaymentDto
{
    public long TeacherId { get; set; }
    public long TeacherStudentId { get; set; }
    public long SessionId { get; set; }
    public decimal Amount { get; set; }
    public PaymentCollectionMethod PaymentMethod { get; set; }
    /// <summary>
    /// The user collecting this payment (auto-set from auth context if null).
    /// REQ-PAY-011: Automatically associated with logged-in user.
    /// </summary>
    public long? CollectedByUserId { get; set; }
    /// <summary>
    /// Whether the user has confirmed a same-day duplicate warning.
    /// REQ-PAY-020: Second payment on same day requires confirmation.
    /// </summary>
    public bool DuplicateConfirmed { get; set; } = false;
    /// <summary>
    /// Whether the user has confirmed payment for an already-paid period.
    /// REQ-PAY-026: Warning before allowing payment for a fully-paid period.
    /// PAY-9 / INERT BY DESIGN: the monthly engine already accepts up to one month in advance, and
    /// paying beyond that is a hard 422 (<c>PaymentAmountExceedsAdvanceLimit</c>) — so once a student
    /// is "already paid" there is genuinely nothing left to collect and no override is possible. The
    /// flag is kept for wire-compat and the concurrency-retry path; the already-paid message is
    /// worded as a terminal statement (not a "proceed?" prompt) to match.
    /// </summary>
    public bool AlreadyPaidConfirmed { get; set; } = false;
    /// <summary>
    /// Online payment reference (for online payment methods only).
    /// REQ-PAY-008: Transaction reference.
    /// </summary>
    public string? OnlineTransactionRef { get; set; }
    /// <summary>
    /// Whether this is an offline-collected record being synced.
    /// REQ-PAY-079: Offline records with full metadata.
    /// </summary>
    public bool IsOfflineRecord { get; set; } = false;
    public string? OfflineDeviceId { get; set; }

    /// <summary>
    /// When the cash was actually taken on the device, as a UTC INSTANT — the app has always
    /// sent <c>now.toUtc()</c> here (and the same for the <c>clientCreatedAt</c> fallback).
    /// It is NOT the teacher's wall-clock: converting it is what
    /// <c>PaymentTransaction.LocalCollectedAt</c> needs, and storing it raw put a UTC time on
    /// the receipt.
    /// </summary>
    public DateTime? OfflineCollectedAt { get; set; }

    /// <summary>
    /// Feature C: optional free-text note captured with the collection. Stored on the transaction and
    /// shown in history. Required by <c>/api/v1/collect/submit</c> when the amount is partial/custom
    /// (that path sets it); ignored/optional on other collection paths.
    /// </summary>
    public string? CollectionNote { get; set; }

    /// <summary>
    /// Client-generated unique id for offline records (uuid). Replaying a
    /// record whose ClientEntryId already exists for this teacher returns
    /// success without recording again (exactly-once sync). Ignored for
    /// online collections.
    /// </summary>
    public string? ClientEntryId { get; set; }

    /// <summary>
    /// REQ-PAY-080. A durable exactly-once key the SERVER derived for an ONLINE collection, so the
    /// filtered unique index <c>IX_PT_TeacherId_ClientEntryId</c> - not a cache - decides whether
    /// this cash has already been taken.
    /// <para>
    /// <b>Not on the wire, and deliberately so.</b> It is <c>internal</c>, which means
    /// System.Text.Json neither binds it from a request body nor emits it, and Swagger never shows
    /// it. Honouring the PUBLIC <see cref="ClientEntryId"/> on online collections instead would
    /// have changed <c>POST api/Payment/collect</c> for every deployed client in one step: a client
    /// that re-sent a key on an online retry would go from "silently ignored" to a unique-index
    /// violation with no handler above it. Only <c>POST /api/v1/collect/submit</c> sets this, and
    /// only when the caller supplied an <c>Idempotency-Key</c> header.
    /// </para>
    /// <para>
    /// Null (the default, and what every existing caller produces) means the transaction stores a
    /// NULL <c>ClientEntryId</c> exactly as it does today - the filtered index ignores NULLs, so
    /// nothing about those collections changes.
    /// </para>
    /// </summary>
    internal string? ServerDerivedReplayKey { get; set; }
}

/// <summary>
/// Result DTO returned after payment collection attempt.
/// Contains warnings, pro-rate info, and the created transaction.
/// </summary>
public class CollectPaymentResultDto
{
    public PaymentTransactionDto? Transaction { get; set; }
    /// <summary>
    /// Whether a same-day duplicate was detected.
    /// REQ-PAY-020: Requires explicit confirmation.
    /// </summary>
    public bool IsSameDayDuplicate { get; set; } = false;
    public decimal? TodayPaidAmount { get; set; }
    public string? TodayPaidSessionName { get; set; }

    // ── Same-day soft-confirm attribution (Issue 1, 2026-09-02) ──
    // A second user collecting from the same student the same day is WARNED, not blocked. These fields
    // let the client render "Mohamed (assistant) collected 200 EGP for September today · Collect anyway".

    /// <summary>Display name of the user who recorded the (most recent) same-day payment. Null when unresolved.</summary>
    public string? TodayPaidByName { get; set; }

    /// <summary>The installment month/period label the same-day payment settled (e.g. "September 2026"). Null when unknown.</summary>
    public string? TodayPaidMonthLabel { get; set; }
    /// <summary>
    /// Whether the period was already fully paid.
    /// REQ-PAY-026: Warning to collector.
    /// </summary>
    public bool IsAlreadyPaid { get; set; } = false;

    /// <summary>
    /// REQ-PAY-079. True when this was an OFFLINE record whose device-reported collection instant
    /// was refused (in the future of the server's clock, or more than a week old) and the server's
    /// own clock dated the collection instead. The cash is recorded either way; what changed is the
    /// day - and therefore the month's totals - it counts in. Always false for online collections.
    /// Additive; older clients ignore it.
    /// </summary>
    public bool CollectedAtAdjusted { get; set; } = false;
    /// <summary>
    /// Whether a pro-rated amount was applied.
    /// REQ-PAY-025: Pro-rate indicator.
    /// </summary>
    public bool IsProRated { get; set; } = false;
    public string? ProRatedTierLabel { get; set; }
    public decimal? OriginalAmount { get; set; }
    public decimal? ProRatedAmount { get; set; }

    /// <summary>The months this cash cleared or part-cleared, as "YYYY-MM", oldest first — the same
    /// settlement slices recorded in <c>PaymentTransactionAllocation</c>. Captured here at the point
    /// the cascade runs so callers never need a follow-up query to learn WHERE the money landed;
    /// the offline-sync echo uses it to tell a teacher which month an over-payment rolled into.</summary>
    public List<string> SettledMonths { get; set; } = new();

    /// <summary>
    /// The same cascade as <see cref="SettledMonths"/>, but WITH the amount that landed on each month
    /// — oldest first. Added 2026-09-15 so a collector can be told where their cash actually went.
    ///
    /// <para>The month list alone was not enough. The engine fills the oldest unpaid month first and
    /// cascades forward (§7.4), so 300 handed over "for September" can settle August's 190 and put
    /// 110 on September — and every screen reported only that the collection succeeded. The tutor
    /// then sees 190 still owed for a month they believe was paid in full and calls it impossible.
    /// Reported at the moment the cascade runs; no follow-up query.</para>
    /// </summary>
    public List<PaymentSettlementSliceDto> Settlements { get; set; } = new();
}

/// <summary>
/// One month a collection landed on: which month, how much of the cash it took, and whether that
/// closed the month or only reduced it. Oldest first, mirroring the collection cascade.
/// </summary>
public class PaymentSettlementSliceDto
{
    /// <summary>The month, as "yyyy-MM" — the stable key; clients format their own label.</summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>Invariant English label ("September 2026") for clients with no month formatter.</summary>
    public string MonthLabel { get; set; } = string.Empty;

    /// <summary>How much of this collection landed on this month.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// True when this slice settled the month in FULL. False means the month is still short —
    /// the distinction the collector has to relay to the parent.
    /// </summary>
    public bool ClearedMonth { get; set; }
}

/// <summary>
/// The system-SUGGESTED joining-month (first-month) proration for a student, per the teacher's chosen
/// <see cref="Edvanz.Domain.Enums.ProrationMethod"/> (REQ-PAY-021/022 rev 2, 2026-09-03). Anchored to
/// the student's ENROLLMENT (earliest assignment) date — attendance never moves the bill: an absence is
/// a missed obligation, not a discount. Deterministic — recomputed from the join date + the CURRENT
/// settings on every call (never echoed from the stored period, so a manual override can always be
/// reset to the true system price). Pure output — computed on demand, never persisted.
/// Consumed by the collect-lookup enrichment and the per-student proration endpoint.
/// </summary>
public class ProrationSuggestionResult
{
    /// <summary>True when the student has a still-owed proration anchor month (a NEW enrollment's first
    /// month, not yet paid). False = nothing to prorate (proration off, transfer, already paid, or no anchor).</summary>
    public bool Applicable { get; set; }

    public Edvanz.Domain.Enums.ProrationMethod Method { get; set; }

    /// <summary>The anchor <see cref="Edvanz.Domain.Entities.PaymentPeriod"/> id, when applicable.</summary>
    public long? AnchorPeriodId { get; set; }

    /// <summary>First day of the anchor (joining) month.</summary>
    public DateTime AnchorMonthStart { get; set; }

    /// <summary>The full month base = custom per-student amount ?? session amount.</summary>
    public decimal FullBase { get; set; }

    /// <summary>Suggested joining amount — already rounded to the nearest 5 and clamped to [0, FullBase].</summary>
    public decimal SuggestedAmount { get; set; }

    /// <summary>SuggestedAmount ÷ FullBase (display only).</summary>
    public decimal Fraction { get; set; }

    /// <summary>The student's enrollment (earliest assignment) date, teacher-local, clamped into the
    /// anchor month — the single basis of the suggestion. Null only when no assignment row exists.</summary>
    public DateTime? JoinDate { get; set; }

    /// <summary>LEGACY (rev 1 attendance anchor). No longer populated — kept so older clients that bound
    /// the field simply see null and hide their "first class" line.</summary>
    public DateTime? FirstClassDate { get; set; }

    /// <summary>ByClasses: total scheduled classes in the anchor month. Null for other methods / no session.</summary>
    public int? ClassesTotalThisMonth { get; set; }

    /// <summary>ByClasses: scheduled classes from the JOIN date through month-end (the billed numerator).</summary>
    public int? ClassesBilledThisMonth { get; set; }

    /// <summary>LEGACY (rev 1 "attended N so far"). No longer populated — attendance does not price the
    /// joining month; kept null for wire-compat.</summary>
    public int? ClassesAttendedThisMonth { get; set; }

    /// <summary>The anchor's stored AmountDue right now (may already be a manual override).</summary>
    public decimal CurrentAmountDue { get; set; }

    /// <summary>True when the anchor already carries a human-set (sticky) joining amount.</summary>
    public bool IsManualOverride { get; set; }

    /// <summary>When manually overridden: who set the amount (display name, from the proration audit
    /// log). Null when not manual or the audit row predates attribution.</summary>
    public string? SetByName { get; set; }

    /// <summary>When manually overridden: when the amount was set (UTC). Null when not manual.</summary>
    public DateTime? SetAt { get; set; }

    /// <summary>Plain, buildable reason string, e.g. "7 of 13 classes from 14 Sep". Null when not prorated.</summary>
    public string? Reason { get; set; }
}

/// <summary>
/// What the retroactive proration reconcile actually did after a settings save (REQ-PAY-021/022 rev 2 —
/// the recalculation must be VISIBLE, not silent): how many still-owed first months were re-priced, and
/// how many were deliberately kept (sticky manual override / cash already collected on the month).
/// Attached to the configuration-save response; null when the proration config did not change.
/// </summary>
public class ProrationReconcileSummary
{
    /// <summary>First months whose amount actually changed under the new settings.</summary>
    public int Repriced { get; set; }

    /// <summary>First months skipped because a person set the amount by hand (sticky override).</summary>
    public int KeptManual { get; set; }

    /// <summary>First months skipped because cash was already collected against them.</summary>
    public int KeptPaid { get; set; }

    /// <summary>Eligible first months whose amount already matched the new settings (no-op).</summary>
    public int Unchanged { get; set; }
}

/// <summary>
/// What a PER-STUDENT price change (custom amount set or cleared) did to that student's still-owed
/// bills. Same purpose as <see cref="SessionRepriceSummary"/>: the app reports the outcome instead of
/// leaving the teacher to wonder why one month did not follow the new price. There is no KeptPaid here
/// — the re-price predicate already excludes settled months.
/// </summary>
public class CustomAmountRepriceSummary
{
    /// <summary>Still-owed bills rewritten to the new per-student amount.</summary>
    public int Repriced { get; set; }

    /// <summary>
    /// Joining months skipped because a person set the amount by hand (sticky override). Surfaced so a
    /// frozen bill announces itself at the moment it diverges from the student's new price.
    /// </summary>
    public int KeptManual { get; set; }

    /// <summary>
    /// Bills skipped because money was already settled against them - cash collected or an amount
    /// forgiven. That money is ground truth: re-pricing such a month below what was settled would close
    /// it as Paid and swallow the surplus. Added 2026-09-09 alongside the guard itself; the SESSION
    /// price change has reported this since 24d928f and the per-student change now matches it.
    /// Additive - the shipped 4.0.0+17 client returns Future&lt;void&gt; from this call and never reads
    /// the payload, so populating it cannot affect an existing build.
    /// </summary>
    public int KeptPaid { get; set; }
}

/// <summary>
/// What a SESSION PRICE change did to the session's still-owed bills: how many were re-priced to the
/// new amount and how many were deliberately kept. Monthly bills re-price over EVERY still-owed month
/// — arrears included — matching what a per-student price change already does; per-class bills only
/// from next month, since a class already delivered was delivered at the old price. The change must be
/// VISIBLE rather than silent: the app reports it instead of leaving the teacher to wonder why a figure
/// moved (or did not). Attached to the session-update response; null on every other session response.
/// </summary>
public class SessionRepriceSummary
{
    /// <summary>Still-owed bills rewritten to the new session amount.</summary>
    public int Repriced { get; set; }

    /// <summary>
    /// Past/current bills skipped because money was already settled against them — cash collected or
    /// an amount forgiven. That money is ground truth and re-pricing below it would silently swallow
    /// the surplus.
    /// </summary>
    public int KeptPaid { get; set; }

    /// <summary>Joining months skipped because a person set the amount by hand (sticky override).</summary>
    public int KeptManual { get; set; }

    /// <summary>Distinct students whose bills changed.</summary>
    public int StudentsAffected { get; set; }

    /// <summary>
    /// Earliest month actually re-priced — what the app names in "N bills updated from {month}".
    /// Null when nothing was re-priced. A calendar day (always first-of-month for a monthly bill), so
    /// it is a <c>DateOnly</c> on the wire (<c>"2026-09-01"</c>) and never an instant a client could
    /// shift (§11b).
    /// </summary>
    public DateOnly? EarliestMonth { get; set; }
}

/// <summary>
/// What the billing-start reconcile did (or WOULD do, on a dry run) when a teacher's
/// <c>BillingStartDate</c> is set or changed: obligations dated before the billing floor are removed
/// (never-paid, no-cash, non-manual rows only), months the floor newly allows are backfilled, and each
/// affected student's first-month anchor + counter are recomputed. Attached to the configuration-save
/// response (sibling of <see cref="ProrationReconcileSummary"/>) and to the admin billing-start
/// endpoint's result. Null on plain reads and on saves where the billing start did not change.
/// </summary>
public class BillingStartReconcileSummary
{
    /// <summary>Pre-billing-start periods deleted (Monthly months + PerSession class dates).</summary>
    public int RemovedPeriods { get; set; }

    /// <summary>Missing months/class dates (re)generated because the floor moved earlier.</summary>
    public int BackfilledPeriods { get; set; }

    /// <summary>Pre-billing-start rows kept because cash was already collected against them.</summary>
    public int KeptPaid { get; set; }

    /// <summary>Pre-billing-start rows kept because a person set the amount by hand (sticky).</summary>
    public int KeptManual { get; set; }

    /// <summary>Distinct students whose obligations changed (removed, backfilled or re-anchored).</summary>
    public int StudentsAffected { get; set; }
}

/// <summary>
/// Result of the SUPER-ADMIN billing-start set (<c>POST /api/admin/payments/billing-start</c>):
/// the applied (or previewed) floor plus the reconcile summary. On a dry run nothing is written —
/// the config keeps its stored value and the summary reports what WOULD happen.
/// </summary>
public class BillingStartAdminResult
{
    public long TeacherId { get; set; }

    /// <summary>The normalized (first-of-month) billing start the call applied / previewed.</summary>
    public DateTime BillingStartDate { get; set; }

    /// <summary>The value stored BEFORE this call (null = was not set).</summary>
    public DateTime? PreviousBillingStartDate { get; set; }

    public bool DryRun { get; set; }

    public BillingStartReconcileSummary Reconcile { get; set; } = new();
}

// ══════════════════════════════════════════════════════════════════════════
// PAYMENT STATUS DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Student payment status for the collection screen.
/// REQ-PAY-NFR-006: Displayed prominently after student identification.
/// </summary>
public class PaymentStatusDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string? SessionName { get; set; }
    public PaymentStatus CurrentStatus { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Outstanding { get; set; }
    public string? PeriodLabel { get; set; }
    public int ConsecutiveUnpaid { get; set; }
    public bool IsProRated { get; set; }
    public string? ProRatedTierLabel { get; set; }
    public bool HasCustomAmount { get; set; }
    public decimal? CustomAmount { get; set; }
}

/// <summary>
/// Result of a duplicate check for a student.
/// REQ-PAY-020/026: Same-day and already-paid detection.
/// </summary>
public class DuplicateCheckResultDto
{
    public bool HasSameDayPayment { get; set; }
    public decimal? TodayPaidAmount { get; set; }
    public string? TodayPaidPeriodLabel { get; set; }
    public bool IsCurrentPeriodPaid { get; set; }
    public string? CurrentPeriodLabel { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// PAYMENT TRANSACTION DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Display DTO for a single payment transaction.
/// REQ-PAY-012: Shows all required metadata.
/// </summary>
public class PaymentTransactionDto
{
    public long Id { get; set; }
    public long? TeacherStudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }
    public string SessionName { get; set; } = null!;
    public long? SessionId { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public PaymentCollectionMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentTransactionStatus { get; set; }
    public long? CollectedByUserId { get; set; }
    public string? CollectedByUserName { get; set; }
    public DateTime CollectedAt { get; set; }

    /// <summary>
    /// The teacher's LOCAL wall-clock at collection (Kind Unspecified), so a receipt shows the
    /// time the tutor actually took the cash. Its sibling <see cref="CollectedAt"/> is the UTC
    /// instant. Opted out of the global UTC converter on purpose — stamping a <c>Z</c> here
    /// would make every client shift the receipt by the Egypt offset.
    /// </summary>
    [JsonConverter(typeof(LocalWallClockDateTimeJsonConverter))]
    public DateTime LocalCollectedAt { get; set; }
    public bool IsPartial { get; set; }
    public bool IsProRated { get; set; }
    public string? ProRatedTierLabel { get; set; }
    public bool IsOnlinePayment { get; set; }
    public string? OnlineTransactionRef { get; set; }
    public string? PeriodLabel { get; set; }
    public bool IsEdited { get; set; }

    /// <summary>Feature C: the note captured at collection time (null when none). Shown in history.</summary>
    public string? Note { get; set; }
}

/// <summary>
/// Student payment history with period grouping.
/// REQ-PAY-052: Single Student Payment History.
/// </summary>
public class PaymentHistoryDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public decimal TotalAmountPaid { get; set; }
    public decimal TotalOutstanding { get; set; }
    public List<PaymentPeriodDto> Periods { get; set; } = new();
    public List<SessionTransferEventDto> Transfers { get; set; } = new();
    public List<StudentDepartureDto> Departures { get; set; } = new();

    /// <summary>
    /// Balance forgiveness events on this student's timeline (any status), newest first — so the app
    /// can render a "Forgiven" line alongside collections/transfers/departures. Each entry carries a
    /// constant <c>type = "Forgiven"</c>.
    /// </summary>
    public List<ForgivenessHistoryEntryDto> Forgivenesses { get; set; } = new();
}

/// <summary>One "Forgiven" entry on the student payment history timeline.</summary>
public class ForgivenessHistoryEntryDto
{
    public long Id { get; set; }
    /// <summary>Constant discriminator for the client — always "Forgiven".</summary>
    public string Type { get; set; } = "Forgiven";
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    /// <summary>Display name of the tutor who forgave.</summary>
    public string? ByName { get; set; }
    public DateTime Date { get; set; }
    /// <summary>active | reversed</summary>
    public string Status { get; set; } = "active";
}

/// <summary>
/// What a pending student move would do to the MONEY, per student, computed without writing anything.
/// Feeds the transfer confirmation so a teacher is told that arrears re-price and future months are
/// cancelled BEFORE they tap, instead of discovering it on a parent's next bill.
/// </summary>
public class MoveBillingPreviewDto
{
    public List<MoveBillingPreviewStudentDto> Students { get; set; } = new();

    /// <summary>Months that follow the students into the destination, across the whole batch.</summary>
    public int CarriedMonths { get; set; }

    /// <summary>What those months will be worth at the destination's price.</summary>
    public decimal CarriedAmount { get; set; }

    /// <summary>Future months voided in the source (the destination re-creates its own).</summary>
    public int CancelledMonths { get; set; }

    /// <summary>What those voided months were worth in the source.</summary>
    public decimal CancelledAmount { get; set; }

    /// <summary>Students the move will refuse (see <see cref="MoveBillingPreviewStudentDto.BlockedReason"/>).</summary>
    public int BlockedCount { get; set; }
}

/// <inheritdoc cref="MoveBillingPreviewDto"/>
public class MoveBillingPreviewStudentDto
{
    public long StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string? StudentCode { get; set; }

    /// <summary>The class they are leaving; null when they have no current class (a plain assign).</summary>
    public string? FromSessionName { get; set; }

    /// <summary>True when this student cannot be moved at all — the batch would fail on them.</summary>
    public bool IsBlocked { get; set; }

    /// <summary>Localized reason, ready to render. Null unless <see cref="IsBlocked"/>.</summary>
    public string? BlockedReason { get; set; }

    public int CarriedMonths { get; set; }
    public decimal CarriedAmount { get; set; }
    public int CancelledMonths { get; set; }
    public decimal CancelledAmount { get; set; }
}

/// <summary>
/// Display DTO for a payment period.
/// </summary>
public class PaymentPeriodDto
{
    public long Id { get; set; }
    public string SessionName { get; set; } = null!;
    public PeriodType PeriodType { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public bool IsProRated { get; set; }
    public decimal ProRatedFraction { get; set; }
    public int PeriodSequence { get; set; }
    public bool IsCarriedForward { get; set; }
    public string? OriginSessionName { get; set; }

    /// <summary>
    /// When this obligation was carried over from another session on a student reassignment
    /// (A → B), the id of the session it was moved FROM. Null for a normal (non-moved) period.
    /// </summary>
    public long? MovedFromSessionId { get; set; }

    /// <summary>
    /// Display snapshot of the session this obligation was moved FROM. Null for a normal period.
    /// The frontend can build a "moved from &lt;name&gt;" label from this + <see cref="IsCarriedForward"/>.
    /// </summary>
    public string? MovedFromSessionName { get; set; }

    public List<PaymentTransactionDto> Transactions { get; set; } = new();
}

// ══════════════════════════════════════════════════════════════════════════
// EDIT DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for editing a payment transaction.
/// BR-PAY-002: Only tutor may edit.
/// </summary>
public class EditPaymentDto
{
    public long TeacherId { get; set; }
    public long TransactionId { get; set; }
    public decimal? NewAmount { get; set; }
    public PaymentStatus? NewStatus { get; set; }
    public long? NewPaymentPeriodId { get; set; }
    public string? EditReason { get; set; }

    /// <summary>
    /// Feature B: audit note shown per edit (the wire field is <c>note</c>). Stored on the
    /// <c>PaymentEditLog</c> (reusing the <c>EditReason</c> column) and returned in edit history.
    /// When <see cref="EnforceNoteOnPartial"/> is set and the edit is to a partial/custom amount, this
    /// (or the legacy <see cref="EditReason"/>) is REQUIRED. Null falls back to <see cref="EditReason"/>.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Feature B gate — set ONLY by the single <c>PUT /api/Payment/transactions/{id}</c> path. When
    /// true, an amount edit that is not a whole-month multiple of the student's monthly rate requires a
    /// note (else 400 <c>EditNoteRequired</c>). Batch edit/revert leave this false, preserving their
    /// existing behaviour. Server-set — never bound from the request body.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool EnforceNoteOnPartial { get; set; } = false;

    public long EditedByUserId { get; set; }
}

/// <summary>
/// Display DTO for a payment edit log entry.
/// </summary>
public class PaymentEditLogDto
{
    public long Id { get; set; }
    public PaymentEditAction EditAction { get; set; }
    public decimal PreviousAmount { get; set; }
    public decimal NewAmount { get; set; }
    public PaymentStatus PreviousStatus { get; set; }
    public PaymentStatus NewStatus { get; set; }
    public DateTime EditedAt { get; set; }
    public long? EditedByUserId { get; set; }
    public string? EditReason { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// CUSTOM AMOUNT DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for setting a custom payment amount for a student.
/// REQ-PAY-016: Custom amount overrides session default.
/// </summary>
public class SetCustomAmountDto
{
    public long TeacherId { get; set; }
    public long TeacherStudentId { get; set; }
    public decimal? CustomAmount { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// UNPAID OVERVIEW DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Filter criteria for the Unpaid Students View.
/// REQ-PAY-032: Filterable by session, group, payment type, consecutive count.
/// </summary>
public class UnpaidStudentsFilterDto
{
    public long? SessionId { get; set; }
    public long? SessionGroupId { get; set; }
    public PaymentType? PaymentType { get; set; }
    public int? MinConsecutiveUnpaid { get; set; }
    public string? Search { get; set; }

    /// <summary>
    /// Optional arrears cutoff as <c>"YYYY-MM"</c>. Outstanding amounts and unpaid-period counts
    /// are computed only through the END of this month (CLAUDE.md §7.4), so pre-generated future
    /// periods are never reported as owed. Omitted → the teacher's current local (Africa/Cairo)
    /// month. A malformed value returns 422.
    /// </summary>
    public string? AsOfMonth { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Display DTO for an unpaid student row.
/// REQ-PAY-031: Displays student details, unpaid periods, outstanding amount.
/// </summary>
public class UnpaidStudentDto
{
    public long TeacherStudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string? SessionName { get; set; }
    public long? SessionId { get; set; }
    public int ConsecutiveUnpaid { get; set; }
    public int TotalUnpaidPeriods { get; set; }
    public decimal TotalOutstanding { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public List<string> UnpaidPeriodLabels { get; set; } = new();
}

/// <summary>
/// Counts for the Paid / Pro-rated / Unpaid students overview card.
/// A student is Paid when they have no outstanding period; among students
/// with an outstanding period, Pro-rated breaks out those whose earliest
/// outstanding period is a prorated first period (REQ-PAY-021/022) from the
/// remaining regular Unpaid students.
/// </summary>
public class StudentPaymentStatusCountsDto
{
    public int PaidCount { get; set; }
    public int ProRatedCount { get; set; }
    public int UnpaidCount { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// COLLECTOR VIEW DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Display DTO for the collector summary view.
/// REQ-PAY-013: Per-user collection breakdown.
/// </summary>
public class CollectorSummaryDto
{
    public long UserId { get; set; }
    public string UserName { get; set; } = null!;
    public string UserRole { get; set; } = null!;
    public decimal TotalCollected { get; set; }
    public int TransactionCount { get; set; }
    public decimal? CurrentWalletBalance { get; set; }

    /// <summary>
    /// This collector's "Books &amp; fees" (مذكرات ومصاريف) cash in the range. Additive:
    /// <see cref="TotalCollected"/> keeps its fee-only meaning so no deployed figure changes.
    ///
    /// <para>It matters here because <see cref="CurrentWalletBalance"/> on this very row has ALWAYS
    /// included extras cash — without the split the row could not explain its own balance.</para>
    /// </summary>
    public decimal TotalCollectedExtras { get; set; }

    /// <summary>Number of "Books &amp; fees" collections this collector took in the range.</summary>
    public int ExtrasTransactionCount { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// WALLET DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Display DTO for an assistant's wallet.
/// REQ-PAY-035: Wallet balance and itemized collection list.
/// </summary>
public class AssistantWalletDto
{
    public long AssistantId { get; set; }
    public string AssistantName { get; set; } = null!;
    public decimal CurrentBalance { get; set; }
    public decimal TotalCollected { get; set; }
    public int TransactionCount { get; set; }
    public DateTime? LastCollectionAt { get; set; }
}

/// <summary>
/// Aggregated view of all assistant wallets for a teacher.
/// REQ-PAY-035: Tutor views current wallet balance of each assistant, plus the
/// combined total currently held across all assistants.
/// </summary>
public class AssistantWalletsSummaryDto
{
    /// <summary>Sum of <see cref="AssistantWalletDto.CurrentBalance"/> across all assistants.</summary>
    public decimal TotalCurrentBalance { get; set; }
    public List<AssistantWalletDto> Assistants { get; set; } = new();
}

/// <summary>
/// Input DTO for resetting an assistant's wallet.
/// REQ-PAY-036: Tutor confirms cash handover.
/// </summary>
public class WalletResetDto
{
    public long TeacherId { get; set; }
    public long AssistantId { get; set; }
    public long ResetByUserId { get; set; }
}

/// <summary>
/// Display DTO for a wallet reset log entry.
/// REQ-PAY-037: Permanent ledger event.
/// </summary>
public class WalletResetLogDto
{
    public long Id { get; set; }
    public string? AssistantName { get; set; }
    public decimal AmountReset { get; set; }
    public DateTime ResetAt { get; set; }
    public long ResetByUserId { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// DASHBOARD DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Filter criteria for the Payment Overview Dashboard.
/// REQ-PAY-043: Filterable by session, group, payment type, date range.
/// </summary>
public class PaymentDashboardFilterDto
{
    public long? SessionId { get; set; }
    public long? SessionGroupId { get; set; }
    public PaymentType? PaymentType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Payment Overview Dashboard data.
/// REQ-PAY-039/040/041/042: Expected, collected, remaining revenue.
/// </summary>
public class PaymentDashboardDto
{
    /// <summary>
    /// Teacher-wide expected revenue. <c>null</c> when the caller is an assistant — an
    /// assistant's own dashboard has no teacher-wide expectation, and null (not 0) lets the
    /// client distinguish "not available to you" from a genuine zero.
    /// TODO(assistant-dashboard): a real per-assistant target/expected figure is to be
    /// designed and built end-to-end by frontend + backend.
    /// </summary>
    public decimal? ExpectedRevenue { get; set; }

    /// <summary>
    /// Collected revenue. Teacher caller → all collectors combined. Assistant caller →
    /// ONLY the money that assistant personally collected.
    /// </summary>
    public decimal CollectedRevenue { get; set; }

    /// <summary>Teacher-wide remaining revenue. <c>null</c> for an assistant caller (see <see cref="ExpectedRevenue"/>).</summary>
    public decimal? RemainingRevenue { get; set; }

    /// <summary>
    /// "Books &amp; fees" cash in the range. ADDITIVE — <see cref="CollectedRevenue"/> keeps its
    /// existing meaning so no deployed figure changes.
    ///
    /// <para>Note that <see cref="ExpectedRevenue"/> and <see cref="RemainingRevenue"/> are
    /// deliberately untouched: they are PERIOD-based (the obligation lens, <c>GetDashboardAggregates
    /// Async</c>) and an extras obligation has no installment period, so folding extras in would
    /// make "expected" mean two incompatible things at once.</para>
    /// </summary>
    public decimal CollectedRevenueExtras { get; set; }

    /// <summary>All cash in the range = <see cref="CollectedRevenue"/> +
    /// <see cref="CollectedRevenueExtras"/>.</summary>
    public decimal CollectedRevenueAllSources { get; set; }

    /// <summary>Per-session breakdown. <c>null</c> for an assistant caller (teacher-wide view, not their own data).</summary>
    public List<SessionRevenueBreakdownDto>? PerSessionBreakdown { get; set; } = new();

    /// <summary>Per-collector breakdown. Assistant caller → a single entry (themselves).</summary>
    public List<CollectorRevenueBreakdownDto> PerCollectorBreakdown { get; set; } = new();
}

/// <summary>
/// Per-session revenue breakdown.
/// REQ-PAY-043: Per-session granularity.
/// </summary>
public class SessionRevenueBreakdownDto
{
    public long SessionId { get; set; }
    public string SessionName { get; set; } = null!;
    public decimal Expected { get; set; }
    public decimal Collected { get; set; }
    public decimal Remaining { get; set; }
}

/// <summary>
/// Per-collector revenue breakdown.
/// REQ-PAY-041: Broken down by collecting users.
/// </summary>
public class CollectorRevenueBreakdownDto
{
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public decimal Collected { get; set; }
    public int TransactionCount { get; set; }

    /// <summary>This collector's "Books &amp; fees" cash in the range (additive).</summary>
    public decimal CollectedExtras { get; set; }

    /// <summary>Number of "Books &amp; fees" collections this collector took in the range.</summary>
    public int ExtrasTransactionCount { get; set; }
}

/// <summary>
/// Display DTO for the "Collected by Sessions" card — one row per currently
/// active session (<c>EndDate &gt;= today</c>).
/// REQ-PAY-043: Per-session collection progress while the session is active.
/// </summary>
public class SessionCollectionSummaryDto
{
    public long SessionId { get; set; }
    public string SessionName { get; set; } = null!;

    /// <summary>e.g. "Saturday - 5:00 PM - prep3".</summary>
    public string ScheduleLabel { get; set; } = null!;
    public decimal CollectedAmount { get; set; }
    public decimal ExpectedAmount { get; set; }
    public int PaidStudentCount { get; set; }
    public int TotalStudentCount { get; set; }

    /// <summary>Rounded 0-100. <c>CollectedAmount / ExpectedAmount</c>; 0 when nothing is due yet.</summary>
    public decimal PercentCollected { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// DEPARTURE DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Departure summary displayed before confirmation.
/// REQ-PAY-072: All fields shown on departure summary screen.
/// </summary>
public class DepartureSummaryDto
{
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string SessionName { get; set; } = null!;
    public string CurrentPeriodLabel { get; set; } = null!;
    public int TotalOccurrencesInPeriod { get; set; }
    public int AttendedOccurrences { get; set; }
    public decimal FullPeriodAmount { get; set; }
    public decimal ProRatedAmount { get; set; }
    public PaymentStatus PaymentStatusAtDeparture { get; set; }
    public DepartureOutcome DepartureOutcome { get; set; }
    public decimal FinalAmount { get; set; }
    public string OutcomeLabel { get; set; } = null!;

    // ── Anchored-month disclosure (added with the attendance-based refund correction) ──
    // The refund is always computed against ONE month: the month of the student's LATEST
    // PAID period in this session (or the teacher-local current month when they never paid).
    // These fields let the frontend show exactly which month the numbers describe.

    /// <summary>First day of the anchored month the refund was calculated against.</summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>Display label of the anchored month (e.g. "July 2026").</summary>
    public string MonthLabel { get; set; } = null!;

    /// <summary>
    /// Id of the anchored <c>PaymentPeriod</c> (the latest paid month). Null when the student
    /// never paid in this session — nothing to reverse at confirm time.
    /// </summary>
    public long? PaymentPeriodId { get; set; }

    /// <summary>
    /// Cash the student actually paid for the anchored month (<c>PaymentPeriod.AmountPaid</c>).
    /// This is the refund base and the hard ceiling for a tutor override on a refund.
    /// </summary>
    public decimal PaidAmount { get; set; }
}

/// <summary>
/// Input DTO for confirming a student departure.
/// REQ-PAY-073: Confirm Departure action.
/// REQ-PAY-075: Optional tutor override.
/// </summary>
public class ConfirmDepartureDto
{
    public long TeacherId { get; set; }
    public long TeacherStudentId { get; set; }
    public long SessionId { get; set; }
    public decimal? OverrideAmount { get; set; }
    public long ConfirmedByUserId { get; set; }

    /// <summary>
    /// Optional (defaults false — existing clients unaffected). When true, the student is also
    /// soft-deleted (moved to the recycle bin) as part of the departure, not just unassigned.
    /// When false, the departure only unassigns the student and processes the refund.
    /// </summary>
    public bool DeleteStudent { get; set; } = false;
}

/// <summary>
/// Display DTO for a student departure record.
/// </summary>
public class StudentDepartureDto
{
    public long Id { get; set; }
    public string SessionName { get; set; } = null!;
    public string? StudentName { get; set; }
    public PaymentStatus PaymentStatusAtDeparture { get; set; }
    public int TotalOccurrencesInPeriod { get; set; }
    public int AttendedOccurrences { get; set; }
    public decimal FullPeriodAmount { get; set; }
    public decimal ProRatedAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public bool IsTutorOverride { get; set; }
    public DepartureOutcome DepartureOutcome { get; set; }
    public DateTime DepartedAt { get; set; }
}

/// <summary>One row in the teacher-wide departed-students list (GET api/Payment/departures).</summary>
public class DepartureListItemDto
{
    public long Id { get; set; }
    public string? StudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }
    public string? SessionName { get; set; }
    public DateTime DepartedAt { get; set; }
    /// <summary>RefundDue | AmountOwed | NoObligation</summary>
    public string DepartureOutcome { get; set; } = string.Empty;
    public decimal FinalAmount { get; set; }
    /// <summary>True when the departure produced a refund to the student.</summary>
    public bool IsRefund { get; set; }

    /// <summary>Sessions attended in the anchored month (the "3" in "3/15").</summary>
    public int AttendedOccurrences { get; set; }
    /// <summary>Total sessions scheduled in the anchored month (the "15" in "3/15").</summary>
    public int TotalOccurrencesInPeriod { get; set; }
    /// <summary>The month's full price before attendance proration.</summary>
    public decimal FullPeriodAmount { get; set; }
    /// <summary>The attendance-prorated worth of the attended sessions.</summary>
    public decimal ProRatedAmount { get; set; }
    /// <summary>Payment status at departure (Paid | PartiallyPaid | Unpaid | …) — drives "didn't pay" copy.</summary>
    public string PaymentStatusAtDeparture { get; set; } = string.Empty;

    // ── The story: why this figure, and whether a human changed it (REQ-PAY-075) ──

    /// <summary>What the system calculated before any tutor override — the "should have been 200".</summary>
    public decimal OriginalCalculatedAmount { get; set; }
    /// <summary>True when the tutor settled on a figure other than the calculated one (0 included).</summary>
    public bool IsTutorOverride { get; set; }
    /// <summary>
    /// True when the tutor waived a refund entirely (overrode a RefundDue down to 0). Lets the client
    /// label the row honestly instead of rendering the contradictory "Refunded 0".
    /// </summary>
    public bool IsWaivedRefund { get; set; }
    /// <summary>
    /// First day of the anchored month — by construction the LAST month the student paid for.
    /// Null on rows recorded before this was captured; clients must degrade gracefully.
    /// </summary>
    public DateTime? AnchorPeriodStart { get; set; }
    /// <summary>Cash paid for the anchored month at departure. Null on historical rows.</summary>
    public decimal? PaidAmountAtDeparture { get; set; }
    /// <summary>
    /// Stable calendar-day key ("yyyy-MM-dd") of the raw UTC DepartedAt — the client groups the list
    /// into day sections by this string. Matches <c>CollectionRow.DayKey</c> so both ledgers bucket
    /// days identically (never re-derive it from a local date on the client).
    /// </summary>
    public string DayKey { get; set; } = string.Empty;

    // ── Amount correction (REQ-PAY-075, 2026-09-15) ──

    /// <summary>
    /// <see cref="FinalAmount"/> as it stood BEFORE the tutor corrected it, so the card can say
    /// "was 300 · now 190". Null when this departure has never been corrected — which is also how a
    /// client tells "corrected" from "settled first time": never infer it from the amount. Additive.
    /// </summary>
    public decimal? AmountBeforeEdit { get; set; }

    /// <summary>Display name of the tutor who corrected the figure. Null when never corrected.</summary>
    public string? AmountEditedByName { get; set; }

    /// <summary>UTC instant of the correction. Null when never corrected.</summary>
    public DateTime? AmountEditedAt { get; set; }

    /// <summary>The tutor's stated reason for the correction — required when one is made.</summary>
    public string? AmountEditNote { get; set; }

    /// <summary>
    /// Whether this row's figure can still be corrected, and it is the SERVER's answer, not the
    /// client's guess. False for a NoObligation departure (there is nothing to move), and false once
    /// the student has been permanently deleted — the periods and counter the correction has to move
    /// no longer exist. The client hides the action rather than offering one that will 422.
    /// </summary>
    public bool CanEditAmount { get; set; }
}

/// <summary>
/// Request to CORRECT the settled amount of a departure already confirmed — a figure entered by
/// mistake, not a re-run of the departure (REQ-PAY-075). Tutor-only.
/// </summary>
public class EditDepartureAmountDto
{
    /// <summary>
    /// The corrected settlement. Must be ≥ 0 and within the same ceiling the original confirmation
    /// enforced: a refund can never exceed the cash actually paid for the anchored month, and an owed
    /// amount can never exceed that month's full price.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Why it is being changed, in the tutor's own words. REQUIRED — a settled figure quietly becoming
    /// a different settled figure is exactly the change that must carry a reason.
    /// </summary>
    public string? Note { get; set; }
}

/// <summary>What a departure-amount correction actually did, so the client can say it rather than "Saved".</summary>
public class DepartureAmountEditResultDto
{
    public long DepartureId { get; set; }
    public string? StudentName { get; set; }

    /// <summary>RefundDue | AmountOwed — unchanged by a correction; only the amount moves.</summary>
    public string DepartureOutcome { get; set; } = string.Empty;

    /// <summary>The figure before this correction.</summary>
    public decimal PreviousAmount { get; set; }

    /// <summary>The figure now.</summary>
    public decimal NewAmount { get; set; }

    /// <summary>
    /// New − Previous. Positive means MORE money leaves the tutor's side (a bigger refund, or a
    /// bigger debt recorded against the student); negative means less.
    /// </summary>
    public decimal Delta { get; set; }

    /// <summary>
    /// Display name of the collector whose cash bag absorbed the difference, when one did. Null when
    /// the departure was confirmed by the tutor — they hold their own cash and have no wallet, so
    /// nothing moved in any bag. Never null-as-unknown: null means "no bag was involved".
    /// </summary>
    public string? WalletAdjustedForName { get; set; }
}

/// <summary>One calendar day's totals in the departed-students list — drives the day-separator header.</summary>
public class DepartureDailyTotalDto
{
    /// <summary>Stable calendar-day key ("yyyy-MM-dd") — matches <see cref="DepartureListItemDto.DayKey"/>.</summary>
    public string DateKey { get; set; } = string.Empty;
    /// <summary>The calendar day (date component only).</summary>
    public DateTime Date { get; set; }
    /// <summary>How many students departed on this day.</summary>
    public int DepartedCount { get; set; }
    /// <summary>Total actually refunded on this day (a waived refund contributes 0).</summary>
    public decimal RefundedTotal { get; set; }
    /// <summary>Total recorded as still owed on this day.</summary>
    public decimal OwedTotal { get; set; }
}

/// <summary>Paged response for the departed-students list.</summary>
public class DeparturesResponse
{
    public int Page { get; set; }
    public int Limit { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public List<DepartureListItemDto> Departures { get; set; } = new();
    /// <summary>
    /// Per-day totals across the WHOLE filtered scope (not just this page), so the day-separator
    /// figures stay correct as the client pages in more rows.
    /// </summary>
    public List<DepartureDailyTotalDto> DailyTotals { get; set; } = new();
}

// ══════════════════════════════════════════════════════════════════════════
// SESSION TRANSFER DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Pre-transfer financial summary.
/// REQ-PAY-088: Shown before tutor confirms transfer.
/// </summary>
public class TransferSummaryDto
{
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string SourceSessionName { get; set; } = null!;
    public string DestinationSessionName { get; set; } = null!;
    public PaymentStatus PaymentStatusInSource { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal CreditBalance { get; set; }
    public string DestinationPaymentType { get; set; } = null!;
    public decimal DestinationSessionAmount { get; set; }
}

/// <summary>
/// Input DTO for confirming a session transfer.
/// REQ-PAY-088: Confirm Transfer action.
/// </summary>
public class ConfirmTransferDto
{
    public long TeacherId { get; set; }
    public long TeacherStudentId { get; set; }
    public long SourceSessionId { get; set; }
    public long DestinationSessionId { get; set; }
    public long TransferredByUserId { get; set; }
}

/// <summary>
/// Display DTO for a session transfer event.
/// REQ-PAY-089: Permanently retained in history.
/// </summary>
public class SessionTransferEventDto
{
    public long Id { get; set; }
    public string SourceSessionName { get; set; } = null!;
    public string DestinationSessionName { get; set; } = null!;
    public PaymentStatus PaymentStatusAtTransfer { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal CreditBalance { get; set; }
    public DateTime TransferredAt { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// OFFLINE SYNC DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for syncing offline-collected payment records.
/// REQ-PAY-080/081: Batch sync on reconnection.
/// </summary>
public class OfflinePaymentSyncRequestDto
{
    public long TeacherId { get; set; }
    public List<CollectPaymentDto> OfflineRecords { get; set; } = new();
}

/// <summary>
/// Result of an offline sync operation.
/// REQ-PAY-080: Shows how many records were synced.
/// REQ-PAY-082: Lists conflicts for resolution.
/// Per-record outcomes ride <see cref="EntryResults"/> (keyed by
/// ClientEntryId) so the client can transition each queued op individually —
/// mirrors attendance's SyncResultDto. The aggregate counts and
/// <see cref="Conflicts"/> stay for wire compatibility.
/// </summary>
public class PaymentSyncResultDto
{
    /// <summary>Records that were recorded, or acknowledged as already recorded by an earlier run.</summary>
    public int SyncedCount { get; set; }

    /// <summary>
    /// Records whose <see cref="PaymentSyncEntryResultDto.IsConflict"/> is true - today that is
    /// exactly one situation: the student was already paid up, so there was nothing left to take.
    /// <para>
    /// A SUBSET of <see cref="FailedCount"/>, never added to it - the same relationship attendance's
    /// <c>SyncResultDto.DuplicateCount</c> has to its successes, and chosen for the same reason:
    /// <see cref="FailedCount"/> keeps the value it has always had, so no deployed client sees a
    /// number move under it.
    /// </para>
    /// <para>
    /// It was never incremented before 2026-09-15, which made it permanently 0 and the batch
    /// message permanently "synced successfully" regardless of what happened. It is incremented
    /// from the same flag the per-entry list reports, so the count and the list can never disagree.
    /// </para>
    /// </summary>
    public int ConflictCount { get; set; }

    /// <summary>Records that recorded nothing, for any reason (conflicts included).</summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Legacy pairing of an offline record with the COMPETING SERVER RECORD that blocked it
    /// (REQ-PAY-082). It is empty, and that is the honest answer rather than an oversight: the
    /// cross-device same-day conflict that used to fill it was deliberately removed (2026-09-02,
    /// two people may legitimately collect from one student on one day), and the one conflict the
    /// engine still produces - "already paid up, nothing owed in the payable window" - has no
    /// competing row to point at. Per-record detail rides <see cref="EntryResults"/>.
    /// <para>
    /// Do not start filling it without first widening
    /// <see cref="PaymentConflictDto.ExistingRecord"/> to nullable: it is non-nullable, and a
    /// deployed client's never-yet-exercised parser for this array could fault on a null.
    /// </para>
    /// </summary>
    public List<PaymentConflictDto> Conflicts { get; set; } = new();

    /// <summary>Per-record outcome, keyed by ClientEntryId. What clients actually read.</summary>
    public List<PaymentSyncEntryResultDto> EntryResults { get; set; } = new();
}

/// <summary>
/// Outcome of one offline payment record in a sync batch.
/// </summary>
public class PaymentSyncEntryResultDto
{
    /// <summary>Echo of the record's client-generated id (may be null for
    /// legacy clients that did not send one).</summary>
    public string? ClientEntryId { get; set; }
    public bool Success { get; set; }
    public bool IsConflict { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Stable machine-readable reason a record was NOT recorded (the message key behind
    /// <see cref="ErrorMessage"/>, e.g. <c>PaymentAmountExceedsAdvanceLimit</c>, or
    /// <c>StudentNotAssignedToSession</c> / <c>StudentNotFound</c> for the pre-collect checks).
    /// The app needs it to tell the collector what to DO about cash already in their hand —
    /// the localized sentence alone cannot be branched on. Null on success. Additive.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>True when this record was already recorded by an earlier
    /// sync (ClientEntryId dedup) — success without a new transaction.</summary>
    public bool AlreadySynced { get; set; }

    /// <summary>The existing transaction on conflict / already-synced.</summary>
    public PaymentTransactionDto? ExistingRecord { get; set; }

    /// <summary>
    /// True when <see cref="AlreadySynced"/> is set AND the transaction this record was already
    /// recorded as has since been DELETED by the tutor. The replay is still reported as handled -
    /// the cash event was applied once and the tutor's later decision to remove it is theirs, not
    /// something a retry may undo - but a client should not show a live receipt for money that is
    /// no longer in the ledger. Additive; false on every other outcome.
    /// </summary>
    public bool ExistingRecordDeleted { get; set; }

    /// <summary>
    /// True when the collecting device's reported instant was NOT used to date this collection
    /// because it fell outside the accepted window (in the future relative to the server, or older
    /// than a week), and the server's own clock was used instead. The money is recorded either way;
    /// what moved is which DAY - and so which month's totals - the cash counts in.
    /// <para>
    /// Machine-readable on purpose, and deliberately NOT a blocking prompt: this arrives on a
    /// background drain with no human in front of it (the same reason a replay is never withheld
    /// for a confirmation), and "your phone's clock is wrong" is not something a collector can act
    /// on at the classroom door. The durable trace lives on the row itself
    /// (<c>PaymentTransaction.DeviceReportedCollectedAt</c>); this flag just lets a client that
    /// wants to mention it do so. Additive.
    /// </para>
    /// </summary>
    public bool CollectedAtAdjusted { get; set; }

    // ── Settlement echo (additive) ────────────────────────────────────────────────────────────
    // An offline collection carries a CLIENT-computed amount and the server applies it verbatim —
    // it never recomputes or rewrites it, because the cash physically changed hands and silently
    // recording a different number would break wallet reconciliation. But when the device's cached
    // figure was stale (or, before the client fix, simply wrong — the offline lookup showed the full
    // monthly rate instead of the prorated/arrears total), the collector quoted one number and the
    // ledger settled another, with nothing on any screen saying so.
    //
    // These three fields report that difference so the app can tell the teacher exactly what
    // happened. Purely additive: older clients ignore unknown JSON members, so the LIVE build is
    // unaffected. All three are null on failures, conflicts and already-synced replays.

    /// <summary>Cash actually applied to periods by this record — the amount the collector took.</summary>
    public decimal? AppliedAmount { get; set; }

    /// <summary>What the student actually owed at sync time across the months this cash targeted
    /// (Σ remaining due). Differs from <see cref="AppliedAmount"/> when the device's cached figure
    /// was stale or wrong; equal on the healthy path.</summary>
    public decimal? AmountDueAtSync { get; set; }

    /// <summary>The months this cash cleared or part-cleared, as "YYYY-MM", oldest first — so the
    /// app can say WHERE an over-payment landed rather than only that one occurred.</summary>
    public List<string>? SettledMonths { get; set; }
}

/// <summary>
/// A detected sync conflict.
/// REQ-PAY-082: Both records shown for tutor resolution.
/// </summary>
public class PaymentConflictDto
{
    public CollectPaymentDto OfflineRecord { get; set; } = null!;
    public PaymentTransactionDto ExistingRecord { get; set; } = null!;
    public string ConflictReason { get; set; } = null!;
}

// ══════════════════════════════════════════════════════════════════════════
// REPORT DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for generating a payment report.
/// REQ-PAY-048: Ten report types.
/// </summary>
public class PaymentReportRequestDto
{
    public PaymentReportType ReportType { get; set; }
    public long? StudentId { get; set; }
    public long? SessionId { get; set; }
    public long? SessionGroupId { get; set; }
    public long? AssistantId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public PaymentType? PaymentType { get; set; }
    public int? MinConsecutiveUnpaid { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}

/// <summary>
/// Standard report header.
/// REQ-PAY-049: All reports display standard header information.
/// </summary>
public class PaymentReportHeaderDto
{
    public string TutorAccountName { get; set; } = null!;
    public string ReportType { get; set; } = null!;
    public string ReportTitle { get; set; } = null!;
    public DateTime GeneratedAt { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public string? ActiveFilters { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
// STUDENT/PARENT VIEW DTOs
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Payment data visible to students/parents.
/// Gated by TeacherConfiguration.StudentVisibilityPayment / ParentVisibilityPayment.
/// </summary>
public class StudentPaymentViewDto
{
    public string SessionName { get; set; } = null!;
    public PaymentStatus CurrentStatus { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Outstanding { get; set; }
    public List<PaymentPeriodDto> Periods { get; set; } = new();
}
// ══════════════════════════════════════════════════════════════════════════
// BATCH COLLECTION DTOs (UI: "Mark N students as Paid")
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for collecting payments from multiple students in one request.
/// SessionId and PaymentMethod are shared across all items (the collection screen
/// is session-scoped). TeacherId and CollectedByUserId are intentionally absent —
/// both are resolved from the JWT by the presentation layer, matching the single
/// collect endpoint (REQ-PAY-011 / BR-PAY-004).
/// </summary>
public class BatchCollectPaymentDto
{
    public long SessionId { get; set; }
    public PaymentCollectionMethod PaymentMethod { get; set; }

    /// <summary>Cascades to each item's same-day duplicate confirmation (REQ-PAY-020).</summary>
    public bool ConfirmAllDuplicates { get; set; } = false;

    /// <summary>Cascades to each item's already-paid confirmation (REQ-PAY-026).</summary>
    public bool ConfirmAllAlreadyPaid { get; set; } = false;

    public List<BatchCollectItemDto> Items { get; set; } = new();
}

/// <summary>A single student entry within a batch collection request.</summary>
public class BatchCollectItemDto
{
    public long TeacherStudentId { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Online payment reference, for online collection methods only (REQ-PAY-008).</summary>
    public string? OnlineTransactionRef { get; set; }
}

/// <summary>
/// Aggregated result of a batch collection.
/// Mirrors the offline-sync result shape: totals plus a per-student outcome list.
/// </summary>
public class BatchCollectResultDto
{
    public int TotalRequested { get; set; }
    public int CollectedCount { get; set; }
    public int NeedsConfirmationCount { get; set; }
    public int FailedCount { get; set; }
    public List<BatchCollectItemResultDto> Results { get; set; } = new();
}

/// <summary>Per-student outcome within a batch collection.</summary>
public class BatchCollectItemResultDto
{
    public long TeacherStudentId { get; set; }
    public BatchCollectItemStatus Status { get; set; }
    /// <summary>Localized outcome message (reused from the single-collect path).</summary>
    public string? Message { get; set; }
    /// <summary>The created transaction — populated only when Status is Collected.</summary>
    public PaymentTransactionDto? Transaction { get; set; }
    /// <summary>REQ-PAY-020: same-day duplicate — requires ConfirmAllDuplicates to proceed.</summary>
    public bool IsSameDayDuplicate { get; set; }
    /// <summary>REQ-PAY-026: period already fully paid — requires ConfirmAllAlreadyPaid to proceed.</summary>
    public bool IsAlreadyPaid { get; set; }
    /// <summary>Total already collected today for this student, when a same-day duplicate is detected.</summary>
    public decimal? TodayPaidAmount { get; set; }
}
// ══════════════════════════════════════════════════════════════════════════
// BATCH EDIT DTOs  (UI: "Saved N changes" / "Submit N students" — D2)
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for batch-editing payment transactions (D2).
/// Mirrors the batch-collect request shape: TeacherId and EditedByUserId are
/// intentionally ABSENT — both are resolved from the JWT by the presentation layer,
/// never trusted from the body (BR-PAY-002). Each item is applied independently via
/// EditPaymentAsync — the single source of truth for all edit business rules.
/// </summary>
public class BatchEditPaymentDto
{
    public List<BatchEditItemDto> Items { get; set; } = new();
}

/// <summary>A single transaction edit within a batch request.</summary>
public class BatchEditItemDto
{
    public long TransactionId { get; set; }
    /// <summary>New paid amount; null leaves the amount unchanged.</summary>
    public decimal? NewAmount { get; set; }
    /// <summary>New status (reversal is a status change → logged as Reversed); null leaves it unchanged.</summary>
    public PaymentStatus? NewStatus { get; set; }
    /// <summary>Reassign the transaction to a different period; null leaves it unchanged.</summary>
    public long? NewPaymentPeriodId { get; set; }
    /// <summary>Optional audit reason recorded on the PaymentEditLog (≤ EditReasonMaxLength).</summary>
    public string? EditReason { get; set; }
}

/// <summary>
/// Aggregated result of a batch edit. Mirrors BatchCollectResultDto:
/// totals plus a per-transaction outcome list.
/// (Requirement wording: TotalRequested / Successful / Failed.)
/// </summary>
public class BatchEditResultDto
{
    public int TotalRequested { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public List<BatchEditItemResultDto> Results { get; set; } = new();
}

/// <summary>Per-transaction outcome within a batch edit.</summary>
public class BatchEditItemResultDto
{
    public long TransactionId { get; set; }
    public BatchEditItemStatus Status { get; set; }
    /// <summary>Underlying HTTP status from the reused edit path:
    /// 200 success, 400 validation, 404 not found/not owned, 500 unexpected.</summary>
    public int StatusCode { get; set; }
    /// <summary>Localized outcome message (reused from the single-edit path).</summary>
    public string? Message { get; set; }
    /// <summary>The updated transaction — populated only when Status is Succeeded.</summary>
    public PaymentTransactionDto? Transaction { get; set; }
}
// ══════════════════════════════════════════════════════════════════════════
// BATCH REVERT DTOs  (UI: "Revert (N students)" — D1)
// ══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Input DTO for batch-reverting payment transactions (D1).
/// A revert is expressed as an edit that zeroes the collected amount and marks the
/// transaction Unpaid, reusing EditPaymentAsync (single source of truth). TeacherId and
/// EditedByUserId are resolved from the JWT by the presentation layer, never from the body
/// (BR-PAY-002). One Reason applies to every transaction in the batch and is recorded on
/// each PaymentEditLog.
/// </summary>
public class BatchRevertPaymentDto
{
    public List<long> TransactionIds { get; set; } = new();
    /// <summary>Audit reason recorded on every reverted transaction's PaymentEditLog (≤ EditReasonMaxLength).</summary>
    public string? Reason { get; set; }
}

/// <summary>Aggregated result of a batch revert. Mirrors BatchEditResultDto.</summary>
public class BatchRevertResultDto
{
    public int TotalRequested { get; set; }
    public int RevertedCount { get; set; }
    public int FailedCount { get; set; }
    public List<BatchRevertItemResultDto> Results { get; set; } = new();
}

/// <summary>Per-transaction outcome within a batch revert.</summary>
public class BatchRevertItemResultDto
{
    public long TransactionId { get; set; }
    /// <summary>Reuses BatchEditItemStatus — a revert has the same binary Succeeded/Failed outcome as an edit.</summary>
    public BatchEditItemStatus Status { get; set; }
    /// <summary>Underlying HTTP status from the reused edit path: 200 success, 404 not found/not owned, 500 unexpected.</summary>
    public int StatusCode { get; set; }
    /// <summary>Localized outcome message (reused from the single-edit path).</summary>
    public string? Message { get; set; }
    /// <summary>The reverted transaction (amount 0, status Unpaid) — populated only when Status is Succeeded.</summary>
    public PaymentTransactionDto? Transaction { get; set; }
}