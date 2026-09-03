using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingStartDateToTeacherConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BillingStartDate",
                table: "TeacherConfigurations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BillingStartDateChangeAllowed",
                table: "TeacherConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "BillingStartDateSetAt",
                table: "TeacherConfigurations",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingStartDate",
                table: "TeacherConfigurations");

            migrationBuilder.DropColumn(
                name: "BillingStartDateChangeAllowed",
                table: "TeacherConfigurations");

            migrationBuilder.DropColumn(
                name: "BillingStartDateSetAt",
                table: "TeacherConfigurations");
        }
    }
}
