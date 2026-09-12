using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;

namespace Edvanz.Application.Services;

/// <summary>
/// Parses the string a card sends back when someone clicks it.
///
/// Most keys are just an <see cref="AdminSegmentKey"/> name. Two families carry an argument after a
/// colon because they are one card repeated — per feature (<c>ModuleUsing:Videos</c>) and per month
/// (<c>Renewed:2026-08</c>). Encoding the argument in the key keeps ONE drill-down endpoint behind
/// every number on the page; a separate route per card is how a console ends up with forty
/// endpoints that each drift on their own schedule.
/// </summary>
public static class AdminSegmentKeyParser
{
    /// <summary>A parsed key: which segment, plus whichever argument that family takes.</summary>
    /// <param name="Key">The segment.</param>
    /// <param name="Module">Set for the three per-feature families.</param>
    /// <param name="Month">Set for the two per-month families: first day of that month.</param>
    public sealed record Parsed(AdminSegmentKey Key, UsageModules? Module, DateOnly? Month);

    /// <summary>
    /// Returns null when the key names no segment, or when a parameterised family is missing or
    /// carries an unreadable argument. Callers answer 400 — a silently-empty list would read as
    /// "nobody is in this card", which is a different and much more expensive answer.
    /// </summary>
    public static Parsed? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        string value = raw.Trim();
        int colon = value.IndexOf(':');
        string name = colon < 0 ? value : value[..colon];
        string? argument = colon < 0 ? null : value[(colon + 1)..];

        if (!Enum.TryParse<AdminSegmentKey>(name, ignoreCase: true, out var key)) return null;

        bool needsModule = key is AdminSegmentKey.ModuleUsing
                               or AdminSegmentKey.ModuleNeverOpened
                               or AdminSegmentKey.ModuleHaveIt;
        bool needsMonth = key is AdminSegmentKey.Renewed or AdminSegmentKey.NotRenewed;

        if (needsModule)
        {
            if (!Enum.TryParse<UsageModules>(argument, ignoreCase: true, out var module)
                || module == UsageModules.None) return null;
            return new Parsed(key, module, null);
        }

        if (needsMonth)
        {
            // "2026-08" — the same string the renewals response hands back, so a click can never
            // send a month the server did not itself produce.
            if (argument is null
                || !DateOnly.TryParseExact(argument + "-01", "yyyy-MM-dd", out var month)) return null;
            return new Parsed(key, null, month);
        }

        // An argument on a family that takes none is a typo, not a filter — refuse it rather than
        // ignore it, or a mistyped key quietly returns the unfiltered card.
        return argument is null ? new Parsed(key, null, null) : null;
    }
}
