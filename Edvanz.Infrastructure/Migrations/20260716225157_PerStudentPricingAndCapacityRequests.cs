using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PerStudentPricingAndCapacityRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapacityIncreaseRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    CapacityAtRequest = table.Column<int>(type: "int", nullable: false),
                    RequestedCapacity = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_CapacityIncreaseRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CapacityIncreaseRequests_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CapacityIncreaseRequests_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPricingSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PricePerStudentEGP = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPricingSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionPricingSettings_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "ModuleQuotas",
                columns: new[] { "Id", "CreateAt", "Description", "FreeTierLimit", "ModuleKey", "UpdatedAt", "UpdatedByUserId" },
                values: new object[,]
                {
                    { 10L, new DateTime(2026, 7, 17, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, "Exams", null, null },
                    { 11L, new DateTime(2026, 7, 17, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, "OnlineExams", null, null }
                });

            migrationBuilder.InsertData(
                table: "SubscriptionPricingSettings",
                columns: new[] { "Id", "CreateAt", "PricePerStudentEGP", "UpdatedAt", "UpdatedByUserId" },
                values: new object[] { 1L, new DateTime(2026, 7, 17, 0, 0, 0, 0, DateTimeKind.Utc), 2.50m, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_CapacityIncreaseRequests_ResolvedByUserId",
                table: "CapacityIncreaseRequests",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CapacityIncreaseRequests_Status_RequestedAt",
                table: "CapacityIncreaseRequests",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_CapacityIncreaseRequests_Teacher_Pending",
                table: "CapacityIncreaseRequests",
                column: "TeacherId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPricingSettings_UpdatedByUserId",
                table: "SubscriptionPricingSettings",
                column: "UpdatedByUserId");

            // Data-fix: legacy "3000+"-tier teachers carry StudentCapacity = int.MaxValue,
            // which would overflow the decimal(10,2) money columns under the new
            // capacity × rate pricing. Concretize to 3000
            // (SubscriptionConstants.UnlimitedPackageFallbackCapacity); teachers can raise
            // it further via the new capacity-increase request flow. This UPDATE touches
            // only PRE-EXISTING columns of a PRE-EXISTING table, so the BUG-10
            // EXEC(N'...') deferral rule does not apply here.
            migrationBuilder.Sql("UPDATE [Teachers] SET [StudentCapacity] = 3000 WHERE [StudentCapacity] > 100000;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapacityIncreaseRequests");

            migrationBuilder.DropTable(
                name: "SubscriptionPricingSettings");

            migrationBuilder.DeleteData(
                table: "ModuleQuotas",
                keyColumn: "Id",
                keyValue: 10L);

            migrationBuilder.DeleteData(
                table: "ModuleQuotas",
                keyColumn: "Id",
                keyValue: 11L);
        }
    }
}
