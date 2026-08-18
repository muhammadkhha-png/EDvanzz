using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProrationAnchorMonth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProrationAnchorMonth",
                table: "PaymentPeriods",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Backfill the anchor flag for EXISTING data so the settings-reconcile + first-attendance
            // re-price work on students who predate this column: each student's earliest MONTHLY,
            // non-carried-forward period is their enrollment's first month (the proration anchor).
            // Wrapped in EXEC(N'...') so name resolution is deferred to run time — the column was added
            // in this same batch, and a bare UPDATE would fail batch-compile with "invalid column name"
            // (BUG-10). PeriodType 1 = Monthly.
            migrationBuilder.Sql(@"EXEC(N'
;WITH firstMonthly AS (
    SELECT p.Id, ROW_NUMBER() OVER (PARTITION BY p.TeacherStudentId ORDER BY p.PeriodSequence, p.Id) AS rn
    FROM PaymentPeriods p
    WHERE p.PeriodType = 1 AND p.IsCarriedForward = 0 AND p.TeacherStudentId IS NOT NULL
)
UPDATE pp SET IsProrationAnchorMonth = 1
FROM PaymentPeriods pp
INNER JOIN firstMonthly fm ON fm.Id = pp.Id
WHERE fm.rn = 1;
')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsProrationAnchorMonth",
                table: "PaymentPeriods");
        }
    }
}
