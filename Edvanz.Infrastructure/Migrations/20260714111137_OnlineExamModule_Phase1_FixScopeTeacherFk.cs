using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OnlineExamModule_Phase1_FixScopeTeacherFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnlineExamScopes_Teachers_TeacherId",
                table: "OnlineExamScopes");

            migrationBuilder.AddForeignKey(
                name: "FK_OnlineExamScopes_Teachers_TeacherId",
                table: "OnlineExamScopes",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnlineExamScopes_Teachers_TeacherId",
                table: "OnlineExamScopes");

            migrationBuilder.AddForeignKey(
                name: "FK_OnlineExamScopes_Teachers_TeacherId",
                table: "OnlineExamScopes",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
