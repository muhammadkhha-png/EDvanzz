using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_TeacherStudent_Phone_UniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TeacherStudents_TeacherId_ParentPhoneNumber",
                table: "TeacherStudents",
                columns: new[] { "TeacherId", "ParentPhoneNumber" },
                unique: true,
                filter: "[ParentPhoneNumber] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherStudents_TeacherId_StudentPhoneNumber",
                table: "TeacherStudents",
                columns: new[] { "TeacherId", "StudentPhoneNumber" },
                unique: true,
                filter: "[StudentPhoneNumber] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeacherStudents_TeacherId_ParentPhoneNumber",
                table: "TeacherStudents");

            migrationBuilder.DropIndex(
                name: "IX_TeacherStudents_TeacherId_StudentPhoneNumber",
                table: "TeacherStudents");
        }
    }
}
