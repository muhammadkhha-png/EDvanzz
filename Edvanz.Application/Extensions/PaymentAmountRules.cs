using System;

namespace Edvanz.Application.Extensions;

/// <summary>
/// Shared money-amount rules for the payment module. Single-sourced so the "does this amount need a
/// note?" decision is IDENTICAL across the collect (Feature C) and edit (Feature B) flows.
/// </summary>
public static class PaymentAmountRules
{
    /// <summary>
    /// True when <paramref name="amount"/> is a WHOLE-MONTH multiple of <paramref name="monthlyRate"/>
    /// — i.e. <c>amount == N × monthlyRate</c> for some integer N ≥ 1. Such an amount is a plain
    /// "pay N months" and needs no explanatory note. Anything else — a partial/custom amount, 0, or an
    /// unknown/zero monthly rate — returns false (a note is required). Uses a small epsilon so decimal
    /// rounding (e.g. 300.00 / 100.00) does not misclassify an exact multiple.
    /// </summary>
    public static bool IsWholeMonthMultiple(decimal amount, decimal monthlyRate)
    {
        if (monthlyRate <= 0m || amount <= 0m) return false;
        decimal ratio = amount / monthlyRate;
        decimal rounded = Math.Round(ratio);
        return rounded >= 1m && Math.Abs(ratio - rounded) < 0.0001m;
    }
}
