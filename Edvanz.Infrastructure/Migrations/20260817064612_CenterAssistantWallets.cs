using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CenterAssistantWallets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AW_TeacherId_AssistantId",
                table: "AssistantWallets");

            migrationBuilder.AlterColumn<long>(
                name: "AssistantId",
                table: "AssistantWallets",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "CenterAssistantId",
                table: "AssistantWallets",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantWallets_CenterAssistantId",
                table: "AssistantWallets",
                column: "CenterAssistantId");

            migrationBuilder.CreateIndex(
                name: "IX_AW_TeacherId_AssistantId",
                table: "AssistantWallets",
                columns: new[] { "TeacherId", "AssistantId" },
                unique: true,
                filter: "[AssistantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AW_TeacherId_CenterAssistantId",
                table: "AssistantWallets",
                columns: new[] { "TeacherId", "CenterAssistantId" },
                unique: true,
                filter: "[CenterAssistantId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantWallets_CenterAssistants_CenterAssistantId",
                table: "AssistantWallets",
                column: "CenterAssistantId",
                principalTable: "CenterAssistants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssistantWallets_CenterAssistants_CenterAssistantId",
                table: "AssistantWallets");

            migrationBuilder.DropIndex(
                name: "IX_AssistantWallets_CenterAssistantId",
                table: "AssistantWallets");

            migrationBuilder.DropIndex(
                name: "IX_AW_TeacherId_AssistantId",
                table: "AssistantWallets");

            migrationBuilder.DropIndex(
                name: "IX_AW_TeacherId_CenterAssistantId",
                table: "AssistantWallets");

            migrationBuilder.DropColumn(
                name: "CenterAssistantId",
                table: "AssistantWallets");

            migrationBuilder.AlterColumn<long>(
                name: "AssistantId",
                table: "AssistantWallets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AW_TeacherId_AssistantId",
                table: "AssistantWallets",
                columns: new[] { "TeacherId", "AssistantId" },
                unique: true);
        }
    }
}
