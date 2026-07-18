using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DefaultOfflineExamVisibilityTrue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Product decision (2026-07-18): offline exams are now visible-by-default on the
            // student side. Backfill existing teacher configs whose flag is still the old default
            // (false) so the offline-exam section shows without each teacher re-saving config.
            // The column already exists (init migration), so this bare UPDATE is safe.
            migrationBuilder.Sql(
                "UPDATE [TeacherConfigurations] SET [StudentVisibilityExamDefault] = 1 WHERE [StudentVisibilityExamDefault] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way data backfill: originally-false rows can't be distinguished from
            // deliberately-false ones after the fact, so Down intentionally does nothing.
        }
    }
}
