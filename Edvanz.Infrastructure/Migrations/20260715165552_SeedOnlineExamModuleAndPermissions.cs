using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only migration — seeds the "OnlineExam" module and its two permissions
    /// ("ManageExams", "View") into the existing Models/Permissions reference tables.
    /// Values match <see cref="Edvanz.Domain.Constants.OnlineExamConstants"/> exactly.
    ///
    /// SAFE-ON-PRODUCTION GUARANTEES:
    ///   - No DDL. Does not touch the OnlineExam* schema (migrated separately, Phase 1).
    ///   - Inserts exactly one Models row and two Permissions rows. Never updates or
    ///     deletes any existing row in any table.
    ///   - Guarded by an app-level "IF NOT EXISTS" check in addition to EF's own
    ///     __EFMigrationsHistory idempotent-script wrapper — safe to include in a
    ///     `migrations script --idempotent` re-run.
    ///   - Does NOT use HasData/InsertData with an explicit identity value: Models/
    ///     Permissions rows were originally seeded outside of migrations (DbInitializer,
    ///     dev-only), so this migration has no reliable knowledge of the current max
    ///     identity value in production. SCOPE_IDENTITY() is used instead — zero
    ///     assumptions about existing row IDs, zero collision risk.
    ///   - Does NOT grant this module to any Teacher/Assistant. TutorModuleAccess and
    ///     UsersPermissions are untouched — enabling the module for a tenant is a
    ///     separate subscription/business decision, out of scope here.
    /// </summary>
    public partial class SeedOnlineExamModuleAndPermissions : Migration
    {
        private const string ModuleName = "OnlineExam";
        private const string PermissionManageExams = "ManageExams";
        private const string PermissionView = "View";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM [Models] WHERE [Name] = N'{ModuleName}')
BEGIN
    DECLARE @OnlineExamModuleId BIGINT;

    INSERT INTO [Models] ([Name], [CreateAt])
    VALUES (N'{ModuleName}', SYSUTCDATETIME());

    SET @OnlineExamModuleId = SCOPE_IDENTITY();

    INSERT INTO [Permissions] ([Name], [ModuleId], [IsRestricted], [CreateAt])
    VALUES
        (N'{PermissionManageExams}', @OnlineExamModuleId, CAST(0 AS BIT), SYSUTCDATETIME()),
        (N'{PermissionView}',        @OnlineExamModuleId, CAST(0 AS BIT), SYSUTCDATETIME());
END;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
DELETE FROM [Permissions]
WHERE [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'{ModuleName}')
  AND [Name] IN (N'{PermissionManageExams}', N'{PermissionView}');

DELETE FROM [Models]
WHERE [Name] = N'{ModuleName}'
  AND NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [ModuleId] = [Models].[Id]);
");
        }
    }
}