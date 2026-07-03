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
    public List<CollectionRow> Items { get; set; } = new();
}

/// <summary>One row in the collected-payments ledger.</summary>
public class CollectionRow
{
    public string Id { get; set; } = string.Empty;
    public int Index { get; set; }
    public string? StudentId { get; set; }
    public string? StudentName { get; set; }
    public decimal Amount { get; set; }
    /// <summary>collected | pending. A recorded transaction is always a collection.</summary>
    public string Status { get; set; } = "collected";
    public string? SessionName { get; set; }
    public DateTime? CollectedAt { get; set; }
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
    public decimal TotalCashCollected { get; set; }
    public decimal WalletBalance { get; set; }
    public int CollectionsCount { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

public class AssistantWalletCollectionsDto
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
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
    public string? AvatarUrl { get; set; }
    public decimal Amount { get; set; }
    /// <summary>assigned | unassigned</summary>
    public string Assignment { get; set; } = "unassigned";
    /// <summary>paid | unpaid</summary>
    public string Status { get; set; } = "paid";
    public int UnpaidMonths { get; set; }
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
}
