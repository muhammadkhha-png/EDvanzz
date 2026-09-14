using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BooksAndFeesFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EventStudentObligations_TeacherId",
                table: "EventStudentObligations");

            migrationBuilder.DropIndex(
                name: "IX_EventPaymentTransactions_EventStudentObligationId",
                table: "EventPaymentTransactions");

            migrationBuilder.AddColumn<bool>(
                name: "ParentVisibilityExtras",
                table: "TeacherConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowExtrasOnAttendanceScreen",
                table: "TeacherConfigurations",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StudentVisibilityExtras",
                table: "TeacherConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoIncludeNewStudents",
                table: "PaymentEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "PaymentEvents",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CollectDuringAttendance",
                table: "PaymentEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "DeletedByUserId",
                table: "PaymentEvents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "PaymentEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomAmountSetAt",
                table: "EventStudentObligations",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CustomAmountSetByUserId",
                table: "EventStudentObligations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExemptReason",
                table: "EventStudentObligations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExemptedAt",
                table: "EventStudentObligations",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExemptedByUserId",
                table: "EventStudentObligations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomAmount",
                table: "EventStudentObligations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsExempt",
                table: "EventStudentObligations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ClientEntryId",
                table: "EventPaymentTransactions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionNote",
                table: "EventPaymentTransactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "EventPaymentTransactions",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "EventPaymentTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOfflineRecord",
                table: "EventPaymentTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OfflineDeviceId",
                table: "EventPaymentTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "SyncStatus",
                table: "EventPaymentTransactions",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PaymentEvents_Id_TeacherId",
                table: "PaymentEvents",
                columns: new[] { "Id", "TeacherId" });

            migrationBuilder.CreateTable(
                name: "EventPaymentEditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    EventPaymentTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentEventId = table.Column<long>(type: "bigint", nullable: true),
                    EventStudentObligationId = table.Column<long>(type: "bigint", nullable: true),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: true),
                    StudentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StudentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EventName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EditAction = table.Column<byte>(type: "tinyint", nullable: false),
                    PreviousAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    NewAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ChargedToUserId = table.Column<long>(type: "bigint", nullable: true),
                    CollectedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    EditedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    EditedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    EditReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventPaymentEditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventPaymentEditLogs_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentEventScopes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentEventId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    ScopeType = table.Column<byte>(type: "tinyint", nullable: false),
                    SessionId = table.Column<long>(type: "bigint", nullable: true),
                    SessionGroupId = table.Column<long>(type: "bigint", nullable: true),
                    AssignedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEventScopes", x => x.Id);
                    table.CheckConstraint("CK_PaymentEventScopes_TargetMatchesScopeType", "([ScopeType] = 2 AND [SessionId] IS NOT NULL AND [SessionGroupId] IS NULL) OR ([ScopeType] = 3 AND [SessionGroupId] IS NOT NULL AND [SessionId] IS NULL) OR ([ScopeType] = 4 AND [SessionId] IS NULL AND [SessionGroupId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_PaymentEventScopes_PaymentEvents_PaymentEventId_TeacherId",
                        columns: x => new { x.PaymentEventId, x.TeacherId },
                        principalTable: "PaymentEvents",
                        principalColumns: new[] { "Id", "TeacherId" });
                    table.ForeignKey(
                        name: "FK_PaymentEventScopes_SessionGroups_SessionGroupId",
                        column: x => x.SessionGroupId,
                        principalTable: "SessionGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentEventScopes_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentEventScopes_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentEventScopes_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ESO_EventId_IsExempt_Status",
                table: "EventStudentObligations",
                columns: new[] { "PaymentEventId", "IsExempt", "PaymentStatus" })
                .Annotation("SqlServer:Include", new[] { "TeacherStudentId", "AmountDue", "AmountPaid" });

            migrationBuilder.CreateIndex(
                name: "IX_ESO_TeacherId_StudentId_IsExempt",
                table: "EventStudentObligations",
                columns: new[] { "TeacherId", "TeacherStudentId", "IsExempt" })
                .Annotation("SqlServer:Include", new[] { "PaymentEventId", "AmountDue", "AmountPaid" });

            migrationBuilder.CreateIndex(
                name: "IX_EPT_ObligationId_CollectedAt",
                table: "EventPaymentTransactions",
                columns: new[] { "EventStudentObligationId", "CollectedAt" },
                descending: new[] { false, true })
                .Annotation("SqlServer:Include", new[] { "CollectedByUserId", "AmountPaid" });

            migrationBuilder.CreateIndex(
                name: "IX_EPT_TeacherId_CollectedAt",
                table: "EventPaymentTransactions",
                columns: new[] { "TeacherId", "CollectedAt" },
                descending: new[] { false, true })
                .Annotation("SqlServer:Include", new[] { "PaymentEventId", "EventStudentObligationId", "TeacherStudentId", "AmountPaid", "CollectedByUserId", "StudentName", "StudentCode", "EventName", "PaymentMethod", "CollectionNote" });

            migrationBuilder.CreateIndex(
                name: "IX_EPT_TeacherId_CollectedBy_CollectedAt",
                table: "EventPaymentTransactions",
                columns: new[] { "TeacherId", "CollectedByUserId", "CollectedAt" },
                descending: new[] { false, false, true })
                .Annotation("SqlServer:Include", new[] { "PaymentEventId", "EventStudentObligationId", "TeacherStudentId", "AmountPaid", "StudentName", "StudentCode", "EventName", "PaymentMethod", "CollectionNote" });

            migrationBuilder.CreateIndex(
                name: "UX_EPT_TeacherId_ClientEntryId",
                table: "EventPaymentTransactions",
                columns: new[] { "TeacherId", "ClientEntryId" },
                unique: true,
                filter: "[ClientEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EPEL_TeacherId_ChargedTo_EditedAt",
                table: "EventPaymentEditLogs",
                columns: new[] { "TeacherId", "ChargedToUserId", "EditedAt" })
                .Annotation("SqlServer:Include", new[] { "EditAction", "PreviousAmount", "NewAmount", "StudentName", "StudentCode", "EventName", "EventPaymentTransactionId", "TeacherStudentId", "CollectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EPEL_TransactionId",
                table: "EventPaymentEditLogs",
                column: "EventPaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEventScopes_AssignedByUserId",
                table: "PaymentEventScopes",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEventScopes_PaymentEventId",
                table: "PaymentEventScopes",
                column: "PaymentEventId")
                .Annotation("SqlServer:Include", new[] { "ScopeType", "SessionId", "SessionGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEventScopes_PaymentEventId_TeacherId",
                table: "PaymentEventScopes",
                columns: new[] { "PaymentEventId", "TeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEventScopes_SessionGroupId",
                table: "PaymentEventScopes",
                column: "SessionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEventScopes_SessionId",
                table: "PaymentEventScopes",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEventScopes_TeacherId_SessionGroupId",
                table: "PaymentEventScopes",
                columns: new[] { "TeacherId", "SessionGroupId" },
                filter: "[SessionGroupId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "PaymentEventId", "ScopeType" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEventScopes_TeacherId_SessionId",
                table: "PaymentEventScopes",
                columns: new[] { "TeacherId", "SessionId" },
                filter: "[SessionId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "PaymentEventId", "ScopeType" });

            migrationBuilder.CreateIndex(
                name: "UX_PaymentEventScopes_Event_Type_Target",
                table: "PaymentEventScopes",
                columns: new[] { "PaymentEventId", "ScopeType", "SessionId", "SessionGroupId" },
                unique: true);

            // ── Books & fees is SUBSCRIBER-ONLY: free-tier limit 1 → 0 ──
            // SubscriptionGateService.CanCreateAsync short-circuits on limit <= 0 without counting,
            // the same way Assistants / Groups / Triggers already work. Existing items keep working:
            // the gate is on CREATE only, so a free-tier teacher who already made one does not lose it.
            //
            // Idempotent and safe as a bare Sql(): ModuleQuotas and every column named here are
            // PRE-EXISTING, so nothing is bound to a column added in this same GO-less batch — which
            // is the trap that silently broke the SessionOccurrence slot-key backfill (BUG-10).
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [ModuleQuotas] WHERE [ModuleKey] = N'Events')
BEGIN
    UPDATE [ModuleQuotas]
       SET [FreeTierLimit] = 0
     WHERE [ModuleKey] = N'Events' AND [FreeTierLimit] <> 0;
END
ELSE
BEGIN
    INSERT INTO [ModuleQuotas] ([ModuleKey], [FreeTierLimit], [Description], [CreateAt])
    VALUES (N'Events', 0, N'Books and fees items - subscriber only.', SYSUTCDATETIME());
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventPaymentEditLogs");

            migrationBuilder.DropTable(
                name: "PaymentEventScopes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PaymentEvents_Id_TeacherId",
                table: "PaymentEvents");

            migrationBuilder.DropIndex(
                name: "IX_ESO_EventId_IsExempt_Status",
                table: "EventStudentObligations");

            migrationBuilder.DropIndex(
                name: "IX_ESO_TeacherId_StudentId_IsExempt",
                table: "EventStudentObligations");

            migrationBuilder.DropIndex(
                name: "IX_EPT_ObligationId_CollectedAt",
                table: "EventPaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_EPT_TeacherId_CollectedAt",
                table: "EventPaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_EPT_TeacherId_CollectedBy_CollectedAt",
                table: "EventPaymentTransactions");

            migrationBuilder.DropIndex(
                name: "UX_EPT_TeacherId_ClientEntryId",
                table: "EventPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ParentVisibilityExtras",
                table: "TeacherConfigurations");

            migrationBuilder.DropColumn(
                name: "ShowExtrasOnAttendanceScreen",
                table: "TeacherConfigurations");

            migrationBuilder.DropColumn(
                name: "StudentVisibilityExtras",
                table: "TeacherConfigurations");

            migrationBuilder.DropColumn(
                name: "AutoIncludeNewStudents",
                table: "PaymentEvents");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "PaymentEvents");

            migrationBuilder.DropColumn(
                name: "CollectDuringAttendance",
                table: "PaymentEvents");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "PaymentEvents");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "PaymentEvents");

            migrationBuilder.DropColumn(
                name: "CustomAmountSetAt",
                table: "EventStudentObligations");

            migrationBuilder.DropColumn(
                name: "CustomAmountSetByUserId",
                table: "EventStudentObligations");

            migrationBuilder.DropColumn(
                name: "ExemptReason",
                table: "EventStudentObligations");

            migrationBuilder.DropColumn(
                name: "ExemptedAt",
                table: "EventStudentObligations");

            migrationBuilder.DropColumn(
                name: "ExemptedByUserId",
                table: "EventStudentObligations");

            migrationBuilder.DropColumn(
                name: "IsCustomAmount",
                table: "EventStudentObligations");

            migrationBuilder.DropColumn(
                name: "IsExempt",
                table: "EventStudentObligations");

            migrationBuilder.DropColumn(
                name: "ClientEntryId",
                table: "EventPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "CollectionNote",
                table: "EventPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "EventPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "EventPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "IsOfflineRecord",
                table: "EventPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "OfflineDeviceId",
                table: "EventPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "EventPaymentTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_EventStudentObligations_TeacherId",
                table: "EventStudentObligations",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_EventPaymentTransactions_EventStudentObligationId",
                table: "EventPaymentTransactions",
                column: "EventStudentObligationId");
        }
    }
}
