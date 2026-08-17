using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniquePaymentPeriodPerStudentSessionMonth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_PP_Student_Session_Month_Monthly",
                table: "PaymentPeriods",
                columns: new[] { "TeacherStudentId", "SessionId", "PeriodStart" },
                unique: true,
                filter: "[TeacherStudentId] IS NOT NULL AND [SessionId] IS NOT NULL AND [PeriodType] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_PP_Student_Session_Month_Monthly",
                table: "PaymentPeriods");
        }
    }
}
