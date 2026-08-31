using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Persistence;

/// <summary>
/// EF Core-translatable SQL search helpers. These methods have NO C# body worth running —
/// they are mapped to database functions in <c>OnModelCreating</c> and are only ever used
/// inside an <see cref="IQueryable"/> expression tree (EF translates the call to SQL).
/// </summary>
public static class DbSearch
{
    /// <summary>
    /// Folds Arabic orthographic variants so a SQL substring search treats them as equal —
    /// the exact SQL mirror of <see cref="Edvanz.Domain.Helpers.ArabicTextNormalizer"/>:
    /// أ/إ/آ/ٱ→ا, ة→ه, ى/ئ→ي, ؤ→و, strips tatweel + tashkeel, maps Arabic-Indic digits→ASCII,
    /// and lower-cases. Backed by the scalar UDF <c>dbo.ArabicNormalize</c>
    /// (migration <c>AddArabicNormalizeFunction</c>). Apply it to BOTH the searched column and
    /// the (already C#-normalized) search term:
    /// <code>DbSearch.ArabicNormalize(ts.StudentName).Contains(term)</code>
    /// Only valid inside an EF query — never call it from ordinary C#.
    /// </summary>
    public static string ArabicNormalize(string? input)
        => throw new global::System.NotSupportedException(
            "DbSearch.ArabicNormalize is EF-only; it is translated to dbo.ArabicNormalize inside a query.");
}
