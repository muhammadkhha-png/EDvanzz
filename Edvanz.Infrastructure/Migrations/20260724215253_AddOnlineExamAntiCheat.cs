using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineExamAntiCheat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ViolationCount",
                table: "StudentOnlineExamReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "BlockOnViolation",
                table: "OnlineExams",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxViolations",
                table: "OnlineExams",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddCheckConstraint(
                name: "CK_OnlineExams_MaxViolationsRange",
                table: "OnlineExams",
                sql: "[MaxViolations] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OnlineExams_MaxViolationsRange",
                table: "OnlineExams");

            migrationBuilder.DropColumn(
                name: "ViolationCount",
                table: "StudentOnlineExamReports");

            migrationBuilder.DropColumn(
                name: "BlockOnViolation",
                table: "OnlineExams");

            migrationBuilder.DropColumn(
                name: "MaxViolations",
                table: "OnlineExams");
        }
    }
}
