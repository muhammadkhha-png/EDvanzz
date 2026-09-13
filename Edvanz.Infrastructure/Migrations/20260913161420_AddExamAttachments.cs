using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExamAttachmentReleaseDelayHours",
                table: "TeacherConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 48);

            migrationBuilder.AddColumn<long>(
                name: "AssignmentTemplateId",
                table: "FileObjects",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AttachmentsReleaseBaseAt",
                table: "AssignmentTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AttachmentsReleaseOverride",
                table: "AssignmentTemplates",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileObjects_AssignmentTemplateId",
                table: "FileObjects",
                column: "AssignmentTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileObjects_AssignmentTemplates_AssignmentTemplateId",
                table: "FileObjects",
                column: "AssignmentTemplateId",
                principalTable: "AssignmentTemplates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileObjects_AssignmentTemplates_AssignmentTemplateId",
                table: "FileObjects");

            migrationBuilder.DropIndex(
                name: "IX_FileObjects_AssignmentTemplateId",
                table: "FileObjects");

            migrationBuilder.DropColumn(
                name: "ExamAttachmentReleaseDelayHours",
                table: "TeacherConfigurations");

            migrationBuilder.DropColumn(
                name: "AssignmentTemplateId",
                table: "FileObjects");

            migrationBuilder.DropColumn(
                name: "AttachmentsReleaseBaseAt",
                table: "AssignmentTemplates");

            migrationBuilder.DropColumn(
                name: "AttachmentsReleaseOverride",
                table: "AssignmentTemplates");
        }
    }
}
