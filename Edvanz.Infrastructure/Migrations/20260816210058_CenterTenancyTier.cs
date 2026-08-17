using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CenterTenancyTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CenterId",
                table: "Teachers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "CenterPlanType",
                table: "Teachers",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RevenueSharePercentOverride",
                table: "Teachers",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Centers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CenterCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    DefaultRevenueSharePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    LanguagePreference = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    AccountStatus = table.Column<int>(type: "int", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Centers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Centers_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Centers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CenterSubscriptionPricingSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullTeacherSlotPriceEGP = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ManagerialTeacherSlotPriceEGP = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CenterSubscriptionPricingSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CenterSubscriptionPricingSettings_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CenterAssistants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CenterId = table.Column<long>(type: "bigint", nullable: false),
                    LanguagePreference = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AccountStatus = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CenterAssistants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CenterAssistants_Centers_CenterId",
                        column: x => x.CenterId,
                        principalTable: "Centers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CenterAssistants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CenterSubscriptionRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CenterId = table.Column<long>(type: "bigint", nullable: false),
                    FullTeacherSlots = table.Column<int>(type: "int", nullable: false),
                    ManagerialTeacherSlots = table.Column<int>(type: "int", nullable: false),
                    StudentCapacityTotal = table.Column<int>(type: "int", nullable: false),
                    StudentCapacityUnderFull = table.Column<int>(type: "int", nullable: false),
                    StudentCapacityUnderManagerial = table.Column<int>(type: "int", nullable: false),
                    ComputedAmountEGP = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CenterSubscriptionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CenterSubscriptionRequests_Centers_CenterId",
                        column: x => x.CenterId,
                        principalTable: "Centers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CenterSubscriptionRequests_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CenterSubscriptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CenterId = table.Column<long>(type: "bigint", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    FullTeacherSlots = table.Column<int>(type: "int", nullable: false),
                    ManagerialTeacherSlots = table.Column<int>(type: "int", nullable: false),
                    StudentCapacityTotal = table.Column<int>(type: "int", nullable: false),
                    StudentCapacityUnderFull = table.Column<int>(type: "int", nullable: false),
                    StudentCapacityUnderManagerial = table.Column<int>(type: "int", nullable: false),
                    AmountPaidEGP = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    PaymentConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CenterSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CenterSubscriptions_Centers_CenterId",
                        column: x => x.CenterId,
                        principalTable: "Centers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CenterSubscriptions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "CenterSubscriptionPricingSettings",
                columns: new[] { "Id", "CreateAt", "FullTeacherSlotPriceEGP", "ManagerialTeacherSlotPriceEGP", "UpdatedAt", "UpdatedByUserId" },
                values: new object[] { 1L, new DateTime(2026, 8, 16, 0, 0, 0, 0, DateTimeKind.Utc), 100.00m, 50.00m, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_CenterId",
                table: "Teachers",
                column: "CenterId");

            migrationBuilder.CreateIndex(
                name: "IX_CenterAssistants_CenterId",
                table: "CenterAssistants",
                column: "CenterId");

            migrationBuilder.CreateIndex(
                name: "IX_CenterAssistants_UserId",
                table: "CenterAssistants",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Centers_CenterCode",
                table: "Centers",
                column: "CenterCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Centers_CreatedByUserId",
                table: "Centers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Centers_UserId",
                table: "Centers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CenterSubscriptionPricingSettings_UpdatedByUserId",
                table: "CenterSubscriptionPricingSettings",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CenterSubscriptionRequests_ResolvedByUserId",
                table: "CenterSubscriptionRequests",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CenterSubscriptionRequests_Status_RequestedAt",
                table: "CenterSubscriptionRequests",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_CenterSubscriptionRequests_Center_Pending",
                table: "CenterSubscriptionRequests",
                column: "CenterId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CenterSubscriptions_CenterId_EndDate",
                table: "CenterSubscriptions",
                columns: new[] { "CenterId", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CenterSubscriptions_CreatedByUserId",
                table: "CenterSubscriptions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CenterSubscriptions_Current",
                table: "CenterSubscriptions",
                column: "CenterId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Teachers_Centers_CenterId",
                table: "Teachers",
                column: "CenterId",
                principalTable: "Centers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Teachers_Centers_CenterId",
                table: "Teachers");

            migrationBuilder.DropTable(
                name: "CenterAssistants");

            migrationBuilder.DropTable(
                name: "CenterSubscriptionPricingSettings");

            migrationBuilder.DropTable(
                name: "CenterSubscriptionRequests");

            migrationBuilder.DropTable(
                name: "CenterSubscriptions");

            migrationBuilder.DropTable(
                name: "Centers");

            migrationBuilder.DropIndex(
                name: "IX_Teachers_CenterId",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "CenterId",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "CenterPlanType",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "RevenueSharePercentOverride",
                table: "Teachers");
        }
    }
}
