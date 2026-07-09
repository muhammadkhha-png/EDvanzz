using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Users_Phone_Optional_FilteredUnique : Migration
    {
        // Makes User.PhoneNumber OPTIONAL and its unique index FILTERED (WHERE PhoneNumber IS NOT NULL)
        // so multiple phone-less users are allowed while supplied phones stay globally unique. This
        // fixes the assistant-create "conflict with existing data" bug (a 2nd blank-phone user
        // collided on the non-filtered unique phone index).
        //
        // The phone index has DRIFTED across environments (prod had non-filtered unique
        // UX_Users_PhoneNumber; other DBs had the non-unique IX_User_Phnoe), so the drops are guarded
        // with IF EXISTS to stay safe wherever this runs. Pre-req verified on prod before deploy:
        // 0 empty-string phones and 0 duplicate non-null phones, so the filtered unique index builds.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Users_PhoneNumber' AND object_id = OBJECT_ID('dbo.Users'))
    DROP INDEX [UX_Users_PhoneNumber] ON [dbo].[Users];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_User_Phnoe' AND object_id = OBJECT_ID('dbo.Users'))
    DROP INDEX [IX_User_Phnoe] ON [dbo].[Users];
");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX [UX_Users_PhoneNumber] ON [dbo].[Users]([PhoneNumber]) WHERE [PhoneNumber] IS NOT NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Users_PhoneNumber' AND object_id = OBJECT_ID('dbo.Users'))
    DROP INDEX [UX_Users_PhoneNumber] ON [dbo].[Users];
");

            // Reverting to a required column will fail if any NULL phones exist; blank them first.
            migrationBuilder.Sql("UPDATE [dbo].[Users] SET [PhoneNumber] = '' WHERE [PhoneNumber] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX [UX_Users_PhoneNumber] ON [dbo].[Users]([PhoneNumber]);
");
        }
    }
}
