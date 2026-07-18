using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentVisibilityVideo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default TRUE: videos are visible unless the teacher opts out. Backfills existing
            // TeacherConfiguration rows to visible so the new toggle never hides videos on upgrade.
            migrationBuilder.AddColumn<bool>(
                name: "StudentVisibilityVideo",
                table: "TeacherConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StudentVisibilityVideo",
                table: "TeacherConfigurations");
        }
    }
}
