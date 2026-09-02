using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProrationMethodAndManualFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProrationMethod",
                table: "TeacherConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsProrationManual",
                table: "PaymentPeriods",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "PaymentPeriodId",
                table: "PaymentEditLogs",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PEL_PaymentPeriodId",
                table: "PaymentEditLogs",
                column: "PaymentPeriodId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PEL_PaymentPeriodId",
                table: "PaymentEditLogs");

            migrationBuilder.DropColumn(
                name: "ProrationMethod",
                table: "TeacherConfigurations");

            migrationBuilder.DropColumn(
                name: "IsProrationManual",
                table: "PaymentPeriods");

            migrationBuilder.DropColumn(
                name: "PaymentPeriodId",
                table: "PaymentEditLogs");
        }
    }
}
