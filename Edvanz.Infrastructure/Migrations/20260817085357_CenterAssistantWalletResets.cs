using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CenterAssistantWalletResets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "AssistantId",
                table: "WalletResetLogs",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "CenterAssistantId",
                table: "WalletResetLogs",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletResetLogs_CenterAssistantId",
                table: "WalletResetLogs",
                column: "CenterAssistantId");

            migrationBuilder.CreateIndex(
                name: "IX_WRL_TeacherId_CenterAssistantId",
                table: "WalletResetLogs",
                columns: new[] { "TeacherId", "CenterAssistantId" });

            migrationBuilder.AddForeignKey(
                name: "FK_WalletResetLogs_CenterAssistants_CenterAssistantId",
                table: "WalletResetLogs",
                column: "CenterAssistantId",
                principalTable: "CenterAssistants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletResetLogs_CenterAssistants_CenterAssistantId",
                table: "WalletResetLogs");

            migrationBuilder.DropIndex(
                name: "IX_WalletResetLogs_CenterAssistantId",
                table: "WalletResetLogs");

            migrationBuilder.DropIndex(
                name: "IX_WRL_TeacherId_CenterAssistantId",
                table: "WalletResetLogs");

            migrationBuilder.DropColumn(
                name: "CenterAssistantId",
                table: "WalletResetLogs");

            migrationBuilder.AlterColumn<long>(
                name: "AssistantId",
                table: "WalletResetLogs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
