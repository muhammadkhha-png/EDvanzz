using System.Text;
using System.Text.RegularExpressions;

namespace Edvanz.Application.Common;

/// <summary>
/// The single source of truth for Egyptian mobile numbers.
///
/// <para><b>Validation</b> was EXTRACTED VERBATIM from the private
/// <c>UserService.PhoneNumberValidator</c> (11 digits, <c>01[0125]</c> + 8 digits) — the rule
/// enforced at sign-up and on roster writes. <c>UserService.PhoneNumberValidator</c> now forwards
/// here, so the shipped call sites (AuthService, TeacherStudentService, UserService) keep working
/// against exactly the same regex; behaviour is unchanged.</para>
///
/// <para><b>Normalization</b> is new, added for the parent portal: a parent typing their number on
/// a public web page will use Arabic-Indic digits, spaces, dashes, or the +20 country code.
/// <see cref="Normalize"/> folds all of that to the canonical stored shape (11 digits, leading 0).</para>
///
/// <para><b>Matching</b> against the roster is deliberately split in two, because roster phones are
/// only <c>.Trim()</c>-ed on write and therefore vary in format:
/// <list type="bullet">
///   <item>ONE known row → compare IN MEMORY with <see cref="AreSameNumber"/> (normalize both sides).</item>
///   <item>SEARCHING for rows → pass <see cref="StoredVariants"/> to an <c>IN</c> query, so the
///         index on the raw column is still used (normalizing the column in SQL would not be sargable).</item>
/// </list></para>
/// </summary>
public static class EgyptianPhoneNumber
{
    /// <summary>
    /// Egyptian mobile: 11 digits starting with 010/011/012/015.
    /// Byte-for-byte the pattern that lived in <c>UserService.PhoneNumberValidator</c>.
    /// </summary>
    private static readonly Regex Pattern = new(@"^01[0125]\d{8}$", RegexOptions.Compiled);

    /// <summary>Canonical length of a valid Egyptian mobile number (leading zero included).</summary>
    public const int CanonicalLength = 11;

    /// <summary>
    /// True when <paramref name="phone"/> is ALREADY in the canonical stored shape. Strict by
    /// design: this is the historical sign-up/roster rule and must not start accepting formats it
    /// used to reject. Use <see cref="Normalize"/> first when the input is user-typed.
    /// </summary>
    public static bool IsValidEgyptianMobile(string? phone) =>
        !string.IsNullOrWhiteSpace(phone) && Pattern.IsMatch(phone);

    /// <summary>
    /// Folds a user-typed number to the canonical shape, or returns null when it cannot be one.
    ///
    /// Steps: Arabic-Indic (٠-٩) and Extended Arabic-Indic (۰-۹) digits → ASCII; every non-digit
    /// dropped (spaces, dashes, parentheses, the leading +); the international prefix
    /// <c>0020</c> / <c>20</c> removed; the leading <c>0</c> restored; finally the canonical
    /// 11-digit <c>01[0125]</c> rule applied.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var builder = new StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            char folded = FoldDigit(c);
            if (folded >= '0' && folded <= '9')
                builder.Append(folded);
        }

        string digits = builder.ToString();
        if (digits.Length == 0)
            return null;

        // International prefixes. "+20…" already lost its '+' above, so it arrives as "20…";
        // the length guard stops a local number that legitimately starts with "20" (there is
        // none today, but the guard costs nothing) from being mangled.
        if (digits.StartsWith("0020", StringComparison.Ordinal))
            digits = digits[4..];
        else if (digits.StartsWith("20", StringComparison.Ordinal) && digits.Length >= CanonicalLength + 1)
            digits = digits[2..];

        // A country-code form drops the national trunk zero — restore it.
        if (digits.Length == CanonicalLength - 1 && digits[0] == '1')
            digits = "0" + digits;

        return Pattern.IsMatch(digits) ? digits : null;
    }

    /// <summary>
    /// True when both sides normalize to the SAME canonical number. Null/blank/unparseable on
    /// either side is never a match — a student with no parent phone on file can never be
    /// auto-approved by a parent who typed nothing.
    /// </summary>
    public static bool AreSameNumber(string? left, string? right)
    {
        string? a = Normalize(left);
        if (a is null) return false;

        string? b = Normalize(right);
        return b is not null && string.Equals(a, b, StringComparison.Ordinal);
    }

    /// <summary>
    /// The plausible STORED spellings of one canonical number, for an <c>IN</c> lookup against the
    /// raw <c>ParentPhoneNumber</c> column. Returns an empty list when the input cannot be
    /// normalized, so the caller skips the query entirely.
    /// </summary>
    public static IReadOnlyList<string> StoredVariants(string? raw)
    {
        string? canonical = Normalize(raw);
        if (canonical is null)
            return Array.Empty<string>();

        string withoutTrunkZero = canonical[1..];   // 1012345678

        return new[]
        {
            canonical,                       // 01012345678  (the shape every current write produces)
            withoutTrunkZero,                // 1012345678
            "+20" + withoutTrunkZero,        // +201012345678
            "20" + withoutTrunkZero,         // 201012345678
            "0020" + withoutTrunkZero        // 00201012345678
        };
    }

    // NOTE: a Mask("010•••••678") helper lived here until 2026-09-02. It was removed with its only
    // caller when the teacher endpoints switched to returning the FULL number — a teacher deciding
    // whether to approve a stranger needs to recognize and be able to ring the number.

    /// <summary>Maps Arabic-Indic and Extended Arabic-Indic digit code points onto their ASCII twin; leaves everything else alone.</summary>
    private static char FoldDigit(char c)
    {
        if (c >= '٠' && c <= '٩') return (char)('0' + (c - '٠')); // ٠..٩
        if (c >= '۰' && c <= '۹') return (char)('0' + (c - '۰')); // ۰..۹
        return c;
    }
}
