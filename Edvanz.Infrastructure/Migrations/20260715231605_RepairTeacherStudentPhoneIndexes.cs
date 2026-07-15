using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <summary>
    /// Repairs the TeacherStudent phone-index lineage. The two 2026-07-08 migrations
    /// (add-unieque-index-parent-student-phone-number / update indexes) never applied on
    /// production: the first created a GLOBAL unique index over ParentPhoneNumber that
    /// included soft-deleted rows and failed on an existing duplicate; the second then
    /// failed dropping the index the first never created. Both files were deleted
    /// (BUG-9 precedent — EF ignores their orphan __EFMigrationsHistory rows on dev DBs).
    ///
    /// Every step here is defensive (IF EXISTS / IF NOT EXISTS) so the same script
    /// converges production (neither index), dev databases (both old indexes recorded)
    /// and fresh databases. All referenced tables/columns pre-date this migration, so
    /// bare Sql() is safe here (no BUG-10 same-batch column binding).
    ///
    /// Business rule: ParentPhoneNumber is NOT unique — one parent legitimately has
    /// several children on the same roster. StudentPhoneNumber stays unique per teacher
    /// among active rows.
    /// </summary>
    public partial class RepairTeacherStudentPhoneIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop every legacy variant that may exist in any environment: the global
            // unique pair from the deleted 20260708193718 and the per-teacher UNIQUE
            // parent index from the deleted 20260708220307.
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_TeacherStudents_ParentPhoneNumber] ON [dbo].[TeacherStudents];");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_TeacherStudents_StudentPhoneNumber] ON [dbo].[TeacherStudents];");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_TeacherStudents_TeacherId_ParentPhoneNumber] ON [dbo].[TeacherStudents];");

            // Parent phone: non-unique lookup index (messaging/search only).
            migrationBuilder.Sql(@"
CREATE INDEX [IX_TeacherStudents_TeacherId_ParentPhoneNumber]
    ON [dbo].[TeacherStudents] ([TeacherId], [ParentPhoneNumber]);");

            // Student phone: unique per teacher among active rows. Production ran without
            // this index since 2026-07-08, so active duplicates may exist; keep the phone
            // on the earliest row of each (TeacherId, StudentPhoneNumber) group and NULL
            // the later ones (phones are optional; the roster screen flags them as
            // incomplete for re-entry). Runs only where the index is missing — where it
            // already exists (dev) no duplicates can exist.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_TeacherStudents_TeacherId_StudentPhoneNumber'
                 AND object_id = OBJECT_ID(N'[dbo].[TeacherStudents]'))
BEGIN
    ;WITH Dups AS (
        SELECT Id,
               ROW_NUMBER() OVER (PARTITION BY TeacherId, StudentPhoneNumber ORDER BY Id) AS rn
        FROM [dbo].[TeacherStudents]
        WHERE StudentPhoneNumber IS NOT NULL AND IsDeleted = 0
    )
    UPDATE ts SET ts.StudentPhoneNumber = NULL
    FROM [dbo].[TeacherStudents] ts
    INNER JOIN Dups d ON d.Id = ts.Id
    WHERE d.rn > 1;
    PRINT CONCAT('RepairTeacherStudentPhoneIndexes: nulled ', @@ROWCOUNT, ' duplicate active StudentPhoneNumber value(s).');

    CREATE UNIQUE INDEX [IX_TeacherStudents_TeacherId_StudentPhoneNumber]
        ON [dbo].[TeacherStudents] ([TeacherId], [StudentPhoneNumber])
        WHERE [StudentPhoneNumber] IS NOT NULL AND [IsDeleted] = 0;
END;");

            // PaymentPeriods status/period index was only created by the deleted
            // 20260708193718, so production never received it (see §7.4 catch-up scans).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_PP_TeacherId_Status_PeriodStart'
                 AND object_id = OBJECT_ID(N'[dbo].[PaymentPeriods]'))
BEGIN
    CREATE INDEX [IX_PP_TeacherId_Status_PeriodStart]
        ON [dbo].[PaymentPeriods] ([TeacherId], [PaymentStatus], [PeriodStart]);
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert only the parent index to its previous (unique) shape. May fail on
            // data where siblings legitimately share a parent phone — expected: the
            // unique constraint is what this migration removes on purpose.
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_TeacherStudents_TeacherId_ParentPhoneNumber] ON [dbo].[TeacherStudents];");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherStudents_TeacherId_ParentPhoneNumber",
                table: "TeacherStudents",
                columns: new[] { "TeacherId", "ParentPhoneNumber" },
                unique: true,
                filter: "[ParentPhoneNumber] IS NOT NULL AND [IsDeleted] = 0");
        }
    }
}
