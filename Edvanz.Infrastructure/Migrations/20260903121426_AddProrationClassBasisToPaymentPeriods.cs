using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProrationClassBasisToPaymentPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProrationClassesBilled",
                table: "PaymentPeriods",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProrationClassesTotal",
                table: "PaymentPeriods",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProrationClassesBilled",
                table: "PaymentPeriods");

            migrationBuilder.DropColumn(
                name: "ProrationClassesTotal",
                table: "PaymentPeriods");
        }
    }
}
