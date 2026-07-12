using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VCMPhase1schemachanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDurationManuallySet",
                table: "VideoAssets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishDate",
                table: "VideoAssets",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "VideoAssets",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte>(
                name: "Status",
                table: "VideoAssets",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailBlobPath",
                table: "VideoAssets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "VideoAssets",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VideoAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VideoAssetId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    BlobPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VideoAttachments_VideoAssets_VideoAssetId_TeacherId",
                        columns: x => new { x.VideoAssetId, x.TeacherId },
                        principalTable: "VideoAssets",
                        principalColumns: new[] { "Id", "TeacherId" });
                });

            migrationBuilder.CreateTable(
                name: "VideoUnits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoUnits", x => x.Id);
                    table.UniqueConstraint("AK_VideoUnits_Id_TeacherId", x => new { x.Id, x.TeacherId });
                    table.ForeignKey(
                        name: "FK_VideoUnits_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VideoUnits_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VideoAssetUnits",
                columns: table => new
                {
                    VideoAssetId = table.Column<long>(type: "bigint", nullable: false),
                    UnitId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoAssetUnits", x => new { x.VideoAssetId, x.UnitId });
                    table.ForeignKey(
                        name: "FK_VideoAssetUnits_VideoAssets_VideoAssetId",
                        column: x => x.VideoAssetId,
                        principalTable: "VideoAssets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VideoAssetUnits_VideoUnits_UnitId",
                        column: x => x.UnitId,
                        principalTable: "VideoUnits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VideoUnitScopes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VideoUnitId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    ScopeType = table.Column<byte>(type: "tinyint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: true),
                    SessionId = table.Column<long>(type: "bigint", nullable: true),
                    SessionGroupId = table.Column<long>(type: "bigint", nullable: true),
                    AssignedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoUnitScopes", x => x.Id);
                    table.CheckConstraint("CK_VideoUnitScopes_ExactlyOneTarget", "(CASE WHEN [TeacherStudentId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [SessionId]        IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [SessionGroupId]   IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.CheckConstraint("CK_VideoUnitScopes_ScopeTypeMatchesFK", "([ScopeType] = 0 AND [TeacherStudentId] IS NOT NULL) OR ([ScopeType] = 1 AND [SessionId] IS NOT NULL) OR ([ScopeType] = 2 AND [SessionGroupId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_VideoUnitScopes_SessionGroups_SessionGroupId",
                        column: x => x.SessionGroupId,
                        principalTable: "SessionGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VideoUnitScopes_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VideoUnitScopes_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VideoUnitScopes_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoUnitScopes_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VideoUnitScopes_VideoUnits_VideoUnitId_TeacherId",
                        columns: x => new { x.VideoUnitId, x.TeacherId },
                        principalTable: "VideoUnits",
                        principalColumns: new[] { "Id", "TeacherId" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoAssetUnits_UnitId",
                table: "VideoAssetUnits",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAttachments_UploadedByUserId",
                table: "VideoAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAttachments_VideoAssetId",
                table: "VideoAttachments",
                column: "VideoAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAttachments_VideoAssetId_TeacherId",
                table: "VideoAttachments",
                columns: new[] { "VideoAssetId", "TeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoUnits_CreatedByUserId",
                table: "VideoUnits",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoUnits_TeacherId_CreatedAt",
                table: "VideoUnits",
                columns: new[] { "TeacherId", "CreateAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "UX_VideoUnits_Id_TeacherId",
                table: "VideoUnits",
                columns: new[] { "Id", "TeacherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoUnitScopes_AssignedByUserId",
                table: "VideoUnitScopes",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoUnitScopes_SessionGroupId",
                table: "VideoUnitScopes",
                column: "SessionGroupId",
                filter: "[SessionGroupId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "VideoUnitId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoUnitScopes_SessionId",
                table: "VideoUnitScopes",
                column: "SessionId",
                filter: "[SessionId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "VideoUnitId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoUnitScopes_TeacherId",
                table: "VideoUnitScopes",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoUnitScopes_TeacherStudentId",
                table: "VideoUnitScopes",
                column: "TeacherStudentId",
                filter: "[TeacherStudentId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "VideoUnitId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoUnitScopes_VideoUnitId",
                table: "VideoUnitScopes",
                column: "VideoUnitId")
                .Annotation("SqlServer:Include", new[] { "ScopeType", "TeacherStudentId", "SessionId", "SessionGroupId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoUnitScopes_VideoUnitId_TeacherId",
                table: "VideoUnitScopes",
                columns: new[] { "VideoUnitId", "TeacherId" });

            migrationBuilder.CreateIndex(
                name: "UX_VideoUnitScopes_Unit_Type_Target",
                table: "VideoUnitScopes",
                columns: new[] { "VideoUnitId", "ScopeType", "TeacherStudentId", "SessionId", "SessionGroupId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoAssetUnits");

            migrationBuilder.DropTable(
                name: "VideoAttachments");

            migrationBuilder.DropTable(
                name: "VideoUnitScopes");

            migrationBuilder.DropTable(
                name: "VideoUnits");

            migrationBuilder.DropColumn(
                name: "IsDurationManuallySet",
                table: "VideoAssets");

            migrationBuilder.DropColumn(
                name: "PublishDate",
                table: "VideoAssets");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "VideoAssets");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "VideoAssets");

            migrationBuilder.DropColumn(
                name: "ThumbnailBlobPath",
                table: "VideoAssets");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "VideoAssets");
        }
    }
}
