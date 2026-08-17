using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentForgiveness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ForgivenAmount",
                table: "PaymentPeriods",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentForgivenesses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: true),
                    SessionId = table.Column<long>(type: "bigint", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ForgivenByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ForgivenAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    ReversedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    ReversalNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StudentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StudentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SessionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentForgivenesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentForgivenesses_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentForgivenesses_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentForgivenessAllocations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentForgivenessId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentPeriodId = table.Column<long>(type: "bigint", nullable: true),
                    AmountForgiven = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentForgivenessAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentForgivenessAllocations_PaymentForgivenesses_PaymentForgivenessId",
                        column: x => x.PaymentForgivenessId,
                        principalTable: "PaymentForgivenesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentForgivenessAllocations_PaymentPeriods_PaymentPeriodId",
                        column: x => x.PaymentPeriodId,
                        principalTable: "PaymentPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PFA_PaymentForgivenessId",
                table: "PaymentForgivenessAllocations",
                column: "PaymentForgivenessId");

            migrationBuilder.CreateIndex(
                name: "IX_PFA_PaymentPeriodId",
                table: "PaymentForgivenessAllocations",
                column: "PaymentPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentForgivenesses_TeacherStudentId",
                table: "PaymentForgivenesses",
                column: "TeacherStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_PF_TeacherId_StudentId",
                table: "PaymentForgivenesses",
                columns: new[] { "TeacherId", "TeacherStudentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentForgivenessAllocations");

            migrationBuilder.DropTable(
                name: "PaymentForgivenesses");

            migrationBuilder.DropColumn(
                name: "ForgivenAmount",
                table: "PaymentPeriods");
        }
    }
}
