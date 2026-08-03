using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartureRefundAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CollectedByUserId",
                table: "StudentDepartures",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundPeriodStart",
                table: "StudentDepartures",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SD_TeacherId_DepartedAt",
                table: "StudentDepartures",
                columns: new[] { "TeacherId", "DepartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SD_TeacherId_DepartedAt",
                table: "StudentDepartures");

            migrationBuilder.DropColumn(
                name: "CollectedByUserId",
                table: "StudentDepartures");

            migrationBuilder.DropColumn(
                name: "RefundPeriodStart",
                table: "StudentDepartures");
        }
    }
}
