using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Models",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Models", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserType = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PasswordHashed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdImage = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: true),
                    CreateByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: true),
                    OtpCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OtpExpiry = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Users_CreateByUserId",
                        column: x => x.CreateByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModuleId = table.Column<long>(type: "bigint", nullable: false),
                    IsRestricted = table.Column<bool>(type: "bit", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Permissions_Models_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "GoogleUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoogleId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoogleUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ParentUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    LanguagePreference = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    AccountStatus = table.Column<int>(type: "int", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "StudentCapacityPackages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MinStudents = table.Column<int>(type: "int", nullable: false),
                    MaxStudents = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    MonthlyPriceEGP = table.Column<decimal>(type: "decimal(10,2)", nullable: false, defaultValue: 0m),
                    PriceUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PriceUpdatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCapacityPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentCapacityPackages_Users_PriceUpdatedByUserId",
                        column: x => x.PriceUpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StudentUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    StudentAccountCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LanguagePreference = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    AccountStatus = table.Column<int>(type: "int", nullable: false),
                    IsFirstLogin = table.Column<bool>(type: "bit", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserDeviceTokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    FcmToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Platform = table.Column<byte>(type: "tinyint", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDeviceTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDeviceTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DeepLinkPayload = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Category = table.Column<byte>(type: "tinyint", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserTutor",
                columns: table => new
                {
                    userId = table.Column<long>(type: "bigint", nullable: false),
                    TutorId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTutor", x => new { x.userId, x.TutorId });
                    table.ForeignKey(
                        name: "FK_UserTutor_Users_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_UserTutor_Users_userId",
                        column: x => x.userId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "UsersPermissions",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    PermissionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsersPermissions", x => new { x.UserId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_UsersPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_UsersPermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    StudentCapacityPackageId = table.Column<long>(type: "bigint", nullable: true),
                    StudentCapacity = table.Column<int>(type: "int", nullable: false),
                    LanguagePreference = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    CustomSubject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AccountStatus = table.Column<int>(type: "int", nullable: false),
                    IsConfigurationCompleted = table.Column<bool>(type: "bit", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teachers_StudentCapacityPackages_StudentCapacityPackageId",
                        column: x => x.StudentCapacityPackageId,
                        principalTable: "StudentCapacityPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Teachers_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Teachers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParentChildren",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentUserId = table.Column<long>(type: "bigint", nullable: false),
                    LinkMethod = table.Column<int>(type: "int", nullable: false),
                    StudentUserId = table.Column<long>(type: "bigint", nullable: true),
                    ChildName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentChildren", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentChildren_ParentUsers_ParentUserId",
                        column: x => x.ParentUserId,
                        principalTable: "ParentUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ParentChildren_StudentUsers_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "StudentUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    AssignmentType = table.Column<byte>(type: "tinyint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RecurrencePattern = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    RecurrenceEndDate = table.Column<DateTime>(type: "date", nullable: true),
                    IsRecurrenceStopped = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TrackingMode = table.Column<byte>(type: "tinyint", nullable: true),
                    MaxGrade = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    PassingThreshold = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentTemplates_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssignmentTemplates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Assistants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherAccountId = table.Column<long>(type: "bigint", nullable: false),
                    LanguagePreference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AccountStatus = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assistants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assistants_Teachers_TeacherAccountId",
                        column: x => x.TeacherAccountId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Assistants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MessageTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    RecipientTarget = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageTemplates_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "MessagingChannels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    ChannelType = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EncryptedCredentials = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SenderIdOrNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConnectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessagingChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessagingChannels_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "PaymentEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EventAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TargetScopeType = table.Column<byte>(type: "tinyint", nullable: false),
                    TargetScopeIds = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EventDate = table.Column<DateTime>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TotalStudents = table.Column<int>(type: "int", nullable: false),
                    TotalExpectedRevenue = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    TotalCollectedRevenue = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentEvents_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PendingSubscriptionPayments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    PaymentChannel = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    AmountEGP = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    PaymobSessionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubmittedTransactionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EncryptedSubmittedDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InitiatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingSubscriptionPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingSubscriptionPayments_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PendingSubscriptionPayments_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SessionGroups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionGroups_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionAlerts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    SubscriptionEndDate = table.Column<DateTime>(type: "date", nullable: false),
                    AlertDay = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChannelsSent = table.Column<byte>(type: "tinyint", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionAlerts_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TeacherConfigurations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
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
                    ParentVisibilityAttendance = table.Column<bool>(type: "bit", nullable: false),
                    ParentVisibilityPayment = table.Column<bool>(type: "bit", nullable: false),
                    ParentVisibilityHomework = table.Column<bool>(type: "bit", nullable: false),
                    ParentVisibilityExamDefault = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherConfigurations_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TeacherSubjects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    SubjectId = table.Column<long>(type: "bigint", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherSubjects_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherSubjects_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TeacherSubscriptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    PaymentMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    PaymentChannel = table.Column<byte>(type: "tinyint", nullable: false),
                    AmountPaidEGP = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TransactionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EncryptedPaymentDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherSubscriptions_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeacherSubscriptions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Templates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Templates_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "TutorModuleAccess",
                columns: table => new
                {
                    TutorId = table.Column<long>(type: "bigint", nullable: false),
                    ModuleId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorModuleAccess", x => new { x.TutorId, x.ModuleId });
                    table.ForeignKey(
                        name: "FK_TutorModuleAccess_Models_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_TutorModuleAccess_Teachers_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "VideoAssetAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VideoAssetId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotArchiveUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoAssetAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoAssetAudits_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VideoAssetAudits_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VideoAssets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceType = table.Column<byte>(type: "tinyint", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoAssets", x => x.Id);
                    table.UniqueConstraint("AK_VideoAssets_Id_TeacherId", x => new { x.Id, x.TeacherId });
                    table.ForeignKey(
                        name: "FK_VideoAssets_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VideoAssets_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentOccurrences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    OccurrenceNumber = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    MaxGradeSnapshot = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    PassingThresholdSnapshot = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    TrackingModeSnapshot = table.Column<byte>(type: "tinyint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TotalStudentCount = table.Column<int>(type: "int", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentOccurrences_AssignmentTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "AssignmentTemplates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssignmentOccurrences_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AssistantLoginActivity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssistantId = table.Column<long>(type: "bigint", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeviceOrBrowser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantLoginActivity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantLoginActivity_Assistants_AssistantId",
                        column: x => x.AssistantId,
                        principalTable: "Assistants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "AssistantWallets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    AssistantId = table.Column<long>(type: "bigint", nullable: false),
                    AssistantUserId = table.Column<long>(type: "bigint", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    TotalCollected = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    LastCollectionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantWallets_Assistants_AssistantId",
                        column: x => x.AssistantId,
                        principalTable: "Assistants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssistantWallets_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AuditTrial",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    teacherId = table.Column<long>(type: "bigint", nullable: false),
                    AssistantId = table.Column<long>(type: "bigint", nullable: true),
                    actionType = table.Column<int>(type: "int", nullable: false),
                    ModuleId = table.Column<long>(type: "bigint", nullable: false),
                    Desc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTrial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditTrial_Assistants_AssistantId",
                        column: x => x.AssistantId,
                        principalTable: "Assistants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AuditTrial_Models_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_AuditTrial_Teachers_teacherId",
                        column: x => x.teacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "AutomatedTriggers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    MessageTemplateId = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SendTiming = table.Column<int>(type: "int", nullable: false),
                    ScheduledTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<long>(type: "bigint", nullable: true),
                    SessionGroupId = table.Column<long>(type: "bigint", nullable: true),
                    ThresholdValue = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomatedTriggers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomatedTriggers_MessageTemplates_MessageTemplateId",
                        column: x => x.MessageTemplateId,
                        principalTable: "MessageTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_AutomatedTriggers_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "MessageBlocks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageTemplateId = table.Column<long>(type: "bigint", nullable: false),
                    BlockType = table.Column<int>(type: "int", nullable: false),
                    DynamicKey = table.Column<int>(type: "int", nullable: true),
                    CustomText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageBlocks_MessageTemplates_MessageTemplateId",
                        column: x => x.MessageTemplateId,
                        principalTable: "MessageTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "MessageLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    StudentId = table.Column<long>(type: "bigint", nullable: false),
                    StudentCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StudentName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecipientPhone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecipientType = table.Column<int>(type: "int", nullable: false),
                    MessageTemplateId = table.Column<long>(type: "bigint", nullable: true),
                    ResolvedContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TriggerType = table.Column<int>(type: "int", nullable: true),
                    IsManual = table.Column<bool>(type: "bit", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageLogs_MessageTemplates_MessageTemplateId",
                        column: x => x.MessageTemplateId,
                        principalTable: "MessageTemplates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MessageLogs_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    SessionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OccurrenceType = table.Column<int>(type: "int", nullable: false),
                    SelectedDays = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    MonthlyDayOfMonth = table.Column<byte>(type: "tinyint", nullable: true),
                    PaymentType = table.Column<int>(type: "int", nullable: false),
                    SessionAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    DurationMinutes = table.Column<short>(type: "smallint", nullable: false),
                    SessionGroupId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_SessionGroups_SessionGroupId",
                        column: x => x.SessionGroupId,
                        principalTable: "SessionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Sessions_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TeacherProratedTiers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherConfigurationId = table.Column<long>(type: "bigint", nullable: false),
                    TierNumber = table.Column<int>(type: "int", nullable: false),
                    ThresholdDayStart = table.Column<int>(type: "int", nullable: false),
                    ThresholdDayEnd = table.Column<int>(type: "int", nullable: false),
                    FractionRate = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherProratedTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherProratedTiers_TeacherConfigurations_TeacherConfigurationId",
                        column: x => x.TeacherConfigurationId,
                        principalTable: "TeacherConfigurations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TemplatesPermisions",
                columns: table => new
                {
                    TemplateId = table.Column<long>(type: "bigint", nullable: false),
                    PermisionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplatesPermisions", x => new { x.PermisionId, x.TemplateId });
                    table.ForeignKey(
                        name: "FK_TemplatesPermisions_Permissions_PermisionId",
                        column: x => x.PermisionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_TemplatesPermisions_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentDeletionLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    DeletionType = table.Column<byte>(type: "tinyint", nullable: false),
                    StudentsAffected = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TemplateSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    LastOccurrenceId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentDeletionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentDeletionLogs_AssignmentOccurrences_LastOccurrenceId",
                        column: x => x.LastOccurrenceId,
                        principalTable: "AssignmentOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssignmentDeletionLogs_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssignmentDeletionLogs_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WalletResetLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    AssistantId = table.Column<long>(type: "bigint", nullable: false),
                    AssistantWalletId = table.Column<long>(type: "bigint", nullable: false),
                    AmountReset = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    ResetByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ResetAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    AssistantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletResetLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletResetLogs_AssistantWallets_AssistantWalletId",
                        column: x => x.AssistantWalletId,
                        principalTable: "AssistantWallets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WalletResetLogs_Assistants_AssistantId",
                        column: x => x.AssistantId,
                        principalTable: "Assistants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WalletResetLogs_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SessionLinks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    LinkedSessionId = table.Column<long>(type: "bigint", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionLinks_Sessions_LinkedSessionId",
                        column: x => x.LinkedSessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionLinks_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SessionOccurrences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    OccurrenceDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionOccurrences_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SessionOccurrences_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TeacherStudents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    StudentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StudentCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    HashedToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StudentPhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ParentPhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SessionId = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherStudents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherStudents_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TeacherStudents_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TemplatesPermissionsOfUsers",
                columns: table => new
                {
                    TemplateId = table.Column<long>(type: "bigint", nullable: false),
                    AssisstantId = table.Column<long>(type: "bigint", nullable: false),
                    TemplatePermisionsPermisionId = table.Column<long>(type: "bigint", nullable: true),
                    TemplatePermisionsTemplateId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplatesPermissionsOfUsers", x => new { x.AssisstantId, x.TemplateId });
                    table.ForeignKey(
                        name: "FK_TemplatesPermissionsOfUsers_Assistants_AssisstantId",
                        column: x => x.AssisstantId,
                        principalTable: "Assistants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_TemplatesPermissionsOfUsers_TemplatesPermisions_TemplatePermisionsPermisionId_TemplatePermisionsTemplateId",
                        columns: x => new { x.TemplatePermisionsPermisionId, x.TemplatePermisionsTemplateId },
                        principalTable: "TemplatesPermisions",
                        principalColumns: new[] { "PermisionId", "TemplateId" });
                    table.ForeignKey(
                        name: "FK_TemplatesPermissionsOfUsers_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentScopes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    ScopeType = table.Column<byte>(type: "tinyint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: true),
                    SessionId = table.Column<long>(type: "bigint", nullable: true),
                    SessionGroupId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentScopes", x => x.Id);
                    table.CheckConstraint("CK_AssignmentScopes_ExactlyOneTarget", "(\r\n            CASE WHEN [TeacherStudentId] IS NULL THEN 0 ELSE 1 END\r\n          + CASE WHEN [SessionId]        IS NULL THEN 0 ELSE 1 END\r\n          + CASE WHEN [SessionGroupId]   IS NULL THEN 0 ELSE 1 END\r\n        ) = 1\r\n        AND (\r\n            ([ScopeType] = 0 AND [TeacherStudentId] IS NOT NULL)\r\n         OR ([ScopeType] = 1 AND [SessionId]        IS NOT NULL)\r\n         OR ([ScopeType] = 2 AND [SessionGroupId]   IS NOT NULL)\r\n        )");
                    table.ForeignKey(
                        name: "FK_AssignmentScopes_AssignmentTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "AssignmentTemplates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssignmentScopes_SessionGroups_SessionGroupId",
                        column: x => x.SessionGroupId,
                        principalTable: "SessionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssignmentScopes_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssignmentScopes_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssignmentScopes_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EventStudentObligations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentEventId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: true),
                    StudentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StudentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AmountDue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    PaymentStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventStudentObligations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventStudentObligations_PaymentEvents_PaymentEventId",
                        column: x => x.PaymentEventId,
                        principalTable: "PaymentEvents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EventStudentObligations_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EventStudentObligations_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ParentChildTeacherLinks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentChildId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: true),
                    LinkStatus = table.Column<int>(type: "int", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UnlinkedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentChildTeacherLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentChildTeacherLinks_ParentChildren_ParentChildId",
                        column: x => x.ParentChildId,
                        principalTable: "ParentChildren",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ParentChildTeacherLinks_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ParentChildTeacherLinks_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionTransferEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: true),
                    SourceSessionId = table.Column<long>(type: "bigint", nullable: true),
                    SourceSessionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DestinationSessionId = table.Column<long>(type: "bigint", nullable: true),
                    DestinationSessionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PaymentStatusAtTransfer = table.Column<byte>(type: "tinyint", nullable: false),
                    OutstandingBalance = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CreditBalance = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    SourcePaymentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DestinationPaymentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StudentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StudentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TransferredAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    TransferredByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionTransferEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionTransferEvents_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SessionTransferEvents_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StudentAbsenceCounters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: false),
                    ConsecutiveAbsences = table.Column<int>(type: "int", nullable: false),
                    TotalAbsences = table.Column<int>(type: "int", nullable: false),
                    TotalPresent = table.Column<int>(type: "int", nullable: false),
                    TotalOccurrences = table.Column<int>(type: "int", nullable: false),
                    LastAbsenceDate = table.Column<DateTime>(type: "date", nullable: true),
                    LastAbsenceSessionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastAbsenceSessionId = table.Column<long>(type: "bigint", nullable: true),
                    LastAttendanceDate = table.Column<DateTime>(type: "date", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAbsenceCounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAbsenceCounters_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentAbsenceCounters_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StudentAssignmentObligations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurrenceId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    GradeValue = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    IsGradeEntered = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MarkedByScan = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAssignmentObligations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAssignmentObligations_AssignmentOccurrences_OccurrenceId",
                        column: x => x.OccurrenceId,
                        principalTable: "AssignmentOccurrences",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentAssignmentObligations_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAssignmentObligations_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentAssignmentObligations_Users_LastUpdatedByUserId",
                        column: x => x.LastUpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StudentDepartures",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: true),
                    SessionId = table.Column<long>(type: "bigint", nullable: true),
                    SessionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StudentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StudentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PaymentStatusAtDeparture = table.Column<byte>(type: "tinyint", nullable: false),
                    TotalOccurrencesInPeriod = table.Column<int>(type: "int", nullable: false),
                    AttendedOccurrences = table.Column<int>(type: "int", nullable: false),
                    FullPeriodAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ProRatedAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    FinalAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IsTutorOverride = table.Column<bool>(type: "bit", nullable: false),
                    OriginalCalculatedAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DepartureOutcome = table.Column<byte>(type: "tinyint", nullable: false),
                    ConfirmedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    DepartedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentDepartures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentDepartures_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentDepartures_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudentDepartures_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StudentPaymentCounters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: false),
                    ConsecutiveUnpaid = table.Column<int>(type: "int", nullable: false),
                    TotalUnpaidPeriods = table.Column<int>(type: "int", nullable: false),
                    TotalPaidPeriods = table.Column<int>(type: "int", nullable: false),
                    TotalAmountPaid = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    TotalOutstanding = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    CustomPaymentAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    LastPaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPaymentSessionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPaymentCounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentPaymentCounters_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentPaymentCounters_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StudentSessionAssignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: true),
                    SessionId = table.Column<long>(type: "bigint", nullable: true),
                    SessionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StudentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StudentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UnassignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentSessionAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentSessionAssignments_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentSessionAssignments_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudentSessionAssignments_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StudentTeacherLinks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentUserId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: true),
                    LinkStatus = table.Column<int>(type: "int", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UnlinkedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentTeacherLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentTeacherLinks_StudentUsers_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "StudentUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentTeacherLinks_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudentTeacherLinks_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VideoAnalytics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VideoAssetId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: false),
                    OpenCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalWatchSeconds = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    FirstOpenedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    VideoDurationAtFirstWatch = table.Column<int>(type: "int", nullable: false),
                    LastResumePositionSeconds = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoAnalytics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoAnalytics_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VideoAnalytics_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_VideoAnalytics_VideoAssets_VideoAssetId_TeacherId",
                        columns: x => new { x.VideoAssetId, x.TeacherId },
                        principalTable: "VideoAssets",
                        principalColumns: new[] { "Id", "TeacherId" });
                });

            migrationBuilder.CreateTable(
                name: "VideoScopes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VideoAssetId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_VideoScopes", x => x.Id);
                    table.CheckConstraint("CK_VideoScopes_ExactlyOneTarget", "(CASE WHEN [TeacherStudentId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [SessionId]        IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [SessionGroupId]   IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.CheckConstraint("CK_VideoScopes_ScopeTypeMatchesFK", "([ScopeType] = 0 AND [TeacherStudentId] IS NOT NULL) OR ([ScopeType] = 1 AND [SessionId] IS NOT NULL) OR ([ScopeType] = 2 AND [SessionGroupId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_VideoScopes_SessionGroups_SessionGroupId",
                        column: x => x.SessionGroupId,
                        principalTable: "SessionGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VideoScopes_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VideoScopes_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VideoScopes_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_VideoScopes_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VideoScopes_VideoAssets_VideoAssetId_TeacherId",
                        columns: x => new { x.VideoAssetId, x.TeacherId },
                        principalTable: "VideoAssets",
                        principalColumns: new[] { "Id", "TeacherId" });
                });

            migrationBuilder.CreateTable(
                name: "VideoWatchEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VideoAssetId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventType = table.Column<byte>(type: "tinyint", nullable: false),
                    PositionSeconds = table.Column<int>(type: "int", nullable: false),
                    DeltaSinceLastSeconds = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    EventUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    ClientEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoWatchEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoWatchEvents_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VideoWatchEvents_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VideoWatchEvents_VideoAssets_VideoAssetId_TeacherId",
                        columns: x => new { x.VideoAssetId, x.TeacherId },
                        principalTable: "VideoAssets",
                        principalColumns: new[] { "Id", "TeacherId" });
                });

            migrationBuilder.CreateTable(
                name: "EventPaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentEventId = table.Column<long>(type: "bigint", nullable: true),
                    EventStudentObligationId = table.Column<long>(type: "bigint", nullable: true),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: true),
                    AmountPaid = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    PaymentMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    CollectedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    StudentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StudentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EventName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    IsOnlinePayment = table.Column<bool>(type: "bit", nullable: false),
                    OnlineTransactionRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventPaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventPaymentTransactions_EventStudentObligations_EventStudentObligationId",
                        column: x => x.EventStudentObligationId,
                        principalTable: "EventStudentObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EventPaymentTransactions_PaymentEvents_PaymentEventId",
                        column: x => x.PaymentEventId,
                        principalTable: "PaymentEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EventPaymentTransactions_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EventPaymentTransactions_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StudentObligationAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentObligationId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    OldStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    NewStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    OldGradeValue = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    NewGradeValue = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    MaxGradeSnapshot = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    PassingThresholdSnapshot = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentObligationAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentObligationAuditLogs_StudentAssignmentObligations_StudentObligationId",
                        column: x => x.StudentObligationId,
                        principalTable: "StudentAssignmentObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentObligationAuditLogs_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentObligationAuditLogs_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    SessionOccurrenceId = table.Column<long>(type: "bigint", nullable: true),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: true),
                    StudentSessionAssignmentId = table.Column<long>(type: "bigint", nullable: true),
                    StudentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StudentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SessionId = table.Column<long>(type: "bigint", nullable: true),
                    SessionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OccurrenceDate = table.Column<DateTime>(type: "date", nullable: false),
                    SessionGroupId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    AttendanceMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    IsCrossSession = table.Column<bool>(type: "bit", nullable: false),
                    CrossSessionId = table.Column<long>(type: "bigint", nullable: true),
                    CrossSessionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CrossSessionOccurrenceDate = table.Column<DateTime>(type: "date", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    RecordedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    IsEdited = table.Column<bool>(type: "bit", nullable: false),
                    LastEditedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    LastEditedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_SessionOccurrences_SessionOccurrenceId",
                        column: x => x.SessionOccurrenceId,
                        principalTable: "SessionOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_StudentSessionAssignments_StudentSessionAssignmentId",
                        column: x => x.StudentSessionAssignmentId,
                        principalTable: "StudentSessionAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentPeriods",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    SessionId = table.Column<long>(type: "bigint", nullable: true),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: true),
                    StudentSessionAssignmentId = table.Column<long>(type: "bigint", nullable: true),
                    PeriodType = table.Column<byte>(type: "tinyint", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "date", nullable: false),
                    AmountDue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    PaymentStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    IsProRated = table.Column<bool>(type: "bit", nullable: false),
                    ProRatedFraction = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PeriodSequence = table.Column<int>(type: "int", nullable: false),
                    IsCarriedForward = table.Column<bool>(type: "bit", nullable: false),
                    OriginSessionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SessionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StudentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StudentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentPeriods_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentPeriods_StudentSessionAssignments_StudentSessionAssignmentId",
                        column: x => x.StudentSessionAssignmentId,
                        principalTable: "StudentSessionAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentPeriods_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentPeriods_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AttendanceEditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttendanceRecordId = table.Column<long>(type: "bigint", nullable: true),
                    PreviousStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    NewStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    PreviousAttendanceMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    NewAttendanceMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    EditedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    EditReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceEditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceEditLogs_AttendanceRecords_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "AttendanceRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<long>(type: "bigint", nullable: false),
                    TeacherStudentId = table.Column<long>(type: "bigint", nullable: true),
                    SessionId = table.Column<long>(type: "bigint", nullable: true),
                    SessionOccurrenceId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentPeriodId = table.Column<long>(type: "bigint", nullable: true),
                    StudentSessionAssignmentId = table.Column<long>(type: "bigint", nullable: true),
                    AmountDue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    PaymentMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    PaymentTransactionStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    CollectedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    StudentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StudentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SessionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    LocalCollectedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    IsPartial = table.Column<bool>(type: "bit", nullable: false),
                    IsProRated = table.Column<bool>(type: "bit", nullable: false),
                    ProRatedTierLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsOnlinePayment = table.Column<bool>(type: "bit", nullable: false),
                    OnlineTransactionRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsOfflineRecord = table.Column<bool>(type: "bit", nullable: false),
                    OfflineDeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SyncStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_PaymentPeriods_PaymentPeriodId",
                        column: x => x.PaymentPeriodId,
                        principalTable: "PaymentPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_SessionOccurrences_SessionOccurrenceId",
                        column: x => x.SessionOccurrenceId,
                        principalTable: "SessionOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_StudentSessionAssignments_StudentSessionAssignmentId",
                        column: x => x.StudentSessionAssignmentId,
                        principalTable: "StudentSessionAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_TeacherStudents_TeacherStudentId",
                        column: x => x.TeacherStudentId,
                        principalTable: "TeacherStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentEditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    EditAction = table.Column<byte>(type: "tinyint", nullable: false),
                    PreviousAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    NewAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    PreviousStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    NewStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    EditedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    EditedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    EditReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentEditLogs_PaymentTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "Subjects",
                columns: new[] { "Id", "CreateAt", "DisplayOrder", "IsActive", "NameAr", "NameEn" },
                values: new object[,]
                {
                    { 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, "اللغة العربية", "Arabic Language" },
                    { 2L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "اللغة الإنجليزية", "English Language" },
                    { 3L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, true, "الرياضيات", "Mathematics" },
                    { 4L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4, true, "العلوم", "Science" },
                    { 5L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5, true, "الدراسات الاجتماعية", "Social Studies" },
                    { 6L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 6, true, "اللغة الفرنسية", "French Language" },
                    { 7L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 7, true, "اللغة الألمانية", "German Language" },
                    { 8L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 8, true, "الفيزياء", "Physics" },
                    { 9L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 9, true, "الكيمياء", "Chemistry" },
                    { 10L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 10, true, "الأحياء", "Biology" },
                    { 11L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 11, true, "الجغرافيا", "Geography" },
                    { 12L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 12, true, "التاريخ", "History" },
                    { 13L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 13, true, "الفلسفة والمنطق", "Philosophy & Logic" },
                    { 14L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 14, true, "علم النفس", "Psychology" },
                    { 15L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 15, true, "اللغة الإيطالية", "Italian Language" },
                    { 16L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 16, true, "اللغة الإسبانية", "Spanish Language" },
                    { 17L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 17, true, "علوم الحاسب", "Computer Science" },
                    { 18L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 18, true, "التربية الدينية", "Religious Studies" },
                    { 19L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 19, true, "التربية الفنية", "Art Education" },
                    { 20L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 20, true, "التربية الموسيقية", "Music Education" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDeletionLogs_DeletedByUserId",
                table: "AssignmentDeletionLogs",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDeletionLogs_LastOccurrenceId",
                table: "AssignmentDeletionLogs",
                column: "LastOccurrenceId",
                filter: "[LastOccurrenceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDeletionLogs_TeacherId_DeletedAt",
                table: "AssignmentDeletionLogs",
                columns: new[] { "TeacherId", "DeletedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDeletionLogs_TemplateId",
                table: "AssignmentDeletionLogs",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentOccurrences_DueDate",
                table: "AssignmentOccurrences",
                columns: new[] { "TeacherId", "DueDate", "TemplateId" })
                .Annotation("SqlServer:Include", new[] { "Status", "OccurrenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentOccurrences_TeacherId_Status",
                table: "AssignmentOccurrences",
                columns: new[] { "TeacherId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_AssignmentOccurrences_Template_OccurrenceNumber",
                table: "AssignmentOccurrences",
                columns: new[] { "TemplateId", "OccurrenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentScopes_SessionGroupId",
                table: "AssignmentScopes",
                column: "SessionGroupId",
                filter: "[SessionGroupId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentScopes_SessionId",
                table: "AssignmentScopes",
                column: "SessionId",
                filter: "[SessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentScopes_TeacherId",
                table: "AssignmentScopes",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentScopes_TeacherStudentId",
                table: "AssignmentScopes",
                column: "TeacherStudentId",
                filter: "[TeacherStudentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentScopes_Template",
                table: "AssignmentScopes",
                columns: new[] { "TemplateId", "ScopeType" })
                .Annotation("SqlServer:Include", new[] { "TeacherStudentId", "SessionId", "SessionGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentTemplates_CreatedByUserId",
                table: "AssignmentTemplates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentTemplates_RecurrenceScheduler",
                table: "AssignmentTemplates",
                columns: new[] { "IsRecurring", "IsRecurrenceStopped", "RecurrenceEndDate" },
                filter: "[IsRecurring] = 1 AND [IsRecurrenceStopped] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentTemplates_TeacherList",
                table: "AssignmentTemplates",
                columns: new[] { "TeacherId", "AssignmentType", "IsRecurring", "CreateAt" },
                descending: new[] { false, false, false, true })
                .Annotation("SqlServer:Include", new[] { "Name", "NameAr" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantLoginActivity_AssistantId",
                table: "AssistantLoginActivity",
                column: "AssistantId");

            migrationBuilder.CreateIndex(
                name: "IX_Assistants_TeacherAccountId",
                table: "Assistants",
                column: "TeacherAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Assistants_UserId",
                table: "Assistants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantWallets_AssistantId",
                table: "AssistantWallets",
                column: "AssistantId");

            migrationBuilder.CreateIndex(
                name: "IX_AW_TeacherId_AssistantId",
                table: "AssistantWallets",
                columns: new[] { "TeacherId", "AssistantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AW_TeacherId_AssistantUserId",
                table: "AssistantWallets",
                columns: new[] { "TeacherId", "AssistantUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AEL_AttendanceRecordId",
                table: "AttendanceEditLogs",
                column: "AttendanceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AR_ConsecutiveAbsenceCalc",
                table: "AttendanceRecords",
                columns: new[] { "TeacherStudentId", "SessionId", "OccurrenceDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AR_CrossSession",
                table: "AttendanceRecords",
                columns: new[] { "TeacherId", "TeacherStudentId", "IsCrossSession" },
                filter: "[IsCrossSession] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AR_PostDeletion_DuplicateGuard",
                table: "AttendanceRecords",
                columns: new[] { "TeacherStudentId", "OccurrenceDate", "SessionName" },
                unique: true,
                filter: "[SessionOccurrenceId] IS NULL AND [SessionId] IS NULL AND [TeacherStudentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AR_SessionOccurrenceId",
                table: "AttendanceRecords",
                column: "SessionOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_AR_TeacherId_OccurrenceDate_Status",
                table: "AttendanceRecords",
                columns: new[] { "TeacherId", "OccurrenceDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AR_TeacherId_SessionGroupId",
                table: "AttendanceRecords",
                columns: new[] { "TeacherId", "SessionGroupId" },
                filter: "[SessionGroupId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AR_TeacherId_SessionId_OccurrenceDate",
                table: "AttendanceRecords",
                columns: new[] { "TeacherId", "SessionId", "OccurrenceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AR_TeacherStudentId_OccurrenceDate",
                table: "AttendanceRecords",
                columns: new[] { "TeacherStudentId", "OccurrenceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AR_TeacherStudentId_OccurrenceDate_SessionId",
                table: "AttendanceRecords",
                columns: new[] { "TeacherStudentId", "OccurrenceDate", "SessionId" },
                unique: true,
                filter: "[TeacherStudentId] IS NOT NULL AND [SessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AR_TeacherStudentId_SessionOccurrenceId",
                table: "AttendanceRecords",
                columns: new[] { "TeacherStudentId", "SessionOccurrenceId" },
                unique: true,
                filter: "[SessionOccurrenceId] IS NOT NULL AND [TeacherStudentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_StudentSessionAssignmentId",
                table: "AttendanceRecords",
                column: "StudentSessionAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrial_AssistantId",
                table: "AuditTrial",
                column: "AssistantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrial_ModuleId",
                table: "AuditTrial",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrial_teacherId",
                table: "AuditTrial",
                column: "teacherId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomatedTriggers_MessageTemplateId",
                table: "AutomatedTriggers",
                column: "MessageTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomatedTriggers_TeacherId",
                table: "AutomatedTriggers",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_EPT_TeacherId_EventId",
                table: "EventPaymentTransactions",
                columns: new[] { "TeacherId", "PaymentEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventPaymentTransactions_EventStudentObligationId",
                table: "EventPaymentTransactions",
                column: "EventStudentObligationId");

            migrationBuilder.CreateIndex(
                name: "IX_EventPaymentTransactions_PaymentEventId",
                table: "EventPaymentTransactions",
                column: "PaymentEventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventPaymentTransactions_TeacherStudentId",
                table: "EventPaymentTransactions",
                column: "TeacherStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ESO_EventId_Status",
                table: "EventStudentObligations",
                columns: new[] { "PaymentEventId", "PaymentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ESO_EventId_StudentId",
                table: "EventStudentObligations",
                columns: new[] { "PaymentEventId", "TeacherStudentId" },
                unique: true,
                filter: "[TeacherStudentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EventStudentObligations_TeacherId",
                table: "EventStudentObligations",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_EventStudentObligations_TeacherStudentId",
                table: "EventStudentObligations",
                column: "TeacherStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleUsers_UserId",
                table: "GoogleUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageBlocks_MessageTemplateId",
                table: "MessageBlocks",
                column: "MessageTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_MessageTemplateId",
                table: "MessageLogs",
                column: "MessageTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_TeacherId",
                table: "MessageLogs",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplate_TeacherId_Name",
                table: "MessageTemplates",
                columns: new[] { "TeacherId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplates_Name",
                table: "MessageTemplates",
                column: "Name")
                .Annotation("SqlServer:Clustered", false);

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplates_TeacherId",
                table: "MessageTemplates",
                column: "TeacherId")
                .Annotation("SqlServer:Clustered", false);

            migrationBuilder.CreateIndex(
                name: "IX_MessagingChannels_TeacherId",
                table: "MessagingChannels",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildren_ParentUserId_IsActive",
                table: "ParentChildren",
                columns: new[] { "ParentUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildren_ParentUserId_StudentUserId",
                table: "ParentChildren",
                columns: new[] { "ParentUserId", "StudentUserId" },
                unique: true,
                filter: "[StudentUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildren_ParentUserId_StudentUserId_IsActive",
                table: "ParentChildren",
                columns: new[] { "ParentUserId", "StudentUserId", "IsActive" },
                filter: "[StudentUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildren_StudentUserId",
                table: "ParentChildren",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildTeacherLinks_ChildId_LinkStatus",
                table: "ParentChildTeacherLinks",
                columns: new[] { "ParentChildId", "LinkStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildTeacherLinks_ChildId_TeacherId",
                table: "ParentChildTeacherLinks",
                columns: new[] { "ParentChildId", "TeacherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildTeacherLinks_TeacherId",
                table: "ParentChildTeacherLinks",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildTeacherLinks_TeacherStudentId",
                table: "ParentChildTeacherLinks",
                column: "TeacherStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentUsers_AccountStatus_DeletedAt",
                table: "ParentUsers",
                columns: new[] { "AccountStatus", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ParentUsers_UserId",
                table: "ParentUsers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PEL_PaymentTransactionId",
                table: "PaymentEditLogs",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PE_TeacherId_IsDeleted",
                table: "PaymentEvents",
                columns: new[] { "TeacherId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPeriods_SessionId",
                table: "PaymentPeriods",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPeriods_StudentSessionAssignmentId",
                table: "PaymentPeriods",
                column: "StudentSessionAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPeriods_TeacherStudentId",
                table: "PaymentPeriods",
                column: "TeacherStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_PP_EarliestUnpaid",
                table: "PaymentPeriods",
                columns: new[] { "TeacherId", "TeacherStudentId", "PaymentStatus", "PeriodSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_PP_TeacherId_SessionId_PeriodDates",
                table: "PaymentPeriods",
                columns: new[] { "TeacherId", "SessionId", "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PP_TeacherId_SessionId_Status",
                table: "PaymentPeriods",
                columns: new[] { "TeacherId", "SessionId", "PaymentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_PP_TeacherId_StudentId_Sequence",
                table: "PaymentPeriods",
                columns: new[] { "TeacherId", "TeacherStudentId", "PeriodSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_SessionId",
                table: "PaymentTransactions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_SessionOccurrenceId",
                table: "PaymentTransactions",
                column: "SessionOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_StudentSessionAssignmentId",
                table: "PaymentTransactions",
                column: "StudentSessionAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_TeacherStudentId",
                table: "PaymentTransactions",
                column: "TeacherStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_PT_PaymentPeriodId",
                table: "PaymentTransactions",
                column: "PaymentPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_PT_TeacherId_CollectorId_CollectedAt",
                table: "PaymentTransactions",
                columns: new[] { "TeacherId", "CollectedByUserId", "CollectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PT_TeacherId_IsDeleted",
                table: "PaymentTransactions",
                columns: new[] { "TeacherId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_PT_TeacherId_SessionId_CollectedAt",
                table: "PaymentTransactions",
                columns: new[] { "TeacherId", "SessionId", "CollectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PT_TeacherId_StudentId_CollectedAt",
                table: "PaymentTransactions",
                columns: new[] { "TeacherId", "TeacherStudentId", "CollectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PT_TeacherId_StudentId_LocalDate",
                table: "PaymentTransactions",
                columns: new[] { "TeacherId", "TeacherStudentId", "LocalCollectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingSubscriptionPayments_PaymobSessionId",
                table: "PendingSubscriptionPayments",
                column: "PaymobSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingSubscriptionPayments_ResolvedByUserId",
                table: "PendingSubscriptionPayments",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingSubscriptionPayments_TeacherId_Status",
                table: "PendingSubscriptionPayments",
                columns: new[] { "TeacherId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ModuleId",
                table: "Permissions",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionGroups_TeacherId",
                table: "SessionGroups",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionGroups_TeacherId_GroupName",
                table: "SessionGroups",
                columns: new[] { "TeacherId", "GroupName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionLinks_LinkedSessionId",
                table: "SessionLinks",
                column: "LinkedSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionLinks_SessionId_LinkedSessionId",
                table: "SessionLinks",
                columns: new[] { "SessionId", "LinkedSessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionOccurrences_SessionId_OccurrenceDate",
                table: "SessionOccurrences",
                columns: new[] { "SessionId", "OccurrenceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionOccurrences_TeacherId_OccurrenceDate",
                table: "SessionOccurrences",
                columns: new[] { "TeacherId", "OccurrenceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionOccurrences_TeacherId_Status",
                table: "SessionOccurrences",
                columns: new[] { "TeacherId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_SessionGroupId",
                table: "Sessions",
                column: "SessionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TeacherId",
                table: "Sessions",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TeacherId_EndDate",
                table: "Sessions",
                columns: new[] { "TeacherId", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TeacherId_OccurrenceType_SelectedDays",
                table: "Sessions",
                columns: new[] { "TeacherId", "OccurrenceType", "SelectedDays" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TeacherId_SessionName",
                table: "Sessions",
                columns: new[] { "TeacherId", "SessionName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionTransferEvents_TeacherStudentId",
                table: "SessionTransferEvents",
                column: "TeacherStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_STE_TeacherId_StudentId",
                table: "SessionTransferEvents",
                columns: new[] { "TeacherId", "TeacherStudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_SAC_TeacherId_ConsecutiveAbsences",
                table: "StudentAbsenceCounters",
                columns: new[] { "TeacherId", "ConsecutiveAbsences" });

            migrationBuilder.CreateIndex(
                name: "IX_SAC_TeacherId_TeacherStudentId",
                table: "StudentAbsenceCounters",
                columns: new[] { "TeacherId", "TeacherStudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SAC_TeacherStudentId",
                table: "StudentAbsenceCounters",
                column: "TeacherStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssignmentObligations_Absence",
                table: "StudentAssignmentObligations",
                columns: new[] { "TeacherId", "TeacherStudentId", "OccurrenceId" },
                filter: "[Status] IN (2, 5)")
                .Annotation("SqlServer:Include", new[] { "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssignmentObligations_LastUpdatedByUserId",
                table: "StudentAssignmentObligations",
                column: "LastUpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssignmentObligations_PendingGrades",
                table: "StudentAssignmentObligations",
                columns: new[] { "TeacherId", "OccurrenceId" },
                filter: "[Status] IN (3, 6)")
                .Annotation("SqlServer:Include", new[] { "TeacherStudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssignmentObligations_StudentHistory",
                table: "StudentAssignmentObligations",
                columns: new[] { "TeacherId", "TeacherStudentId", "CreateAt" },
                descending: new[] { false, false, true })
                .Annotation("SqlServer:Include", new[] { "OccurrenceId", "Status", "GradeValue", "IsGradeEntered" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssignmentObligations_TeacherStudentId",
                table: "StudentAssignmentObligations",
                column: "TeacherStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssignmentObligations_Tracking",
                table: "StudentAssignmentObligations",
                columns: new[] { "TeacherId", "OccurrenceId", "Status" })
                .Annotation("SqlServer:Include", new[] { "TeacherStudentId", "GradeValue", "IsGradeEntered", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_StudentAssignmentObligations_Occurrence_Student",
                table: "StudentAssignmentObligations",
                columns: new[] { "OccurrenceId", "TeacherStudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentCapacityPackages_IsActive",
                table: "StudentCapacityPackages",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCapacityPackages_PriceUpdatedByUserId",
                table: "StudentCapacityPackages",
                column: "PriceUpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SD_TeacherId_StudentId",
                table: "StudentDepartures",
                columns: new[] { "TeacherId", "TeacherStudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentDepartures_SessionId",
                table: "StudentDepartures",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDepartures_TeacherStudentId",
                table: "StudentDepartures",
                column: "TeacherStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentObligationAuditLogs_ChangedByUserId",
                table: "StudentObligationAuditLogs",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentObligationAuditLogs_Obligation_ChangedAt",
                table: "StudentObligationAuditLogs",
                columns: new[] { "StudentObligationId", "ChangedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_StudentObligationAuditLogs_TeacherId_ChangedAt",
                table: "StudentObligationAuditLogs",
                columns: new[] { "TeacherId", "ChangedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SPC_TeacherId_ConsecutiveUnpaid",
                table: "StudentPaymentCounters",
                columns: new[] { "TeacherId", "ConsecutiveUnpaid" });

            migrationBuilder.CreateIndex(
                name: "IX_SPC_TeacherId_StudentId",
                table: "StudentPaymentCounters",
                columns: new[] { "TeacherId", "TeacherStudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SPC_TeacherId_TotalOutstanding",
                table: "StudentPaymentCounters",
                columns: new[] { "TeacherId", "TotalOutstanding" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentPaymentCounters_TeacherStudentId",
                table: "StudentPaymentCounters",
                column: "TeacherStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_SSA_SessionId_IsActive",
                table: "StudentSessionAssignments",
                columns: new[] { "SessionId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SSA_TeacherId_TeacherStudentId",
                table: "StudentSessionAssignments",
                columns: new[] { "TeacherId", "TeacherStudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_SSA_TeacherStudentId_IsActive",
                table: "StudentSessionAssignments",
                columns: new[] { "TeacherStudentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentTeacherLinks_StudentUserId_LinkStatus",
                table: "StudentTeacherLinks",
                columns: new[] { "StudentUserId", "LinkStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentTeacherLinks_StudentUserId_TeacherId",
                table: "StudentTeacherLinks",
                columns: new[] { "StudentUserId", "TeacherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentTeacherLinks_TeacherId",
                table: "StudentTeacherLinks",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTeacherLinks_TeacherStudentId",
                table: "StudentTeacherLinks",
                column: "TeacherStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentUsers_AccountStatus_DeletedAt",
                table: "StudentUsers",
                columns: new[] { "AccountStatus", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentUsers_StudentAccountCode",
                table: "StudentUsers",
                column: "StudentAccountCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentUsers_UserId",
                table: "StudentUsers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_IsActive",
                table: "Subjects",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionAlerts_Key",
                table: "SubscriptionAlerts",
                columns: new[] { "TeacherId", "SubscriptionEndDate", "AlertDay" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherConfigurations_TeacherId",
                table: "TeacherConfigurations",
                column: "TeacherId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherProratedTiers_ConfigId_TierNumber",
                table: "TeacherProratedTiers",
                columns: new[] { "TeacherConfigurationId", "TierNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_AccountStatus_DeletedAt",
                table: "Teachers",
                columns: new[] { "AccountStatus", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_CreatedByUserId",
                table: "Teachers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_StudentCapacityPackageId",
                table: "Teachers",
                column: "StudentCapacityPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_TeacherCode",
                table: "Teachers",
                column: "TeacherCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_UserId",
                table: "Teachers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherStudents_LinkingLookup",
                table: "TeacherStudents",
                columns: new[] { "TeacherId", "StudentCode", "HashedToken" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherStudents_RecycleBin_DeletedAt",
                table: "TeacherStudents",
                columns: new[] { "IsDeleted", "DeletedAt" },
                filter: "[IsDeleted] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherStudents_SessionId",
                table: "TeacherStudents",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherStudents_TeacherId_IsDeleted",
                table: "TeacherStudents",
                columns: new[] { "TeacherId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherStudents_TeacherId_SessionId",
                table: "TeacherStudents",
                columns: new[] { "TeacherId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherStudents_TeacherId_StudentCode",
                table: "TeacherStudents",
                columns: new[] { "TeacherId", "StudentCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherStudents_TeacherId_StudentName",
                table: "TeacherStudents",
                columns: new[] { "TeacherId", "StudentName" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubjects_SubjectId",
                table: "TeacherSubjects",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubjects_TeacherId_SubjectId",
                table: "TeacherSubjects",
                columns: new[] { "TeacherId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubscriptions_CreatedByUserId",
                table: "TeacherSubscriptions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubscriptions_Current",
                table: "TeacherSubscriptions",
                column: "TeacherId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubscriptions_EndDate",
                table: "TeacherSubscriptions",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubscriptions_TeacherId_EndDate",
                table: "TeacherSubscriptions",
                columns: new[] { "TeacherId", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Templates_TeacherId",
                table: "Templates",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplatesPermisions_TemplateId",
                table: "TemplatesPermisions",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplatesPermissionsOfUsers_TemplateId",
                table: "TemplatesPermissionsOfUsers",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplatesPermissionsOfUsers_TemplatePermisionsPermisionId_TemplatePermisionsTemplateId",
                table: "TemplatesPermissionsOfUsers",
                columns: new[] { "TemplatePermisionsPermisionId", "TemplatePermisionsTemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_TutorModuleAccess_ModuleId",
                table: "TutorModuleAccess",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDeviceTokens_UserId_FcmToken",
                table: "UserDeviceTokens",
                columns: new[] { "UserId", "FcmToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDeviceTokens_UserId_IsActive",
                table: "UserDeviceTokens",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_IsRead_SentAt",
                table: "UserNotifications",
                columns: new[] { "UserId", "IsRead", "SentAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreateByUserId",
                table: "Users",
                column: "CreateByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Users_PhoneNumber",
                table: "Users",
                column: "PhoneNumber",
                unique: true)
                .Annotation("SqlServer:Clustered", false);

            migrationBuilder.CreateIndex(
                name: "IX_UsersPermissions_PermissionId",
                table: "UsersPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTutor_TutorId",
                table: "UserTutor",
                column: "TutorId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAnalytics_TeacherId",
                table: "VideoAnalytics",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAnalytics_TeacherStudentId_VideoAssetId",
                table: "VideoAnalytics",
                columns: new[] { "TeacherStudentId", "VideoAssetId" })
                .Annotation("SqlServer:Include", new[] { "OpenCount", "TotalWatchSeconds", "LastResumePositionSeconds", "LastUpdated" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoAnalytics_VideoAssetId_Includes",
                table: "VideoAnalytics",
                column: "VideoAssetId")
                .Annotation("SqlServer:Include", new[] { "TeacherStudentId", "OpenCount", "TotalWatchSeconds", "LastUpdated" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoAnalytics_VideoAssetId_TeacherId",
                table: "VideoAnalytics",
                columns: new[] { "VideoAssetId", "TeacherId" });

            migrationBuilder.CreateIndex(
                name: "UX_VideoAnalytics_Video_Student",
                table: "VideoAnalytics",
                columns: new[] { "VideoAssetId", "TeacherStudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoAssetAudits_DeletedByUserId",
                table: "VideoAssetAudits",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAssetAudits_TeacherId_DeletedAt",
                table: "VideoAssetAudits",
                columns: new[] { "TeacherId", "DeletedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_VideoAssetAudits_VideoAssetId",
                table: "VideoAssetAudits",
                column: "VideoAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAssets_CreatedByUserId",
                table: "VideoAssets",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAssets_TeacherId_CreatedAt",
                table: "VideoAssets",
                columns: new[] { "TeacherId", "CreateAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_VideoAssets_TeacherId_ExternalId",
                table: "VideoAssets",
                columns: new[] { "TeacherId", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "UX_VideoAssets_Id_TeacherId",
                table: "VideoAssets",
                columns: new[] { "Id", "TeacherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoScopes_AssignedByUserId",
                table: "VideoScopes",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoScopes_SessionGroupId",
                table: "VideoScopes",
                column: "SessionGroupId",
                filter: "[SessionGroupId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "VideoAssetId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoScopes_SessionId",
                table: "VideoScopes",
                column: "SessionId",
                filter: "[SessionId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "VideoAssetId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoScopes_TeacherId",
                table: "VideoScopes",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoScopes_TeacherStudentId",
                table: "VideoScopes",
                column: "TeacherStudentId",
                filter: "[TeacherStudentId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "VideoAssetId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoScopes_VideoAssetId",
                table: "VideoScopes",
                column: "VideoAssetId")
                .Annotation("SqlServer:Include", new[] { "ScopeType", "TeacherStudentId", "SessionId", "SessionGroupId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoScopes_VideoAssetId_TeacherId",
                table: "VideoScopes",
                columns: new[] { "VideoAssetId", "TeacherId" });

            migrationBuilder.CreateIndex(
                name: "UX_VideoScopes_Video_Type_Target",
                table: "VideoScopes",
                columns: new[] { "VideoAssetId", "ScopeType", "TeacherStudentId", "SessionId", "SessionGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoWatchEvents_TeacherId",
                table: "VideoWatchEvents",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoWatchEvents_VideoAssetId_TeacherId",
                table: "VideoWatchEvents",
                columns: new[] { "VideoAssetId", "TeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_VWE_Student_Video_Device_TimeDesc",
                table: "VideoWatchEvents",
                columns: new[] { "TeacherStudentId", "VideoAssetId", "DeviceId", "EventUtc" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_VWE_VideoAssetId_EventUtcDesc",
                table: "VideoWatchEvents",
                columns: new[] { "VideoAssetId", "EventUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "UX_VWE_ClientEventId",
                table: "VideoWatchEvents",
                column: "ClientEventId",
                unique: true,
                filter: "[ClientEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletResetLogs_AssistantId",
                table: "WalletResetLogs",
                column: "AssistantId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletResetLogs_AssistantWalletId",
                table: "WalletResetLogs",
                column: "AssistantWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WRL_TeacherId_AssistantId",
                table: "WalletResetLogs",
                columns: new[] { "TeacherId", "AssistantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentDeletionLogs");

            migrationBuilder.DropTable(
                name: "AssignmentScopes");

            migrationBuilder.DropTable(
                name: "AssistantLoginActivity");

            migrationBuilder.DropTable(
                name: "AttendanceEditLogs");

            migrationBuilder.DropTable(
                name: "AuditTrial");

            migrationBuilder.DropTable(
                name: "AutomatedTriggers");

            migrationBuilder.DropTable(
                name: "EventPaymentTransactions");

            migrationBuilder.DropTable(
                name: "GoogleUsers");

            migrationBuilder.DropTable(
                name: "MessageBlocks");

            migrationBuilder.DropTable(
                name: "MessageLogs");

            migrationBuilder.DropTable(
                name: "MessagingChannels");

            migrationBuilder.DropTable(
                name: "ParentChildTeacherLinks");

            migrationBuilder.DropTable(
                name: "PaymentEditLogs");

            migrationBuilder.DropTable(
                name: "PendingSubscriptionPayments");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "SessionLinks");

            migrationBuilder.DropTable(
                name: "SessionTransferEvents");

            migrationBuilder.DropTable(
                name: "StudentAbsenceCounters");

            migrationBuilder.DropTable(
                name: "StudentDepartures");

            migrationBuilder.DropTable(
                name: "StudentObligationAuditLogs");

            migrationBuilder.DropTable(
                name: "StudentPaymentCounters");

            migrationBuilder.DropTable(
                name: "StudentTeacherLinks");

            migrationBuilder.DropTable(
                name: "SubscriptionAlerts");

            migrationBuilder.DropTable(
                name: "TeacherProratedTiers");

            migrationBuilder.DropTable(
                name: "TeacherSubjects");

            migrationBuilder.DropTable(
                name: "TeacherSubscriptions");

            migrationBuilder.DropTable(
                name: "TemplatesPermissionsOfUsers");

            migrationBuilder.DropTable(
                name: "TutorModuleAccess");

            migrationBuilder.DropTable(
                name: "UserDeviceTokens");

            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropTable(
                name: "UsersPermissions");

            migrationBuilder.DropTable(
                name: "UserTutor");

            migrationBuilder.DropTable(
                name: "VideoAnalytics");

            migrationBuilder.DropTable(
                name: "VideoAssetAudits");

            migrationBuilder.DropTable(
                name: "VideoScopes");

            migrationBuilder.DropTable(
                name: "VideoWatchEvents");

            migrationBuilder.DropTable(
                name: "WalletResetLogs");

            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "EventStudentObligations");

            migrationBuilder.DropTable(
                name: "MessageTemplates");

            migrationBuilder.DropTable(
                name: "ParentChildren");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "StudentAssignmentObligations");

            migrationBuilder.DropTable(
                name: "TeacherConfigurations");

            migrationBuilder.DropTable(
                name: "Subjects");

            migrationBuilder.DropTable(
                name: "TemplatesPermisions");

            migrationBuilder.DropTable(
                name: "VideoAssets");

            migrationBuilder.DropTable(
                name: "AssistantWallets");

            migrationBuilder.DropTable(
                name: "PaymentEvents");

            migrationBuilder.DropTable(
                name: "ParentUsers");

            migrationBuilder.DropTable(
                name: "StudentUsers");

            migrationBuilder.DropTable(
                name: "PaymentPeriods");

            migrationBuilder.DropTable(
                name: "SessionOccurrences");

            migrationBuilder.DropTable(
                name: "AssignmentOccurrences");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Templates");

            migrationBuilder.DropTable(
                name: "Assistants");

            migrationBuilder.DropTable(
                name: "StudentSessionAssignments");

            migrationBuilder.DropTable(
                name: "AssignmentTemplates");

            migrationBuilder.DropTable(
                name: "Models");

            migrationBuilder.DropTable(
                name: "TeacherStudents");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "SessionGroups");

            migrationBuilder.DropTable(
                name: "Teachers");

            migrationBuilder.DropTable(
                name: "StudentCapacityPackages");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
