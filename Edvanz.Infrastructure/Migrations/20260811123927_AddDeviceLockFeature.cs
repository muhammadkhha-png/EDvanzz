using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceLockFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeviceLockEnabled",
                table: "TeacherConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeviceBoundAt",
                table: "StudentTeacherLinks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeviceResetAt",
                table: "StudentTeacherLinks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeviceResetByUserId",
                table: "StudentTeacherLinks",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockedDeviceId",
                table: "StudentTeacherLinks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeviceLockEnabled",
                table: "TeacherConfigurations");

            migrationBuilder.DropColumn(
                name: "DeviceBoundAt",
                table: "StudentTeacherLinks");

            migrationBuilder.DropColumn(
                name: "DeviceResetAt",
                table: "StudentTeacherLinks");

            migrationBuilder.DropColumn(
                name: "DeviceResetByUserId",
                table: "StudentTeacherLinks");

            migrationBuilder.DropColumn(
                name: "LockedDeviceId",
                table: "StudentTeacherLinks");
        }
    }
}
