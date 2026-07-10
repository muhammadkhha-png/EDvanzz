using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_ModuleQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModuleQuotas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModuleKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FreeTierLimit = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleQuotas", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ModuleQuotas",
                columns: new[] { "Id", "CreateAt", "Description", "FreeTierLimit", "ModuleKey" },
                values: new object[,]
                {
                    { 1L, new DateTime(2026, 7, 10, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, "Students" },
                    { 2L, new DateTime(2026, 7, 10, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, "Sessions" },
                    { 3L, new DateTime(2026, 7, 10, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, "Assistants" },
                    { 4L, new DateTime(2026, 7, 10, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, "Groups" },
                    { 5L, new DateTime(2026, 7, 10, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, "Videos" },
                    { 6L, new DateTime(2026, 7, 10, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, "AssignmentTemplates" },
                    { 7L, new DateTime(2026, 7, 10, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, "Events" },
                    { 8L, new DateTime(2026, 7, 10, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, "MessageTemplates" },
                    { 9L, new DateTime(2026, 7, 10, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, "Triggers" }
                });

            migrationBuilder.CreateIndex(
                name: "UX_ModuleQuotas_ModuleKey",
                table: "ModuleQuotas",
                column: "ModuleKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModuleQuotas");
        }
    }
}
