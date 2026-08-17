namespace Edvanz.Application.Dtos.Payment;

// ════════════════════════════════════════════════════════════════════════════
// ADMIN ONE-OFF: RESET-AWARE RECOMPUTE OF AN ASSISTANT WALLET'S CurrentBalance
// ════════════════════════════════════════════════════════════════════════════
//
// Output of PaymentService.RecomputeAssistantWalletAsync. dryRun=true writes nothing and only
// reports old vs new. Repairs a balance corrupted by pre-reset reversals (salma −2700 → 0):
// held cash is reconstructed from events AFTER the last full cash hand-over, so refunds of cash
// already handed to the tutor no longer drive the balance negative.

/// <summary>Report for the recompute-assistant-wallet admin op.</summary>
public sealed class RecomputeAssistantWalletReport
{
    /// <summary>True when the run only PREVIEWED (wrote nothing).</summary>
    public bool DryRun { get; set; }

    public long TeacherId { get; set; }
    public long AssistantId { get; set; }
    public long AssistantUserId { get; set; }
    public string? AssistantName { get; set; }

    /// <summary>The wallet's stored CurrentBalance before the recompute.</summary>
    public decimal OldBalance { get; set; }
    /// <summary>The reset-aware recomputed balance (what CurrentBalance becomes on apply).</summary>
    public decimal NewBalance { get; set; }
    /// <summary>NewBalance − OldBalance (the correction; e.g. +2700 for salma).</summary>
    public decimal Delta { get; set; }

    // ── Components (so the number is auditable) ──
    /// <summary>Instant of the last full cash hand-over the held balance was reconstructed from; null = never handed over (all-time).</summary>
    public System.DateTime? AnchorHandoverAt { get; set; }
    /// <summary>Sum of non-deleted collections taken after the anchor.</summary>
    public decimal PostHandoverCollections { get; set; }
    /// <summary>Number of those collections.</summary>
    public int PostHandoverCollectionCount { get; set; }
    /// <summary>Sum of partial (departure) reversals of those post-anchor collections.</summary>
    public decimal PostHandoverReversals { get; set; }
    /// <summary>Sum of partial withdrawals recorded after the anchor.</summary>
    public decimal PostHandoverWithdrawals { get; set; }
}
