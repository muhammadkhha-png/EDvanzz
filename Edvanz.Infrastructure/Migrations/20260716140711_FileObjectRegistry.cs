using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FileObjectRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoAttachments");

            migrationBuilder.DropColumn(
                name: "ThumbnailBlobPath",
                table: "VideoAssets");

            migrationBuilder.DropColumn(
                name: "IdImage",
                table: "Users");

            migrationBuilder.AddColumn<long>(
                name: "ImageFileId",
                table: "VideoExamQuestions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ThumbnailFileId",
                table: "VideoAssets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IdImageFileId",
                table: "Users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ImageFileId",
                table: "OnlineExamQuestions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FileObjects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: true),
                    Category = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    BlobPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    OriginalName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    VideoAssetId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileObjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileObjects_VideoAssets_VideoAssetId",
                        column: x => x.VideoAssetId,
                        principalTable: "VideoAssets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoExamQuestions_ImageFileId",
                table: "VideoExamQuestions",
                column: "ImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAssets_ThumbnailFileId",
                table: "VideoAssets",
                column: "ThumbnailFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IdImageFileId",
                table: "Users",
                column: "IdImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineExamQuestions_ImageFileId",
                table: "OnlineExamQuestions",
                column: "ImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_FileObjects_Status",
                table: "FileObjects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FileObjects_VideoAssetId",
                table: "FileObjects",
                column: "VideoAssetId");

            migrationBuilder.CreateIndex(
                name: "UX_FileObjects_PublicId",
                table: "FileObjects",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OnlineExamQuestions_FileObjects_ImageFileId",
                table: "OnlineExamQuestions",
                column: "ImageFileId",
                principalTable: "FileObjects",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_FileObjects_IdImageFileId",
                table: "Users",
                column: "IdImageFileId",
                principalTable: "FileObjects",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoAssets_FileObjects_ThumbnailFileId",
                table: "VideoAssets",
                column: "ThumbnailFileId",
                principalTable: "FileObjects",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoExamQuestions_FileObjects_ImageFileId",
                table: "VideoExamQuestions",
                column: "ImageFileId",
                principalTable: "FileObjects",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnlineExamQuestions_FileObjects_ImageFileId",
                table: "OnlineExamQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_FileObjects_IdImageFileId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoAssets_FileObjects_ThumbnailFileId",
                table: "VideoAssets");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoExamQuestions_FileObjects_ImageFileId",
                table: "VideoExamQuestions");

            migrationBuilder.DropTable(
                name: "FileObjects");

            migrationBuilder.DropIndex(
                name: "IX_VideoExamQuestions_ImageFileId",
                table: "VideoExamQuestions");

            migrationBuilder.DropIndex(
                name: "IX_VideoAssets_ThumbnailFileId",
                table: "VideoAssets");

            migrationBuilder.DropIndex(
                name: "IX_Users_IdImageFileId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_OnlineExamQuestions_ImageFileId",
                table: "OnlineExamQuestions");

            migrationBuilder.DropColumn(
                name: "ImageFileId",
                table: "VideoExamQuestions");

            migrationBuilder.DropColumn(
                name: "ThumbnailFileId",
                table: "VideoAssets");

            migrationBuilder.DropColumn(
                name: "IdImageFileId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ImageFileId",
                table: "OnlineExamQuestions");

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailBlobPath",
                table: "VideoAssets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "IdImage",
                table: "Users",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VideoAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    VideoAssetId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    BlobPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false)
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
        }
    }
}
