using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addpermisiontemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Module",
                table: "Permissions");

            migrationBuilder.AddColumn<long>(
                name: "ModuleId",
                table: "Permissions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "Models",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Models", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Templates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplatesOfAssistants",
                columns: table => new
                {
                    TemplateId = table.Column<long>(type: "bigint", nullable: false),
                    AssistantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplatesOfAssistants", x => new { x.AssistantId, x.TemplateId });
                    table.ForeignKey(
                        name: "FK_TemplatesOfAssistants_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_TemplatesOfAssistants_Users_AssistantId",
                        column: x => x.AssistantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "TemplatesPermisions",
                columns: table => new
                {
                    TemplateId = table.Column<long>(type: "bigint", nullable: false),
                    PermisionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplatesPermisions", x => new { x.PermisionId, x.TemplateId });
                    table.ForeignKey(
                        name: "FK_TemplatesPermisions_Permissions_PermisionId",
                        column: x => x.PermisionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_TemplatesPermisions_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ModuleId",
                table: "Permissions",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplatesOfAssistants_TemplateId",
                table: "TemplatesOfAssistants",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplatesPermisions_TemplateId",
                table: "TemplatesPermisions",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Permissions_Models_ModuleId",
                table: "Permissions",
                column: "ModuleId",
                principalTable: "Models",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Permissions_Models_ModuleId",
                table: "Permissions");

            migrationBuilder.DropTable(
                name: "Models");

            migrationBuilder.DropTable(
                name: "TemplatesOfAssistants");

            migrationBuilder.DropTable(
                name: "TemplatesPermisions");

            migrationBuilder.DropTable(
                name: "Templates");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_ModuleId",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "ModuleId",
                table: "Permissions");

            migrationBuilder.AddColumn<string>(
                name: "Module",
                table: "Permissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
