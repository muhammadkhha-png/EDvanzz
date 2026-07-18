using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StudentVideoExamReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentVideoExamReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VideoAssetId = table.Column<long>(type: "bigint", nullable: false),
                    VideoExamId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentVideoExamReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentVideoExamReports_VideoAssets_VideoAssetId",
                        column: x => x.VideoAssetId,
                        principalTable: "VideoAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentVideoExamAnswers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentVideoExamReportId = table.Column<long>(type: "bigint", nullable: false),
                    VideoExamQuestionId = table.Column<long>(type: "bigint", nullable: false),
                    AwardedDegree = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentVideoExamAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentVideoExamAnswers_StudentVideoExamReports_StudentVideoExamReportId",
                        column: x => x.StudentVideoExamReportId,
                        principalTable: "StudentVideoExamReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentVideoExamAnswerOptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentVideoExamAnswerId = table.Column<long>(type: "bigint", nullable: false),
                    VideoExamQuestionOptionId = table.Column<long>(type: "bigint", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentVideoExamAnswerOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentVideoExamAnswerOptions_StudentVideoExamAnswers_StudentVideoExamAnswerId",
                        column: x => x.StudentVideoExamAnswerId,
                        principalTable: "StudentVideoExamAnswers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_StudentVideoExamAnswerOptions_Answer_Option",
                table: "StudentVideoExamAnswerOptions",
                columns: new[] { "StudentVideoExamAnswerId", "VideoExamQuestionOptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_StudentVideoExamAnswers_Report_Question",
                table: "StudentVideoExamAnswers",
                columns: new[] { "StudentVideoExamReportId", "VideoExamQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_StudentVideoExamReports_Video_Student",
                table: "StudentVideoExamReports",
                columns: new[] { "VideoAssetId", "TeacherStudentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentVideoExamAnswerOptions");

            migrationBuilder.DropTable(
                name: "StudentVideoExamAnswers");

            migrationBuilder.DropTable(
                name: "StudentVideoExamReports");
        }
    }
}
