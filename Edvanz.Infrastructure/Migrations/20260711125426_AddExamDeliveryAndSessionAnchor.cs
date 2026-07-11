using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamDeliveryAndSessionAnchor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NameAr",
                table: "AssignmentTemplates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<byte>(
                name: "ExamDeliveryType",
                table: "AssignmentTemplates",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SessionId",
                table: "AssignmentOccurrences",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SessionOccurrenceId",
                table: "AssignmentOccurrences",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentOccurrences_SessionId",
                table: "AssignmentOccurrences",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentOccurrences_SessionOccurrenceId",
                table: "AssignmentOccurrences",
                column: "SessionOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentOccurrences_TeacherId_SessionId",
                table: "AssignmentOccurrences",
                columns: new[] { "TeacherId", "SessionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentOccurrences_SessionOccurrences_SessionOccurrenceId",
                table: "AssignmentOccurrences",
                column: "SessionOccurrenceId",
                principalTable: "SessionOccurrences",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentOccurrences_Sessions_SessionId",
                table: "AssignmentOccurrences",
                column: "SessionId",
                principalTable: "Sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentOccurrences_SessionOccurrences_SessionOccurrenceId",
                table: "AssignmentOccurrences");

            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentOccurrences_Sessions_SessionId",
                table: "AssignmentOccurrences");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentOccurrences_SessionId",
                table: "AssignmentOccurrences");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentOccurrences_SessionOccurrenceId",
                table: "AssignmentOccurrences");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentOccurrences_TeacherId_SessionId",
                table: "AssignmentOccurrences");

            migrationBuilder.DropColumn(
                name: "ExamDeliveryType",
                table: "AssignmentTemplates");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "AssignmentOccurrences");

            migrationBuilder.DropColumn(
                name: "SessionOccurrenceId",
                table: "AssignmentOccurrences");

            migrationBuilder.AlterColumn<string>(
                name: "NameAr",
                table: "AssignmentTemplates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
