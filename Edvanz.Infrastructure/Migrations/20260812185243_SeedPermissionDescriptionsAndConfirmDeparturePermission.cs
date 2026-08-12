using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only migration — backfills Description on every existing seeded Permission row
    /// (idempotent: only touches rows where Description is currently NULL) and inserts the new
    /// "ConfirmDeparture" permission under the "Payment" module (REQ: course-withdrawal/refund
    /// endpoint now requires a dedicated permission instead of a Teacher/SuperAdmin roleOnly gate).
    ///
    /// SAFE-ON-PRODUCTION GUARANTEES (matches SeedOnlineExamModuleAndPermissions convention):
    ///   - No DDL. Schema change (the Description column itself) ships in a separate,
    ///     EF-scaffolded migration (AddPermissionDescriptionColumn) that must run first.
    ///   - Description backfill only ever writes to NULL cells — never overwrites a value an
    ///     admin may have already edited by hand.
    ///   - The new permission insert is guarded by an existence check (IF NOT EXISTS), matching
    ///     the module/permission lookup-by-name pattern already used by
    ///     SeedOnlineExamModuleAndPermissions — no HasData/identity-value assumptions.
    ///   - Does NOT grant ConfirmDeparture to any assistant or profile — that remains a separate,
    ///     per-tenant decision made through the existing Permission/Profile endpoints.
    /// </summary>
    public partial class SeedPermissionDescriptionsAndConfirmDeparturePermission : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Backfill Description on existing seeded permissions ──
            // Idempotent: only touches rows where Description is currently NULL, so it never
            // overwrites a description an admin may have already edited by hand.
            migrationBuilder.Sql(@"
UPDATE [Permissions] SET [Description] = N'Allows viewing the list of enrolled students.' WHERE [Name] = N'ViewList' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Student');
UPDATE [Permissions] SET [Description] = N'Allows viewing a single student''s profile details.' WHERE [Name] = N'ViewProfile' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Student');
UPDATE [Permissions] SET [Description] = N'Allows adding a new student to the roster.' WHERE [Name] = N'Add' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Student');
UPDATE [Permissions] SET [Description] = N'Allows editing an existing student''s details.' WHERE [Name] = N'Edit' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Student');
UPDATE [Permissions] SET [Description] = N'Allows removing a student from the roster.' WHERE [Name] = N'Delete' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Student');
UPDATE [Permissions] SET [Description] = N'Allows bulk-importing students from a file.' WHERE [Name] = N'Import' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Student');
UPDATE [Permissions] SET [Description] = N'Allows exporting student data and reports.' WHERE [Name] = N'ExportReports' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Student');
UPDATE [Permissions] SET [Description] = N'Allows viewing and printing student ID barcodes.' WHERE [Name] = N'ViewBarcodes' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Student');
UPDATE [Permissions] SET [Description] = N'Allows viewing the session schedule.' WHERE [Name] = N'View' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Session');
UPDATE [Permissions] SET [Description] = N'Allows creating a new session.' WHERE [Name] = N'Create' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Session');
UPDATE [Permissions] SET [Description] = N'Allows editing an existing session''s details.' WHERE [Name] = N'Edit' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Session');
UPDATE [Permissions] SET [Description] = N'Allows deleting a session.' WHERE [Name] = N'Delete' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Session');
UPDATE [Permissions] SET [Description] = N'Allows viewing session groups.' WHERE [Name] = N'ViewGroups' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Session');
UPDATE [Permissions] SET [Description] = N'Allows assigning students to a session.' WHERE [Name] = N'AssignStudents' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Session');
UPDATE [Permissions] SET [Description] = N'Allows creating and managing session groups.' WHERE [Name] = N'ManageGroups' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Session');
UPDATE [Permissions] SET [Description] = N'Allows viewing which students belong to a session.' WHERE [Name] = N'ViewMembership' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Session');
UPDATE [Permissions] SET [Description] = N'Allows adding or removing students from a session.' WHERE [Name] = N'ManageMembership' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Session');
UPDATE [Permissions] SET [Description] = N'Allows recording student attendance for a session.' WHERE [Name] = N'Take' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Attendance');
UPDATE [Permissions] SET [Description] = N'Allows editing a previously recorded attendance entry.' WHERE [Name] = N'Edit' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Attendance');
UPDATE [Permissions] SET [Description] = N'Allows viewing past attendance records.' WHERE [Name] = N'ViewHistory' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Attendance');
UPDATE [Permissions] SET [Description] = N'Allows viewing the absence overview across students.' WHERE [Name] = N'ViewAbsenceOverview' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Attendance');
UPDATE [Permissions] SET [Description] = N'Allows generating and exporting attendance reports.' WHERE [Name] = N'GenerateReports' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Attendance');
UPDATE [Permissions] SET [Description] = N'Allows collecting a payment from a student.' WHERE [Name] = N'Collect' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Payment');
UPDATE [Permissions] SET [Description] = N'Allows viewing a student''s payment history.' WHERE [Name] = N'ViewHistory' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Payment');
UPDATE [Permissions] SET [Description] = N'Allows editing or deleting a previously recorded payment. Restricted — carries financial risk.' WHERE [Name] = N'EditHistory' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Payment');
UPDATE [Permissions] SET [Description] = N'Allows viewing the list of students with unpaid balances.' WHERE [Name] = N'ViewUnpaidStudents' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Payment');
UPDATE [Permissions] SET [Description] = N'Allows viewing a summary of amounts collected by collector.' WHERE [Name] = N'ViewCollectorSummary' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Payment');
UPDATE [Permissions] SET [Description] = N'Allows generating and exporting payment reports.' WHERE [Name] = N'GenerateReports' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Payment');
UPDATE [Permissions] SET [Description] = N'Allows viewing payment events.' WHERE [Name] = N'View' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Event-Based Payment');
UPDATE [Permissions] SET [Description] = N'Allows creating a new payment event.' WHERE [Name] = N'Create' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Event-Based Payment');
UPDATE [Permissions] SET [Description] = N'Allows editing an existing payment event.' WHERE [Name] = N'Edit' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Event-Based Payment');
UPDATE [Permissions] SET [Description] = N'Allows deleting a payment event.' WHERE [Name] = N'Delete' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Event-Based Payment');
UPDATE [Permissions] SET [Description] = N'Allows collecting payment for an event.' WHERE [Name] = N'CollectPayment' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Event-Based Payment');
UPDATE [Permissions] SET [Description] = N'Allows generating and exporting event payment reports.' WHERE [Name] = N'GenerateReports' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Event-Based Payment');
UPDATE [Permissions] SET [Description] = N'Allows viewing exam and homework assignments.' WHERE [Name] = N'View' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Exams And Homework');
UPDATE [Permissions] SET [Description] = N'Allows creating a new exam or homework assignment.' WHERE [Name] = N'Create' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Exams And Homework');
UPDATE [Permissions] SET [Description] = N'Allows editing an existing assignment.' WHERE [Name] = N'Edit' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Exams And Homework');
UPDATE [Permissions] SET [Description] = N'Allows deleting an assignment.' WHERE [Name] = N'Delete' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Exams And Homework');
UPDATE [Permissions] SET [Description] = N'Allows recording exam attendance and grades.' WHERE [Name] = N'RecordExamAttendanceAndGrades' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Exams And Homework');
UPDATE [Permissions] SET [Description] = N'Allows recording whether a student completed their homework.' WHERE [Name] = N'RecordHomeworkCompletion' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Exams And Homework');
UPDATE [Permissions] SET [Description] = N'Allows entering grades that were left pending.' WHERE [Name] = N'EnterPendingGrades' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Exams And Homework');
UPDATE [Permissions] SET [Description] = N'Allows generating and exporting exam/homework reports.' WHERE [Name] = N'GenerateReports' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Exams And Homework');
UPDATE [Permissions] SET [Description] = N'Allows full management of exam/homework assignment templates and their configuration.' WHERE [Name] = N'ManageAssignments' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Exams And Homework');
UPDATE [Permissions] SET [Description] = N'Allows viewing sent message history.' WHERE [Name] = N'ViewHistory' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Messaging');
UPDATE [Permissions] SET [Description] = N'Allows manually sending a message to students or parents.' WHERE [Name] = N'SendManual' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Messaging');
UPDATE [Permissions] SET [Description] = N'Allows creating and editing message templates.' WHERE [Name] = N'ManageTemplates' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Messaging');
UPDATE [Permissions] SET [Description] = N'Allows configuring automated message triggers.' WHERE [Name] = N'ConfigureAutomatedTriggers' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Messaging');
UPDATE [Permissions] SET [Description] = N'Allows viewing uploaded videos.' WHERE [Name] = N'View' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Videos');
UPDATE [Permissions] SET [Description] = N'Allows uploading, editing, and deleting videos.' WHERE [Name] = N'ManageVideos' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'Videos');
UPDATE [Permissions] SET [Description] = N'Allows viewing online exams.' WHERE [Name] = N'View' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'OnlineExam');
UPDATE [Permissions] SET [Description] = N'Allows creating, editing, and managing online exams.' WHERE [Name] = N'ManageExams' AND [Description] IS NULL AND [ModuleId] = (SELECT [Id] FROM [Models] WHERE [Name] = N'OnlineExam');
");

            // ── 2. Insert the new Payment.ConfirmDeparture permission ──
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Permissions] p INNER JOIN [Models] m ON m.[Id] = p.[ModuleId] WHERE p.[Name] = N'ConfirmDeparture' AND m.[Name] = N'Payment')
BEGIN
    INSERT INTO [Permissions] ([Name], [ModuleId], [IsRestricted], [Description], [CreateAt])
    SELECT N'ConfirmDeparture', [Id], CAST(1 AS BIT), N'Allows confirming a student''s course withdrawal and processing the associated refund or owed-amount settlement.', SYSUTCDATETIME()
    FROM [Models] WHERE [Name] = N'Payment';
END;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── 1. Remove the new Payment.ConfirmDeparture permission ──
            // Also removes any UsersPermissions/TemplatesPermisions rows referencing it, mirroring
            // how SeedOnlineExamModuleAndPermissions.Down() cleans up its own inserted rows.
            migrationBuilder.Sql(@"
DECLARE @ConfirmDeparturePermissionId BIGINT;
SELECT @ConfirmDeparturePermissionId = p.[Id] FROM [Permissions] p
    INNER JOIN [Models] m ON m.[Id] = p.[ModuleId]
    WHERE p.[Name] = N'ConfirmDeparture' AND m.[Name] = N'Payment';

IF @ConfirmDeparturePermissionId IS NOT NULL
BEGIN
    DELETE FROM [UsersPermissions] WHERE [PermissionId] = @ConfirmDeparturePermissionId;
    DELETE FROM [TemplatesPermisions] WHERE [PermisionId] = @ConfirmDeparturePermissionId;
    DELETE FROM [Permissions] WHERE [Id] = @ConfirmDeparturePermissionId;
END;
");

            // ── 2. Description backfill is intentionally NOT reverted ──
            // Setting these back to NULL on Down() would also wipe out any description an admin
            // may have manually edited after Up() ran. If a true rollback of the backfilled text
            // is ever needed, do it with a targeted, reviewed script rather than blindly here.
        }
    }
}