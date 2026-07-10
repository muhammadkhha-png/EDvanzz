using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_PP_Status_PeriodStart_Index_Catchup : Migration
    {
        // CATCH-UP migration. IX_PP_TeacherId_Status_PeriodStart is declared in the model but was
        // never applied to prod (schema drift found by the model-vs-prod audit). The model snapshot
        // already contains it, so EF generates no diff — hence this hand-authored CreateIndex.
        // It backs GetStudentsByPaymentStatusPagedAsync (students-by-payment-status screen).

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PP_TeacherId_Status_PeriodStart",
                table: "PaymentPeriods",
                columns: new[] { "TeacherId", "PaymentStatus", "PeriodStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PP_TeacherId_Status_PeriodStart",
                table: "PaymentPeriods");
        }
    }
}
