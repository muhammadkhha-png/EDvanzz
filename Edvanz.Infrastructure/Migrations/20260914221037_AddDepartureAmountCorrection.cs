using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartureAmountCorrection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountBeforeEdit",
                table: "StudentDepartures",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AmountEditNote",
                table: "StudentDepartures",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AmountEditedAt",
                table: "StudentDepartures",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AmountEditedByUserId",
                table: "StudentDepartures",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountBeforeEdit",
                table: "StudentDepartures");

            migrationBuilder.DropColumn(
                name: "AmountEditNote",
                table: "StudentDepartures");

            migrationBuilder.DropColumn(
                name: "AmountEditedAt",
                table: "StudentDepartures");

            migrationBuilder.DropColumn(
                name: "AmountEditedByUserId",
                table: "StudentDepartures");
        }
    }
}
