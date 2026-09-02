using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParentPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ParentPortalEnabled",
                table: "TeacherConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TeacherStudents_Id_TeacherId",
                table: "TeacherStudents",
                columns: new[] { "Id", "TeacherId" });

            migrationBuilder.CreateTable(
                name: "ParentPortalAccesses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: false),
                    DeviceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ClaimedPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AutoApproved = table.Column<bool>(type: "bit", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RespondedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RequestIpHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentPortalAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentPortalAccesses_TeacherStudents_TeacherStudentId_TeacherId",
                        columns: x => new { x.TeacherStudentId, x.TeacherId },
                        principalTable: "TeacherStudents",
                        principalColumns: new[] { "Id", "TeacherId" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParentPortalAccesses_TeacherStudentId_TeacherId",
                table: "ParentPortalAccesses",
                columns: new[] { "TeacherStudentId", "TeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_PPA_DeviceHash",
                table: "ParentPortalAccesses",
                column: "DeviceHash");

            migrationBuilder.CreateIndex(
                name: "IX_PPA_TeacherId_Status_RequestedAt",
                table: "ParentPortalAccesses",
                columns: new[] { "TeacherId", "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_PPA_Student_Device_Live",
                table: "ParentPortalAccesses",
                columns: new[] { "TeacherStudentId", "DeviceHash" },
                unique: true,
                filter: "[Status] IN (1, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParentPortalAccesses");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TeacherStudents_Id_TeacherId",
                table: "TeacherStudents");

            migrationBuilder.DropColumn(
                name: "ParentPortalEnabled",
                table: "TeacherConfigurations");
        }
    }
}
