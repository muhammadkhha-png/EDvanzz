using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientEntryIdToPaymentTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientEntryId",
                table: "PaymentTransactions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PT_TeacherId_ClientEntryId",
                table: "PaymentTransactions",
                columns: new[] { "TeacherId", "ClientEntryId" },
                unique: true,
                filter: "[ClientEntryId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PT_TeacherId_ClientEntryId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ClientEntryId",
                table: "PaymentTransactions");
        }
    }
}
