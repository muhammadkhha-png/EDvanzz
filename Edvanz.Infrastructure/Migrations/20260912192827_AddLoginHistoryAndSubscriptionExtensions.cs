using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginHistoryAndSubscriptionExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionExtensions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherSubscriptionId = table.Column<long>(type: "bigint", nullable: false),
                    DaysAdded = table.Column<int>(type: "int", nullable: false),
                    PreviousEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NewEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AmountPaidEGP = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExtendedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionExtensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionExtensions_TeacherSubscriptions_TeacherSubscriptionId",
                        column: x => x.TeacherSubscriptionId,
                        principalTable: "TeacherSubscriptions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SubscriptionExtensions_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserLoginActivity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    DeviceOrBrowser = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLoginActivity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLoginActivity_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionExtensions_TeacherId_CreateAt",
                table: "SubscriptionExtensions",
                columns: new[] { "TeacherId", "CreateAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionExtensions_TeacherSubscriptionId",
                table: "SubscriptionExtensions",
                column: "TeacherSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLoginActivity_UserId_CreateAt",
                table: "UserLoginActivity",
                columns: new[] { "UserId", "CreateAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionExtensions");

            migrationBuilder.DropTable(
                name: "UserLoginActivity");
        }
    }
}
