using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameThumbnailToVideoPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VideoAssets_FileObjects_ThumbnailFileId",
                table: "VideoAssets");

            migrationBuilder.RenameColumn(
                name: "ThumbnailFileId",
                table: "VideoAssets",
                newName: "VideoPhotoFileId");

            migrationBuilder.RenameIndex(
                name: "IX_VideoAssets_ThumbnailFileId",
                table: "VideoAssets",
                newName: "IX_VideoAssets_VideoPhotoFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoAssets_FileObjects_VideoPhotoFileId",
                table: "VideoAssets",
                column: "VideoPhotoFileId",
                principalTable: "FileObjects",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VideoAssets_FileObjects_VideoPhotoFileId",
                table: "VideoAssets");

            migrationBuilder.RenameColumn(
                name: "VideoPhotoFileId",
                table: "VideoAssets",
                newName: "ThumbnailFileId");

            migrationBuilder.RenameIndex(
                name: "IX_VideoAssets_VideoPhotoFileId",
                table: "VideoAssets",
                newName: "IX_VideoAssets_ThumbnailFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoAssets_FileObjects_ThumbnailFileId",
                table: "VideoAssets",
                column: "ThumbnailFileId",
                principalTable: "FileObjects",
                principalColumn: "Id");
        }
    }
}
