using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParentVisibilityVideoAndOnlineExamToTeacherConfigurationandAddDateOfBirthAndGenderToParentChild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
          

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "ParentChildren",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "ParentChildren",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
           
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "ParentChildren");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "ParentChildren");
        }
    }
}
