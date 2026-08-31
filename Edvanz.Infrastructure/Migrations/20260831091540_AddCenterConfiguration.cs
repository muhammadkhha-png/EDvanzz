using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCenterConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CenterConfigurations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CenterId = table.Column<long>(type: "bigint", nullable: false),
                    StudentCodeGenerationMode = table.Column<int>(type: "int", nullable: false),
                    StudentCodeLanguage = table.Column<int>(type: "int", nullable: false),
                    SessionNameMode = table.Column<int>(type: "int", nullable: false),
                    SessionNameLanguage = table.Column<int>(type: "int", nullable: false),
                    IsProratedPaymentEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ConsecutiveAbsenceThreshold = table.Column<int>(type: "int", nullable: false),
                    ConsecutiveUnpaidThreshold = table.Column<int>(type: "int", nullable: false),
                    BarcodeDisplayMode = table.Column<int>(type: "int", nullable: false),
                    StudentVisibilityAttendance = table.Column<bool>(type: "bit", nullable: false),
                    StudentVisibilityPayment = table.Column<bool>(type: "bit", nullable: false),
                    StudentVisibilityHomework = table.Column<bool>(type: "bit", nullable: false),
                    StudentVisibilityExamDefault = table.Column<bool>(type: "bit", nullable: false),
                    StudentVisibilityOnlineExamDefault = table.Column<bool>(type: "bit", nullable: false),
                    StudentVisibilityVideo = table.Column<bool>(type: "bit", nullable: false),
                    ParentVisibilityAttendance = table.Column<bool>(type: "bit", nullable: false),
                    ParentVisibilityPayment = table.Column<bool>(type: "bit", nullable: false),
                    ParentVisibilityHomework = table.Column<bool>(type: "bit", nullable: false),
                    ParentVisibilityExamDefault = table.Column<bool>(type: "bit", nullable: false),
                    ParentVisibilityOnlineExamDefault = table.Column<bool>(type: "bit", nullable: false),
                    ParentVisibilityVideo = table.Column<bool>(type: "bit", nullable: false),
                    IsDeviceLockEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ShowPaymentInfoOnAttendanceScreen = table.Column<bool>(type: "bit", nullable: true),
                    ShowAttendanceHistoryOnAttendanceScreen = table.Column<bool>(type: "bit", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CenterConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CenterConfigurations_Centers_CenterId",
                        column: x => x.CenterId,
                        principalTable: "Centers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CenterProratedTiers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CenterConfigurationId = table.Column<long>(type: "bigint", nullable: false),
                    TierNumber = table.Column<int>(type: "int", nullable: false),
                    ThresholdDayStart = table.Column<int>(type: "int", nullable: false),
                    ThresholdDayEnd = table.Column<int>(type: "int", nullable: false),
                    FractionRate = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CenterProratedTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CenterProratedTiers_CenterConfigurations_CenterConfigurationId",
                        column: x => x.CenterConfigurationId,
                        principalTable: "CenterConfigurations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CenterConfigurations_CenterId",
                table: "CenterConfigurations",
                column: "CenterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CenterProratedTiers_ConfigId_TierNumber",
                table: "CenterProratedTiers",
                columns: new[] { "CenterConfigurationId", "TierNumber" },
                unique: true);

            // ── Backfill a system-default configuration row (+ the 3 default prorated tiers) for every
            // existing center, so a center that predates this feature already has a usable template.
            //
            // Wrapped in EXEC(N'…') so name resolution is DEFERRED to run time (BUG-10): EF emits a
            // migration's ops as ONE GO-less batch (and the idempotent migrate.sql keeps them in one
            // batch), so a bare INSERT referencing the just-created tables would fail at batch-compile
            // ("invalid object name"). WHERE NOT EXISTS makes both inserts idempotent (safe re-run and
            // safe alongside the lazy-create in CenterService.EnsureCenterConfigAsync). Enum columns use
            // the 1-based CLR values (GenerationMode.Auto=1, GenerationLanguage.English=1,
            // BarcodeDisplayMode.InApp=1); StudentCodeGenerationMode is copied from the authoritative
            // Center column so the stored mirror matches from day one. On a fresh DB (no centers) both
            // inserts touch 0 rows.
            migrationBuilder.Sql(@"EXEC(N'
INSERT INTO [CenterConfigurations]
    ([CenterId],[StudentCodeGenerationMode],[StudentCodeLanguage],[SessionNameMode],[SessionNameLanguage],
     [IsProratedPaymentEnabled],[ConsecutiveAbsenceThreshold],[ConsecutiveUnpaidThreshold],[BarcodeDisplayMode],
     [StudentVisibilityAttendance],[StudentVisibilityPayment],[StudentVisibilityHomework],[StudentVisibilityExamDefault],
     [StudentVisibilityOnlineExamDefault],[StudentVisibilityVideo],
     [ParentVisibilityAttendance],[ParentVisibilityPayment],[ParentVisibilityHomework],[ParentVisibilityExamDefault],
     [ParentVisibilityOnlineExamDefault],[ParentVisibilityVideo],
     [IsDeviceLockEnabled],[ShowPaymentInfoOnAttendanceScreen],[ShowAttendanceHistoryOnAttendanceScreen],
     [UpdatedAt],[CreateAt])
SELECT c.[Id], c.[StudentCodeGenerationMode], 1, 1, 1,
       0, 3, 3, 1,
       1, 1, 1, 1,
       1, 1,
       1, 1, 1, 0,
       0, 1,
       0, 1, 1,
       NULL, SYSUTCDATETIME()
FROM [Centers] c
WHERE NOT EXISTS (SELECT 1 FROM [CenterConfigurations] cc WHERE cc.[CenterId] = c.[Id]);');");

            migrationBuilder.Sql(@"EXEC(N'
INSERT INTO [CenterProratedTiers]
    ([CenterConfigurationId],[TierNumber],[ThresholdDayStart],[ThresholdDayEnd],[FractionRate],[CreateAt])
SELECT cc.[Id], v.[TierNumber], v.[S], v.[E], v.[F], SYSUTCDATETIME()
FROM [CenterConfigurations] cc
CROSS APPLY (VALUES (1, 1, 10, CAST(1.0000 AS decimal(5,4))),
                    (2, 11, 20, CAST(0.6667 AS decimal(5,4))),
                    (3, 21, 31, CAST(0.3333 AS decimal(5,4)))) v([TierNumber],[S],[E],[F])
WHERE NOT EXISTS (SELECT 1 FROM [CenterProratedTiers] t
                  WHERE t.[CenterConfigurationId] = cc.[Id] AND t.[TierNumber] = v.[TierNumber]);');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CenterProratedTiers");

            migrationBuilder.DropTable(
                name: "CenterConfigurations");
        }
    }
}
