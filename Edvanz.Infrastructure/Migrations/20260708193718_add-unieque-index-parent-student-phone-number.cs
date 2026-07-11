using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class adduniequeindexparentstudentphonenumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TeacherStudents_ParentPhoneNumber",
                table: "TeacherStudents",
                column: "ParentPhoneNumber",
                unique: true,
                filter: "[ParentPhoneNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherStudents_StudentPhoneNumber",
                table: "TeacherStudents",
                column: "StudentPhoneNumber",
                unique: true,
                filter: "[StudentPhoneNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PP_TeacherId_Status_PeriodStart",
                table: "PaymentPeriods",
                columns: new[] { "TeacherId", "PaymentStatus", "PeriodStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeacherStudents_ParentPhoneNumber",
                table: "TeacherStudents");

            migrationBuilder.DropIndex(
                name: "IX_TeacherStudents_StudentPhoneNumber",
                table: "TeacherStudents");

            migrationBuilder.DropIndex(
                name: "IX_PP_TeacherId_Status_PeriodStart",
                table: "PaymentPeriods");
        }
    }
}
