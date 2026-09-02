using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppVersionConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppVersionConfigs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Platform = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    MinSupportedBuild = table.Column<int>(type: "int", nullable: false),
                    LatestBuild = table.Column<int>(type: "int", nullable: false),
                    LatestVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StoreUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppVersionConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_AppVersionConfigs_Platform",
                table: "AppVersionConfigs",
                column: "Platform",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppVersionConfigs");
        }
    }
}
