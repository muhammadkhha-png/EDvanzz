using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSubjectFromOnlineExams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnlineExams_TeacherSubjects_TeacherSubjectId",
                table: "OnlineExams");

            migrationBuilder.DropIndex(
                name: "IX_OnlineExams_TeacherSubjectId",
                table: "OnlineExams");

            migrationBuilder.DropColumn(
                name: "TeacherSubjectId",
                table: "OnlineExams");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TeacherSubjectId",
                table: "OnlineExams",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_OnlineExams_TeacherSubjectId",
                table: "OnlineExams",
                column: "TeacherSubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_OnlineExams_TeacherSubjects_TeacherSubjectId",
                table: "OnlineExams",
                column: "TeacherSubjectId",
                principalTable: "TeacherSubjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
