using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMovedFromSessionToPaymentPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MovedFromSessionId",
                table: "PaymentPeriods",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MovedFromSessionName",
                table: "PaymentPeriods",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MovedFromSessionId",
                table: "PaymentPeriods");

            migrationBuilder.DropColumn(
                name: "MovedFromSessionName",
                table: "PaymentPeriods");
        }
    }
}
