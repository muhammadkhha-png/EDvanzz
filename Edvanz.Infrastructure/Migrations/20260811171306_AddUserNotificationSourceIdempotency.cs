using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserNotificationSourceIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SourceEntityId",
                table: "UserNotifications",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "SourceType",
                table: "UserNotifications",
                type: "tinyint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_UserNotifications_SourceType_SourceEntityId",
                table: "UserNotifications",
                columns: new[] { "SourceType", "SourceEntityId" },
                unique: true,
                filter: "[SourceEntityId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_UserNotifications_SourceType_SourceEntityId",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "SourceEntityId",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "UserNotifications");
        }
    }
}
