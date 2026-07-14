using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class onlineexam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OnlineExams",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherSubjectId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Instructions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartDateTime = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    EndDateTime = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    PassPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Visibility = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnlineExams", x => x.Id);
                    table.UniqueConstraint("AK_OnlineExams_Id_TeacherId", x => new { x.Id, x.TeacherId });
                    table.CheckConstraint("CK_OnlineExams_DateOrder", "[StartDateTime] < [EndDateTime]");
                    table.CheckConstraint("CK_OnlineExams_PassPercentageRange", "[PassPercentage] >= 0 AND [PassPercentage] <= 100");
                    table.ForeignKey(
                        name: "FK_OnlineExams_TeacherSubjects_TeacherSubjectId",
                        column: x => x.TeacherSubjectId,
                        principalTable: "TeacherSubjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnlineExams_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OnlineExams_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnlineExams_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OnlineExamQuestions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OnlineExamId = table.Column<long>(type: "bigint", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuestionType = table.Column<byte>(type: "tinyint", nullable: false),
                    Degree = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnlineExamQuestions", x => x.Id);
                    table.CheckConstraint("CK_OnlineExamQuestions_DegreePositive", "[Degree] > 0");
                    table.ForeignKey(
                        name: "FK_OnlineExamQuestions_OnlineExams_OnlineExamId",
                        column: x => x.OnlineExamId,
                        principalTable: "OnlineExams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OnlineExamScopes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OnlineExamId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    ScopeType = table.Column<byte>(type: "tinyint", nullable: false),
                    SessionId = table.Column<long>(type: "bigint", nullable: true),
                    SessionGroupId = table.Column<long>(type: "bigint", nullable: true),
                    AssignedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnlineExamScopes", x => x.Id);
                    table.CheckConstraint("CK_OnlineExamScopes_ExactlyOneTarget", "(CASE WHEN [SessionId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [SessionGroupId] IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.CheckConstraint("CK_OnlineExamScopes_ScopeTypeMatchesFK", "([ScopeType] = 1 AND [SessionId] IS NOT NULL) OR ([ScopeType] = 2 AND [SessionGroupId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_OnlineExamScopes_OnlineExams_OnlineExamId_TeacherId",
                        columns: x => new { x.OnlineExamId, x.TeacherId },
                        principalTable: "OnlineExams",
                        principalColumns: new[] { "Id", "TeacherId" });
                    table.ForeignKey(
                        name: "FK_OnlineExamScopes_SessionGroups_SessionGroupId",
                        column: x => x.SessionGroupId,
                        principalTable: "SessionGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OnlineExamScopes_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OnlineExamScopes_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OnlineExamScopes_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentOnlineExamReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OnlineExamId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_StudentOnlineExamReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentOnlineExamReports_OnlineExams_OnlineExamId",
                        column: x => x.OnlineExamId,
                        principalTable: "OnlineExams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentOnlineExamReports_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentOnlineExamReports_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OnlineExamQuestionOptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionId = table.Column<long>(type: "bigint", nullable: false),
                    OptionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnlineExamQuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnlineExamQuestionOptions_OnlineExamQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "OnlineExamQuestions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StudentQuestionAnswers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentReportId = table.Column<long>(type: "bigint", nullable: false),
                    QuestionId = table.Column<long>(type: "bigint", nullable: false),
                    AwardedDegree = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentQuestionAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentQuestionAnswers_OnlineExamQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "OnlineExamQuestions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentQuestionAnswers_StudentOnlineExamReports_StudentReportId",
                        column: x => x.StudentReportId,
                        principalTable: "StudentOnlineExamReports",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StudentQuestionAnswerOptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentQuestionAnswerId = table.Column<long>(type: "bigint", nullable: false),
                    QuestionOptionId = table.Column<long>(type: "bigint", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentQuestionAnswerOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentQuestionAnswerOptions_OnlineExamQuestionOptions_QuestionOptionId",
                        column: x => x.QuestionOptionId,
                        principalTable: "OnlineExamQuestionOptions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentQuestionAnswerOptions_StudentQuestionAnswers_StudentQuestionAnswerId",
                        column: x => x.StudentQuestionAnswerId,
                        principalTable: "StudentQuestionAnswers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnlineExamQuestionOptions_QuestionId_SortOrder",
                table: "OnlineExamQuestionOptions",
                columns: new[] { "QuestionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_OnlineExamQuestions_OnlineExamId_SortOrder",
                table: "OnlineExamQuestions",
                columns: new[] { "OnlineExamId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_OnlineExams_CreatedByUserId",
                table: "OnlineExams",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineExams_TeacherId_Status",
                table: "OnlineExams",
                columns: new[] { "TeacherId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OnlineExams_TeacherId_Title",
                table: "OnlineExams",
                columns: new[] { "TeacherId", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_OnlineExams_TeacherSubjectId",
                table: "OnlineExams",
                column: "TeacherSubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineExams_UpdatedByUserId",
                table: "OnlineExams",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_OnlineExams_Id_TeacherId",
                table: "OnlineExams",
                columns: new[] { "Id", "TeacherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnlineExamScopes_AssignedByUserId",
                table: "OnlineExamScopes",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineExamScopes_OnlineExamId",
                table: "OnlineExamScopes",
                column: "OnlineExamId")
                .Annotation("SqlServer:Include", new[] { "ScopeType", "SessionId", "SessionGroupId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OnlineExamScopes_OnlineExamId_TeacherId",
                table: "OnlineExamScopes",
                columns: new[] { "OnlineExamId", "TeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_OnlineExamScopes_SessionGroupId",
                table: "OnlineExamScopes",
                column: "SessionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineExamScopes_SessionId",
                table: "OnlineExamScopes",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineExamScopes_TeacherId",
                table: "OnlineExamScopes",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "UX_OnlineExamScopes_Exam_Type_Target",
                table: "OnlineExamScopes",
                columns: new[] { "OnlineExamId", "ScopeType", "SessionId", "SessionGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentOnlineExamReports_OnlineExamId_Status",
                table: "StudentOnlineExamReports",
                columns: new[] { "OnlineExamId", "Status" })
                .Annotation("SqlServer:Include", new[] { "Percentage" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentOnlineExamReports_OnlineExamId_SubmittedAt",
                table: "StudentOnlineExamReports",
                columns: new[] { "OnlineExamId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentOnlineExamReports_TeacherId",
                table: "StudentOnlineExamReports",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentOnlineExamReports_TeacherStudentId",
                table: "StudentOnlineExamReports",
                column: "TeacherStudentId");

            migrationBuilder.CreateIndex(
                name: "UX_StudentOnlineExamReports_Exam_Student",
                table: "StudentOnlineExamReports",
                columns: new[] { "OnlineExamId", "TeacherStudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentQuestionAnswerOptions_QuestionOptionId",
                table: "StudentQuestionAnswerOptions",
                column: "QuestionOptionId");

            migrationBuilder.CreateIndex(
                name: "UX_StudentQuestionAnswerOptions_Answer_Option",
                table: "StudentQuestionAnswerOptions",
                columns: new[] { "StudentQuestionAnswerId", "QuestionOptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentQuestionAnswers_QuestionId",
                table: "StudentQuestionAnswers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "UX_StudentQuestionAnswers_Report_Question",
                table: "StudentQuestionAnswers",
                columns: new[] { "StudentReportId", "QuestionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnlineExamScopes");

            migrationBuilder.DropTable(
                name: "StudentQuestionAnswerOptions");

            migrationBuilder.DropTable(
                name: "OnlineExamQuestionOptions");

            migrationBuilder.DropTable(
                name: "StudentQuestionAnswers");

            migrationBuilder.DropTable(
                name: "OnlineExamQuestions");

            migrationBuilder.DropTable(
                name: "StudentOnlineExamReports");

            migrationBuilder.DropTable(
                name: "OnlineExams");
        }
    }
}
