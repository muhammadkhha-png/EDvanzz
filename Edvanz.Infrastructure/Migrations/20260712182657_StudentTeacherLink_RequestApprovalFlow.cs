using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StudentTeacherLink_RequestApprovalFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentTeacherLinks_StudentUserId_TeacherId",
                table: "StudentTeacherLinks");

            migrationBuilder.DropIndex(
                name: "IX_StudentTeacherLinks_TeacherId",
                table: "StudentTeacherLinks");

            migrationBuilder.AddColumn<long>(
                name: "RemovedByUserId",
                table: "StudentTeacherLinks",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedAt",
                table: "StudentTeacherLinks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedStudentCode",
                table: "StudentTeacherLinks",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedStudentName",
                table: "StudentTeacherLinks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RespondedAt",
                table: "StudentTeacherLinks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RespondedByUserId",
                table: "StudentTeacherLinks",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentTeacherLinks_StudentUserId_TeacherId",
                table: "StudentTeacherLinks",
                columns: new[] { "StudentUserId", "TeacherId" },
                unique: true,
                filter: "[LinkStatus] IN (1, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTeacherLinks_TeacherId_LinkStatus",
                table: "StudentTeacherLinks",
                columns: new[] { "TeacherId", "LinkStatus" });

            // Self-heal before enforcing one-account-per-roster-record: the old
            // credential flow never checked claims, so two student accounts could
            // both hold an Active link to the same TeacherStudent. Keep the NEWEST
            // Active claim per roster record and demote older ones to Unlinked
            // (2) so the unique index below can always be created. Idempotent.
            migrationBuilder.Sql(@"
UPDATE l
SET    l.LinkStatus = 2,
       l.UnlinkedAt = SYSUTCDATETIME()
FROM   StudentTeacherLinks l
WHERE  l.LinkStatus = 1
  AND  l.TeacherStudentId IS NOT NULL
  AND  EXISTS (SELECT 1
               FROM StudentTeacherLinks n
               WHERE n.TeacherStudentId = l.TeacherStudentId
                 AND n.LinkStatus = 1
                 AND n.Id > l.Id);
");

            migrationBuilder.CreateIndex(
                name: "UX_StudentTeacherLinks_TeacherStudentId_Active",
                table: "StudentTeacherLinks",
                column: "TeacherStudentId",
                unique: true,
                filter: "[LinkStatus] = 1 AND [TeacherStudentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentTeacherLinks_StudentUserId_TeacherId",
                table: "StudentTeacherLinks");

            migrationBuilder.DropIndex(
                name: "IX_StudentTeacherLinks_TeacherId_LinkStatus",
                table: "StudentTeacherLinks");

            migrationBuilder.DropIndex(
                name: "UX_StudentTeacherLinks_TeacherStudentId_Active",
                table: "StudentTeacherLinks");

            migrationBuilder.DropColumn(
                name: "RemovedByUserId",
                table: "StudentTeacherLinks");

            migrationBuilder.DropColumn(
                name: "RequestedAt",
                table: "StudentTeacherLinks");

            migrationBuilder.DropColumn(
                name: "RequestedStudentCode",
                table: "StudentTeacherLinks");

            migrationBuilder.DropColumn(
                name: "RequestedStudentName",
                table: "StudentTeacherLinks");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "StudentTeacherLinks");

            migrationBuilder.DropColumn(
                name: "RespondedByUserId",
                table: "StudentTeacherLinks");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTeacherLinks_StudentUserId_TeacherId",
                table: "StudentTeacherLinks",
                columns: new[] { "StudentUserId", "TeacherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentTeacherLinks_TeacherId",
                table: "StudentTeacherLinks",
                column: "TeacherId");
        }
    }
}
