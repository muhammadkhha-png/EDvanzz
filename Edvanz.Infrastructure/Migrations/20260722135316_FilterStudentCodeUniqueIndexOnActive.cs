using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FilterStudentCodeUniqueIndexOnActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeacherStudents_TeacherId_StudentCode",
                table: "TeacherStudents");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherStudents_TeacherId_StudentCode",
                table: "TeacherStudents",
                columns: new[] { "TeacherId", "StudentCode" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeacherStudents_TeacherId_StudentCode",
                table: "TeacherStudents");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherStudents_TeacherId_StudentCode",
                table: "TeacherStudents",
                columns: new[] { "TeacherId", "StudentCode" },
                unique: true);
        }
    }
}
