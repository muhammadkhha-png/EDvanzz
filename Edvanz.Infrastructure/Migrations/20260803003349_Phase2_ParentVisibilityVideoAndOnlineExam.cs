using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_ParentVisibilityVideoAndOnlineExam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ParentVisibilityOnlineExamDefault",
                table: "TeacherConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ParentVisibilityVideo",
                table: "TeacherConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentVisibilityOnlineExamDefault",
                table: "TeacherConfigurations");

            migrationBuilder.DropColumn(
                name: "ParentVisibilityVideo",
                table: "TeacherConfigurations");
        }
    }
}
