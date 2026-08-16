using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SetAttendanceEnrichmentDefaultsTrue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data-only backfill: the two attendance-screen enrichment toggles now default ON
            // (ShowPaymentInfoOnAttendanceScreen / ShowAttendanceHistoryOnAttendanceScreen).
            // Existing teacher rows created before this change carry NULL in these columns; turn
            // them on so the feature is enabled for everyone by default. The columns already exist
            // from a prior migration, so a bare Sql() UPDATE is safe here (this is NOT the
            // same-migration-as-AddColumn case that requires EXEC() deferral — see BUG-10).
            migrationBuilder.Sql("UPDATE [TeacherConfigurations] SET [ShowPaymentInfoOnAttendanceScreen] = 1 WHERE [ShowPaymentInfoOnAttendanceScreen] IS NULL;");
            migrationBuilder.Sql("UPDATE [TeacherConfigurations] SET [ShowAttendanceHistoryOnAttendanceScreen] = 1 WHERE [ShowAttendanceHistoryOnAttendanceScreen] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentional no-op: the backfill cannot be distinguished from teacher-chosen values
            // after the fact, so the flip is not reverted.
        }
    }
}
