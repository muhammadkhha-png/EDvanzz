using System.Text;

namespace Edvanz.Domain.Helpers;

/// <summary>
/// Folds Arabic orthographic variants so IN-MEMORY search treats them as equal —
/// a user typing "اسامه" must match "أسامة" (and vice versa). Applied to BOTH the
/// search term and the searched field before Contains.
///
/// Folding rules (Egyptian Arabic search conventions):
///   - Alef variants  أ / إ / آ / ٱ            → ا
///   - Taa marbuta    ة                        → ه
///   - Alef maqsura   ى  and hamza-on-yaa  ئ   → ي
///   - Hamza-on-waw   ؤ                        → و
///   - Tatweel        ـ  (U+0640)              → removed
///   - Diacritics     U+064B–U+065F, U+0670    → removed (tashkeel never affects identity)
///   - Arabic-Indic digits ٠–٩ / ۰–۹           → 0–9 (phone typed on an Arabic keyboard)
///   - Everything else                          → lower-cased (Latin case-insensitivity)
///
/// ONLY for in-memory comparisons — this cannot be translated by EF into SQL, so never
/// call it inside an IQueryable expression tree.
/// </summary>
public static class ArabicTextNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            switch (ch)
            {
                case 'أ':
                case 'إ':
                case 'آ':
                case 'ٱ':
                    sb.Append('ا');
                    break;
                case 'ة':
                    sb.Append('ه');
                    break;
                case 'ى':
                case 'ئ':
                    sb.Append('ي');
                    break;
                case 'ؤ':
                    sb.Append('و');
                    break;
                case 'ـ': // tatweel — purely decorative
                    break;
                case >= 'ً' and <= 'ٟ': // tashkeel (fatha, damma, kasra, shadda, sukun, …)
                case 'ٰ':                    // superscript alef
                    break;
                case >= '٠' and <= '٩': // Arabic-Indic digits ٠–٩
                    sb.Append((char)('0' + (ch - '٠')));
                    break;
                case >= '۰' and <= '۹': // Extended Arabic-Indic digits ۰–۹
                    sb.Append((char)('0' + (ch - '۰')));
                    break;
                default:
                    sb.Append(char.ToLowerInvariant(ch));
                    break;
            }
        }
        return sb.ToString();
    }
}
