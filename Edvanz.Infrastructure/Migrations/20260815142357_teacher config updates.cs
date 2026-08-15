using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class teacherconfigupdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowAttendanceHistoryOnAttendanceScreen",
                table: "TeacherConfigurations",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowPaymentInfoOnAttendanceScreen",
                table: "TeacherConfigurations",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowAttendanceHistoryOnAttendanceScreen",
                table: "TeacherConfigurations");

            migrationBuilder.DropColumn(
                name: "ShowPaymentInfoOnAttendanceScreen",
                table: "TeacherConfigurations");
        }
    }
}
