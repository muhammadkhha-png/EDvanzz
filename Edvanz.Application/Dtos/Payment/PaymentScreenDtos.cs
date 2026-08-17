using System;
using System.Collections.Generic;

namespace Edvanz.Application.Dtos.Payment;

// ══════════════════════════════════════════════════════════════════════════
// PAYMENT "SCREENS" DTOs  (frontend payment.json spec — api/v1/*)
//
// These map 1:1 to the shapes the frontend designed in payment.json. They are
// intentionally separate from the existing PaymentDtos so the screen contract can
// evolve without disturbing the current PaymentController DTOs.
//
// CONVENTIONS:
//  - PascalCase C# properties serialize to camelCase JSON (MvcJsonDefaults.Web),
//    which is exactly what the frontend expects (monthLabel, studentId, ...).
//  - Entity ids are exposed as STRINGS per the frontend contract (backend uses long).
//  - DateTimes are returned as-is (System.Text.Json emits ISO-8601).
// ══════════════════════════════════════════════════════════════════════════

// ── Screen: SessionPaymentCollectedByMonth ─────────────────────────────────

/// <summary>Paginated ledger of collected student payments for a given month + year.</summary>
public class CollectionsByMonthResponse
{
    public int Month { get; set; }
    public int Year { get; set; }
    public string MonthLabel { get; set; } = string.Empty;
    public int Page { get; set; }
    public int Limit { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }

    /// <summary>
    /// Echo of an applied date-range filter (inclusive). Populated ONLY when the caller passed
    /// <c>from</c>/<c>to</c>; null on the month/year path so existing consumers are unaffected.
    /// </summary>
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public List<CollectionRow> Items { get; set; } = new();

    /// <summary>
    /// Distribution of the money collected in this scope by per-month amount — one entry per distinct
    /// amount (e.g. 300 → 12, 200 → 7, 100 → 20), highest count first. Derived from the per-period
    /// settlement slices so a multi-month payment (e.g. 600 = 2×300) counts once per month at its
    /// monthly amount. Covers the whole scope (not just the current page); withdrawals are excluded.
    /// </summary>
    public List<CollectionAmountTier> AmountTiers { get; set; } = new();
}

/// <summary>One "how many paid X" bucket for the collections summary cards.</summary>
public class CollectionAmountTier
{
    /// <summary>The per-month amount collected (a settlement slice's applied amount).</summary>
    public decimal Amount { get; set; }
    /// <summary>How many month-payments were collected at this amount in scope.</summary>
    public int Count { get; set; }
}

/// <summary>One row in the collected-payments ledger.</summary>
public class CollectionRow
{
    public string Id { get; set; } = string.Empty;
    public int Index { get; set; }
    public string? StudentId { get; set; }
    public string? StudentName { get; set; }
    /// <summary>The teacher's roster code for the student (e.g. "123"); null for a withdrawal line.</summary>
    public string? StudentCode { get; set; }
    /// <summary>Positive for a collection; NEGATIVE for a departure refund (money returned).</summary>
    public decimal Amount { get; set; }
    /// <summary>collected | pending | refund | withdrawal. Refund/withdrawal are negative-amount lines.</summary>
    public string Status { get; set; } = "collected";
    public string? SessionName { get; set; }
    public DateTime? CollectedAt { get; set; }

    /// <summary>True when this row is a student-departure refund (negative amount).</summary>
    public bool IsRefund { get; set; }

    /// <summary>
    /// True when this row is a cash withdrawal/hand-over taken from this collector's wallet
    /// (negative amount, no student). Rendered as a hand-over line, not a student row.
    /// </summary>
    public bool IsWithdrawal { get; set; }

    /// <summary>
    /// Number of months (settlement slices) this collection cleared — 1 for a normal single-month
    /// payment, &gt;1 when one cash event paid several months at once (e.g. 600 = 2 months). Lets the
    /// client show "N months" so a large amount isn't mistaken for a single month. 0/1 for refund and
    /// withdrawal lines.
    /// </summary>
    public int PeriodsCovered { get; set; }

    /// <summary>
    /// For a refund only: display label of the month the departure applies to (e.g. "March 2026").
    /// Null for a normal collection.
    /// </summary>
    public string? RefundedForMonthLabel { get; set; }
}

// ── Screen: Collections summary (date-filtered) ────────────────────────────

/// <summary>
/// Period summary for the collections/payment screens when a date filter is applied. Composed
/// entirely from existing repo aggregates (reuse-first). IMPORTANT — mixed time semantics, by design:
/// the money/activity and departure/collector metrics honour the exact <c>[from,to]</c> range
/// (keyed on transaction/departure date), while the student status counts are anchored to
/// <c>asOfMonth</c> because payment status ("paid / partial / prorated / unpaid") is defined
/// per calendar month, not over an arbitrary range. The frontend labels each group accordingly.
/// </summary>
public class CollectionsSummaryResponse
{
    // Echo of the applied filter.
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    /// <summary>The "YYYY-MM" the status counts are anchored to (defaults to the month of <c>To</c>).</summary>
    public string AsOfMonth { get; set; } = string.Empty;
    public string AsOfMonthLabel { get; set; } = string.Empty;

    // ── Money / activity — TRUE [from,to] range ──
    /// <summary>Cash physically collected in the range, net of departure refunds.</summary>
    public decimal NetCashCollected { get; set; }
    /// <summary>Departure refunds (money returned) confirmed in the range.</summary>
    public decimal RefundsTotal { get; set; }
    /// <summary>Number of collection transactions in the range.</summary>
    public int TransactionCount { get; set; }
    /// <summary>Distinct students who made at least one payment in the range.</summary>
    public int StudentsPaidCount { get; set; }

    // ── Student payment status — anchored to AsOfMonth ──
    /// <summary>Assigned students with no outstanding period through the month (fully settled).</summary>
    public int PaidInFullCount { get; set; }
    /// <summary>Students with a period this month partially settled (0 &lt; paid &lt; due).</summary>
    public int PartialCount { get; set; }
    public int ProratedCount { get; set; }
    public int UnpaidCount { get; set; }

    // ── Departures — TRUE [from,to] range ──
    public int DepartedCount { get; set; }
    public int DepartedRefundDueCount { get; set; }
    public int DepartedAmountOwedCount { get; set; }

    // ── Per-collector breakdown — TRUE [from,to] range ──
    public List<CollectionsSummaryCollectorDto> ByCollector { get; set; } = new();
}

/// <summary>One collector's collected total in the summary range (you or an assistant).</summary>
public class CollectionsSummaryCollectorDto
{
    /// <summary>The collector's user id (matches <c>collectedByUserId</c> on the collections list).</summary>
    public string UserId { get; set; } = string.Empty;
    public string? Name { get; set; }
    /// <summary>Teacher | Assistant.</summary>
    public string Role { get; set; } = "Collector";
    public decimal CollectedAmount { get; set; }
    public int TransactionCount { get; set; }
}

// ── Screen: AssistantWallet ────────────────────────────────────────────────

/// <summary>An assistant's wallet card + paginated recent collections.</summary>
public class AssistantWalletScreenResponse
{
    public AssistantWalletAssistantDto Assistant { get; set; } = new();
    public AssistantWalletInfoDto Wallet { get; set; } = new();
    public AssistantWalletCollectionsDto Collections { get; set; } = new();
}

public class AssistantWalletAssistantDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Role { get; set; } = "Assistant";
    /// <summary>No backing column exists yet — always null (documented gap).</summary>
    public string? AvatarUrl { get; set; }
    public int TransactionCount { get; set; }
}

public class AssistantWalletInfoDto
{
    /// <summary>Gross collections since the last handover (reset or withdraw).</summary>
    public decimal TotalCashCollected { get; set; }
    /// <summary>Gross refunds taken back from this collector since the last handover.</summary>
    public decimal TotalRefunded { get; set; }
    public decimal WalletBalance { get; set; }
    /// <summary>Lifetime collected -- never reduced by a reset/withdraw. REQ-PAY-035.</summary>
    public decimal TotalCollectedAllTime { get; set; }
    /// <summary>Lifetime transaction count -- NOT scoped to the Collections window below.</summary>
    public int CollectionsCount { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

public class AssistantWalletCollectionsDto
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    /// <summary>
    /// Start of the window below -- the last handover, or null if there has never been one
    /// (in which case this is the assistant's full lifetime history).
    /// </summary>
    public DateTime? SinceAt { get; set; }
    public List<AssistantWalletCollectionItemDto> Items { get; set; } = new();
}

public class AssistantWalletCollectionItemDto
{
    public string Id { get; set; } = string.Empty;
    public string? StudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }
    public string? SessionName { get; set; }
    public decimal Amount { get; set; }
    public DateTime CollectedAt { get; set; }
    /// <summary>
    /// Ledger line type so the client can label/render it: "collection" (positive, a student
    /// payment), "refund" (negative, cash handed back — e.g. a departure), or "withdrawal"
    /// (negative, a cash hand-over the tutor took from this wallet). The three together sum to
    /// the held balance since the last full hand-over.
    /// </summary>
    public string Kind { get; set; } = "collection";
}

// ── Screen: CollectPayment (student list) ──────────────────────────────────

/// <summary>Searchable, filterable, paginated student list for collecting payments.</summary>
public class CollectStudentsResponse
{
    public CollectStudentsCountsDto Counts { get; set; } = new();
    public int Page { get; set; }
    public int Limit { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public List<CollectStudentDto> Students { get; set; } = new();
}

public class CollectStudentsCountsDto
{
    public int All { get; set; }
    public int Assigned { get; set; }
    public int Unassigned { get; set; }
}

public class CollectStudentDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    /// <summary>Student code shown on the card (e.g. "#223"); also matched by search.</summary>
    public string? StudentCode { get; set; }
    public string? AvatarUrl { get; set; }
    public decimal Amount { get; set; }
    /// <summary>assigned | unassigned</summary>
    public string Assignment { get; set; } = "unassigned";
    /// <summary>paid | unpaid</summary>
    public string Status { get; set; } = "paid";
    public int UnpaidMonths { get; set; }
    /// <summary>True when the required amount is 0 (free/scholarship) → "Not paying" label.</summary>
    public bool IsExempt { get; set; }
}

// ── Screen: PaymentTracking (students by status) ───────────────────────────

/// <summary>Paginated student list filtered by payment status for a month.</summary>
public class StudentsByStatusResponse
{
    public string Month { get; set; } = string.Empty;
    public string MonthLabel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalCollected { get; set; }
    public decimal MonthAmount { get; set; }
    public decimal AmountPerMonth { get; set; }
    /// <summary>
    /// Full-set expected revenue for this scope: Σ over every in-scope student of their monthly
    /// rate (custom override ?? session default), including students with no month period yet.
    /// The session-detail screen renders this directly so per-student custom amounts are reflected
    /// (instead of the client computing session-default × student-count).
    /// </summary>
    public decimal ExpectedAmount { get; set; }
    public decimal TotalUnpaidAmount { get; set; }
    public int Total { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    public List<StudentByStatusDto> Students { get; set; } = new();
}

public class StudentByStatusDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal AmountPerMonth { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountDue { get; set; }
    public decimal UnpaidAmount { get; set; }
    public int UnpaidMonths { get; set; }
    /// <summary>Student code shown on the card (e.g. "#223").</summary>
    public string? StudentCode { get; set; }

    /// <summary>True when the required amount is 0 (free/scholarship) → "Not paying" label.</summary>
    public bool IsExempt { get; set; }

    /// <summary>Session the student paid on (falls back to the month's period session).</summary>
    public string? SessionName { get; set; }

    /// <summary>Date of the latest paying transaction this month; null when unpaid.</summary>
    public DateTime? PaidOn { get; set; }
}

// ── Screen: SessionPaymentCollectedByYear ──────────────────────────────────

/// <summary>Per-student month-by-month collection matrix for a selected year.</summary>
public class YearlyCollectionsResponse
{
    public int Year { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public List<YearlyStudentDto> Items { get; set; } = new();
}

public class YearlyStudentDto
{
    public string Id { get; set; } = string.Empty;
    public int Index { get; set; }
    public string? StudentName { get; set; }
    public YearlyStudentSummaryDto Summary { get; set; } = new();
    /// <summary>Only months the student has a period for; months with no period are absent.</summary>
    public List<YearlyMonthDto> Months { get; set; } = new();
}

public class YearlyStudentSummaryDto
{
    public int PaidMonths { get; set; }
    public int UnpaidMonths { get; set; }
    public decimal TotalCollected { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class YearlyMonthDto
{
    public int Month { get; set; }
    /// <summary>paid | unpaid | prorated</summary>
    public string Status { get; set; } = "unpaid";
    public decimal Amount { get; set; }
}

// ── Screen: CollectPaymentSession (lookup) ─────────────────────────────────

/// <summary>Resolves a scanned/typed student to a student + amount owed + paid state.</summary>
public class CollectLookupResponse
{
    public CollectLookupStudentDto Student { get; set; } = new();
    public decimal AmountDue { get; set; }
    /// <summary>paid | unpaid</summary>
    public string PaymentStatus { get; set; } = "unpaid";

    // ── Feature C additions (additive — existing fields above are unchanged) ──

    /// <summary>The student's per-month rate: custom override (else session amount, else 0).</summary>
    public decimal MonthlyAmount { get; set; }

    /// <summary>Number of unpaid months making up <see cref="TotalOwed"/> (arrears through the month).</summary>
    public int MonthsOwed { get; set; }

    /// <summary>Total arrears through the current teacher-local month — equal to <see cref="AmountDue"/>.</summary>
    public decimal TotalOwed { get; set; }
}

public class CollectLookupStudentDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Group { get; set; }
    public string? AvatarUrl { get; set; }
}

// ── Screen: PaymentTracking (aggregate) ────────────────────────────────────

/// <summary>Everything the PaymentTracking screen renders on load for a selected month.</summary>
public class TrackingResponse
{
    public string Month { get; set; } = string.Empty;
    public string MonthLabel { get; set; } = string.Empty;
    public TrackingSummaryDto Summary { get; set; } = new();
    public TrackingStatusBreakdownDto StatusBreakdown { get; set; } = new();
    public TrackingByAssistantDto CollectedByAssistant { get; set; } = new();
    public TrackingBySessionsDto CollectedBySessions { get; set; } = new();
}

public class TrackingSummaryDto
{
    public decimal ExpectedRevenue { get; set; }
    public int TotalStudents { get; set; }
    public int TotalSessions { get; set; }
    public decimal CollectedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public int SessionsCollected { get; set; }
    public int SessionsTotal { get; set; }
    public decimal ProgressPercent { get; set; }
}

public class TrackingStatusBreakdownDto
{
    public int Paid { get; set; }
    public int Prorated { get; set; }
    public int Unpaid { get; set; }
}

public class TrackingByAssistantDto
{
    public decimal TotalCollected { get; set; }
    public List<TrackingAssistantDto> Assistants { get; set; } = new();
}

public class TrackingAssistantDto
{
    /// <summary>The collector's user id (kept for back-compat / identity).</summary>
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "Collector";
    public int TransactionCount { get; set; }
    public decimal CollectedAmount { get; set; }

    /// <summary>The assistant id to open the wallet drill-in (route matches on AssistantId,
    /// NOT the user id). Null for the teacher row, which has no assistant wallet.</summary>
    public string? AssistantId { get; set; }

    /// <summary>Cash this assistant currently holds for the teacher (their wallet balance).
    /// 0 for the teacher row.</summary>
    public decimal WalletBalance { get; set; }
}

public class TrackingBySessionsDto
{
    public List<TrackingSessionDto> Sessions { get; set; } = new();
}

public class TrackingSessionDto
{
    public string Id { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Day { get; set; }
    public string? Time { get; set; }
    public string? Grade { get; set; }
    public decimal CollectedAmount { get; set; }
    public int StudentsCollected { get; set; }
    public int StudentsTotal { get; set; }
    public decimal ProgressPercent { get; set; }
    /// <summary>Group the session belongs to (null = ungrouped). Lets the app render
    /// groups + ungrouped sessions with per-group roll-ups and a group drill-in.</summary>
    public string? GroupId { get; set; }
    public string? GroupName { get; set; }
}

// ── Money: bulk mark-paid (CollectPayment screen) ──────────────────────────

public class MarkPaidRequest
{
    public List<long> StudentIds { get; set; } = new();
}

public class MarkPaidResponse
{
    public int MarkedPaidCount { get; set; }
    public List<MarkPaidResultDto> Results { get; set; } = new();
}

public class MarkPaidResultDto
{
    public string StudentId { get; set; } = string.Empty;
    /// <summary>paid | failed</summary>
    public string Status { get; set; } = "failed";
    public string? Reason { get; set; }
}

// ── Money: submit batch collection (CollectPaymentSession screen) ──────────

public class SubmitCollectionRequest
{
    /// <summary>Informational (YYYY-MM); collection still applies to the earliest unpaid period.</summary>
    public string? Month { get; set; }
    public long? ClassSessionId { get; set; }
    public List<SubmitCollectionItem> Students { get; set; } = new();

    /// <summary>
    /// Feature C: optional note applied to every collection in this submit. REQUIRED (else 400
    /// <c>CollectNoteRequired</c>) when any item's amount is a partial/custom value — not a whole-month
    /// multiple of that student's monthly rate. Stored on each transaction and shown in history.
    /// </summary>
    public string? Note { get; set; }
}

public class SubmitCollectionItem
{
    public long StudentId { get; set; }
    public decimal Amount { get; set; }
}

public class SubmitCollectionResponse
{
    public int SubmittedCount { get; set; }
    public decimal TotalCollected { get; set; }
    public List<SubmitCollectionResultDto> Results { get; set; } = new();
}

public class SubmitCollectionResultDto
{
    public string StudentId { get; set; } = string.Empty;
    /// <summary>committed | failed</summary>
    public string Status { get; set; } = "failed";
    public string? Reason { get; set; }
}

// ── Money: forgive balance (TEACHER-ONLY; assistants 403) ──────────────────

/// <summary>
/// Request to FORGIVE (waive) part of a student's outstanding balance. Forgiving is NOT cash: no
/// wallet change, no transaction, no collector attribution — it only reduces what the student owes,
/// applied oldest-unpaid-month first (cascade). <c>amount</c> must be &gt; 0 and ≤ the student's
/// outstanding through the current teacher-local month.
/// </summary>
public class ForgiveBalanceRequest
{
    public long TeacherStudentId { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Optional free-text reason recorded on the audit row.</summary>
    public string? Note { get; set; }
}

/// <summary>Optional body for reversing a forgiveness (restores the waived balance).</summary>
public class ReverseForgivenessRequest
{
    /// <summary>Optional note recorded on the reversal audit.</summary>
    public string? Note { get; set; }
}

/// <summary>Audit view of a single forgiveness (ids as strings, per the api/v1 contract).</summary>
public class ForgivenessDto
{
    public string Id { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    /// <summary>Display name of the tutor who forgave.</summary>
    public string? ByName { get; set; }
    /// <summary>User id of the tutor who forgave.</summary>
    public string? ByUserId { get; set; }
    public DateTime Date { get; set; }
    /// <summary>active | reversed</summary>
    public string Status { get; set; } = "active";
}

/// <summary>The student's payment summary AFTER a forgive/reverse — the row the frontend re-renders.</summary>
public class ForgiveStudentSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? StudentCode { get; set; }
    public string? SessionName { get; set; }
    /// <summary>Arrears through the current teacher-local month AFTER the forgive/reverse.</summary>
    public decimal Outstanding { get; set; }
    /// <summary>Number of unpaid months through the current month AFTER the forgive/reverse.</summary>
    public int MonthsOwed { get; set; }
    /// <summary>paid | unpaid — whether any outstanding remains through the current month.</summary>
    public string Status { get; set; } = "paid";
}

/// <summary>Response of forgive / reverse — the created (or reversed) forgiveness + updated summary row.</summary>
public class ForgiveBalanceResponse
{
    public ForgivenessDto Forgiveness { get; set; } = new();
    public ForgiveStudentSummaryDto Student { get; set; } = new();
}

// ── Money: assistant wallet withdraw (AssistantWallet screen) ──────────────

public class WalletWithdrawRequest
{
    /// <summary>Defaults to the full current balance when omitted.</summary>
    public decimal? Amount { get; set; }
}

/// <summary>Frontend response for a wallet withdrawal (ids as strings).</summary>
public class WalletWithdrawResponse
{
    public string WithdrawalId { get; set; } = string.Empty;
    /// <summary>Always "completed" — withdrawal is an instant cash handover (no pending state).</summary>
    public string Status { get; set; } = "completed";
    public decimal Amount { get; set; }
    public decimal WalletBalanceAfter { get; set; }
    public DateTime RequestedAt { get; set; }
}

/// <summary>Service-level result of a wallet withdrawal (from IPaymentService).</summary>
public class WalletWithdrawalResult
{
    public long WithdrawalId { get; set; }
    public decimal Amount { get; set; }
    public decimal WalletBalanceAfter { get; set; }
    public DateTime RequestedAt { get; set; }
}
