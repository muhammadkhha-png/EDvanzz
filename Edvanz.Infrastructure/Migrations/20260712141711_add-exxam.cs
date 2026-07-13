using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addexxam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoExams",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VideoAssetId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoExams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoExams_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VideoExams_VideoAssets_VideoAssetId_TeacherId",
                        columns: x => new { x.VideoAssetId, x.TeacherId },
                        principalTable: "VideoAssets",
                        principalColumns: new[] { "Id", "TeacherId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoExamQuestions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExamId = table.Column<long>(type: "bigint", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    QuestionType = table.Column<byte>(type: "tinyint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoExamQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoExamQuestions_VideoExams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "VideoExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoExamQuestionOptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionId = table.Column<long>(type: "bigint", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoExamQuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoExamQuestionOptions_VideoExamQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "VideoExamQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoExamQuestionOptions_QuestionId",
                table: "VideoExamQuestionOptions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoExamQuestions_ExamId",
                table: "VideoExamQuestions",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoExams_CreatedByUserId",
                table: "VideoExams",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoExams_VideoAssetId_TeacherId",
                table: "VideoExams",
                columns: new[] { "VideoAssetId", "TeacherId" });

            migrationBuilder.CreateIndex(
                name: "UX_VideoExams_VideoAssetId",
                table: "VideoExams",
                column: "VideoAssetId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoExamQuestionOptions");

            migrationBuilder.DropTable(
                name: "VideoExamQuestions");

            migrationBuilder.DropTable(
                name: "VideoExams");
        }
    }
}
