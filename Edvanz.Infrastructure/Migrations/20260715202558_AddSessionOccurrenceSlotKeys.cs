using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionOccurrenceSlotKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DayPositionIndex",
                table: "SessionOccurrences",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "WeekStartDate",
                table: "SessionOccurrences",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(2000, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Backfill the cross-session equivalence slot key, mirroring
            // OccurrenceGeneratorService.ComputeOccurrenceKeys, BEFORE the unique index is created
            // (otherwise every existing row shares the default (2000-01-01, 1) slot and the index
            // creation would fail). 2000-01-01 is a Saturday, so
            // (((DATEDIFF(DAY,'2000-01-01',OccurrenceDate) % 7) + 7) % 7) is the app day index 0=Sat..6=Fri.
            //   WeekStartDate     = that week's Saturday (Weekly/BiWeekly) or the 1st of the month (Monthly=3)
            //   DayPositionIndex  = 1-based rank of the weekday within the sorted SelectedDays (Monthly=1)
            //
            // CRITICAL — the UPDATE is wrapped in EXEC(N'...') on purpose. EF emits this migration's
            // AddColumn operations and this Sql() call into a SINGLE T-SQL batch (no GO between them),
            // and the idempotent deploy script (dotnet ef migrations script --idempotent, applied by
            // azure/sql-action in deploy.yml) keeps them in one batch too. SQL Server binds column
            // names for the WHOLE batch at compile time, so a bare UPDATE referencing WeekStartDate /
            // DayPositionIndex — columns the ALTER TABLE statements ADD in that same batch — fails to
            // compile with "Invalid column name" (error 207); under the idempotent IF-wrapper that
            // surfaces downstream as a 1505 duplicate-key when the un-backfilled default rows collide
            // on the unique index. EXEC() defers compilation of the inner statement to run time, AFTER
            // the columns exist. Do NOT unwrap this back to a bare migrationBuilder.Sql UPDATE.
            migrationBuilder.Sql(@"
EXEC(N'
UPDATE o
SET
    WeekStartDate = CASE
        WHEN s.OccurrenceType = 3
            THEN DATEFROMPARTS(YEAR(o.OccurrenceDate), MONTH(o.OccurrenceDate), 1)
        ELSE DATEADD(DAY,
            -(((DATEDIFF(DAY, ''2000-01-01'', o.OccurrenceDate) % 7) + 7) % 7),
            o.OccurrenceDate)
    END,
    DayPositionIndex = CASE
        WHEN s.OccurrenceType = 3 THEN 1
        ELSE (
            SELECT COUNT(*)
            FROM STRING_SPLIT(s.SelectedDays, '','') sd
            WHERE TRY_CAST(sd.value AS int)
                  <= (((DATEDIFF(DAY, ''2000-01-01'', o.OccurrenceDate) % 7) + 7) % 7)
        )
    END
FROM SessionOccurrences o
INNER JOIN Sessions s ON s.Id = o.SessionId;');");

            migrationBuilder.CreateIndex(
                name: "IX_SessionOccurrences_SessionId_WeekStartDate_DayPositionIndex",
                table: "SessionOccurrences",
                columns: new[] { "SessionId", "WeekStartDate", "DayPositionIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SessionOccurrences_SessionId_WeekStartDate_DayPositionIndex",
                table: "SessionOccurrences");

            migrationBuilder.DropColumn(
                name: "DayPositionIndex",
                table: "SessionOccurrences");

            migrationBuilder.DropColumn(
                name: "WeekStartDate",
                table: "SessionOccurrences");
        }
    }
}
