using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerialPlusPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ManagerialPlusMonthlyPriceEGP",
                table: "SubscriptionPricingSettings",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 650.00m);

            migrationBuilder.AddColumn<int>(
                name: "ManagerialPlusTeacherSlots",
                table: "CenterSubscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StudentCapacityUnderManagerialPlus",
                table: "CenterSubscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ManagerialPlusTeacherSlots",
                table: "CenterSubscriptionRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StudentCapacityUnderManagerialPlus",
                table: "CenterSubscriptionRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ManagerialPlusTeacherSlotPriceEGP",
                table: "CenterSubscriptionPricingSettings",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 65.00m);

            migrationBuilder.UpdateData(
                table: "CenterSubscriptionPricingSettings",
                keyColumn: "Id",
                keyValue: 1L,
                column: "ManagerialPlusTeacherSlotPriceEGP",
                value: 65.00m);

            migrationBuilder.UpdateData(
                table: "SubscriptionPricingSettings",
                keyColumn: "Id",
                keyValue: 1L,
                column: "ManagerialPlusMonthlyPriceEGP",
                value: 650.00m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManagerialPlusMonthlyPriceEGP",
                table: "SubscriptionPricingSettings");

            migrationBuilder.DropColumn(
                name: "ManagerialPlusTeacherSlots",
                table: "CenterSubscriptions");

            migrationBuilder.DropColumn(
                name: "StudentCapacityUnderManagerialPlus",
                table: "CenterSubscriptions");

            migrationBuilder.DropColumn(
                name: "ManagerialPlusTeacherSlots",
                table: "CenterSubscriptionRequests");

            migrationBuilder.DropColumn(
                name: "StudentCapacityUnderManagerialPlus",
                table: "CenterSubscriptionRequests");

            migrationBuilder.DropColumn(
                name: "ManagerialPlusTeacherSlotPriceEGP",
                table: "CenterSubscriptionPricingSettings");
        }
    }
}
