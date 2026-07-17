using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentTransactionAllocationLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentTransactionAllocations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentTransactionId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentPeriodId = table.Column<long>(type: "bigint", nullable: true),
                    AmountApplied = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactionAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactionAllocations_PaymentPeriods_PaymentPeriodId",
                        column: x => x.PaymentPeriodId,
                        principalTable: "PaymentPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentTransactionAllocations_PaymentTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentTransactionAllocations_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactionAllocations_TeacherId",
                table: "PaymentTransactionAllocations",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_PTA_PaymentPeriodId",
                table: "PaymentTransactionAllocations",
                column: "PaymentPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_PTA_PaymentTransactionId",
                table: "PaymentTransactionAllocations",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "UX_PTA_Transaction_Period",
                table: "PaymentTransactionAllocations",
                columns: new[] { "PaymentTransactionId", "PaymentPeriodId" },
                unique: true,
                filter: "[PaymentPeriodId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentTransactionAllocations");
        }
    }
}
