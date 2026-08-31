using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArabicNormalizeFunction : Migration
    {
        // Scalar UDF that folds Arabic orthographic variants for substring search — the exact
        // SQL mirror of Edvanz.Domain.Helpers.ArabicTextNormalizer: أ/إ/آ/ٱ→ا, ة→ه, ى/ئ→ي, ؤ→و,
        // strips tatweel U+0640 + tashkeel U+064B–U+065F + superscript-alef U+0670, maps
        // Arabic-Indic U+0660–0669 and extended-Persian U+06F0–06F9 digits → ASCII, then LOWER().
        // Mapped in EdvanzDbContext.OnModelCreating via HasDbFunction; called from repos as
        // DbSearch.ArabicNormalize(col).Contains(term). DDL only — no data touched.
        //
        // Design notes (all load-bearing — verified against SQL Server 2022 / Azure SQL engine):
        //  • COLLATE Latin1_General_100_BIN2 on the innermost @s forces exact UTF-16 code-point
        //    matching and propagates through the nested REPLACEs. REQUIRED: under the default
        //    CI_AS collation the tatweel (and other zero-collation-weight marks) are "ignorable"
        //    and REPLACE silently fails to strip them.
        //  • Written as ONE nested expression with a SINGLE RETURN and no parameter reassignment,
        //    so SQL Server can INLINE the scalar UDF (sys.sql_modules.is_inlineable = 1) → the fold
        //    is folded into the query plan instead of a per-row call (keeps search performant).
        //    A NULL input flows through REPLACE/LOWER as NULL naturally, so no IF guard is needed.
        //  • Wrapped in EXEC(N'…'): the idempotent deploy script wraps each migration in
        //    IF NOT EXISTS(...) BEGIN … END and CREATE FUNCTION must be first in its batch — EXEC
        //    defers compile to run time (same safeguard as the BUG-10 fix). NCHAR(0x…) keeps the
        //    file ASCII (no literal-Arabic encoding risk). CREATE OR ALTER is re-run-safe.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"EXEC(N'
CREATE OR ALTER FUNCTION dbo.ArabicNormalize(@s nvarchar(4000))
RETURNS nvarchar(4000)
WITH SCHEMABINDING
AS
BEGIN
    RETURN LOWER(
        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@s COLLATE Latin1_General_100_BIN2,
            NCHAR(0x0623), NCHAR(0x0627)),
            NCHAR(0x0625), NCHAR(0x0627)),
            NCHAR(0x0622), NCHAR(0x0627)),
            NCHAR(0x0671), NCHAR(0x0627)),
            NCHAR(0x0629), NCHAR(0x0647)),
            NCHAR(0x0649), NCHAR(0x064A)),
            NCHAR(0x0626), NCHAR(0x064A)),
            NCHAR(0x0624), NCHAR(0x0648)),
            NCHAR(0x0640), N''''),
            NCHAR(0x064B), N''''),
            NCHAR(0x064C), N''''),
            NCHAR(0x064D), N''''),
            NCHAR(0x064E), N''''),
            NCHAR(0x064F), N''''),
            NCHAR(0x0650), N''''),
            NCHAR(0x0651), N''''),
            NCHAR(0x0652), N''''),
            NCHAR(0x0653), N''''),
            NCHAR(0x0654), N''''),
            NCHAR(0x0655), N''''),
            NCHAR(0x0656), N''''),
            NCHAR(0x0657), N''''),
            NCHAR(0x0658), N''''),
            NCHAR(0x0659), N''''),
            NCHAR(0x065A), N''''),
            NCHAR(0x065B), N''''),
            NCHAR(0x065C), N''''),
            NCHAR(0x065D), N''''),
            NCHAR(0x065E), N''''),
            NCHAR(0x065F), N''''),
            NCHAR(0x0670), N''''),
            NCHAR(0x0660), NCHAR(48)),
            NCHAR(0x0661), NCHAR(49)),
            NCHAR(0x0662), NCHAR(50)),
            NCHAR(0x0663), NCHAR(51)),
            NCHAR(0x0664), NCHAR(52)),
            NCHAR(0x0665), NCHAR(53)),
            NCHAR(0x0666), NCHAR(54)),
            NCHAR(0x0667), NCHAR(55)),
            NCHAR(0x0668), NCHAR(56)),
            NCHAR(0x0669), NCHAR(57)),
            NCHAR(0x06F0), NCHAR(48)),
            NCHAR(0x06F1), NCHAR(49)),
            NCHAR(0x06F2), NCHAR(50)),
            NCHAR(0x06F3), NCHAR(51)),
            NCHAR(0x06F4), NCHAR(52)),
            NCHAR(0x06F5), NCHAR(53)),
            NCHAR(0x06F6), NCHAR(54)),
            NCHAR(0x06F7), NCHAR(55)),
            NCHAR(0x06F8), NCHAR(56)),
            NCHAR(0x06F9), NCHAR(57))
        );
END
');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.ArabicNormalize;");
        }
    }
}
