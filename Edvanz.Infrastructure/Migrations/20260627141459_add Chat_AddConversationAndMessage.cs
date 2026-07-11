using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addChat_AddConversationAndMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssistantLoginActivity_Assistants_AssistantId",
                table: "AssistantLoginActivity");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditTrial_Models_ModuleId",
                table: "AuditTrial");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditTrial_Teachers_teacherId",
                table: "AuditTrial");

            migrationBuilder.DropForeignKey(
                name: "FK_AutomatedTriggers_MessageTemplates_MessageTemplateId",
                table: "AutomatedTriggers");

            migrationBuilder.DropForeignKey(
                name: "FK_AutomatedTriggers_Teachers_TeacherId",
                table: "AutomatedTriggers");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageBlocks_MessageTemplates_MessageTemplateId",
                table: "MessageBlocks");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageLogs_Teachers_TeacherId",
                table: "MessageLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageTemplates_Teachers_TeacherId",
                table: "MessageTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_MessagingChannels_Teachers_TeacherId",
                table: "MessagingChannels");

            migrationBuilder.DropForeignKey(
                name: "FK_Permissions_Models_ModuleId",
                table: "Permissions");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Users_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Templates_Teachers_TeacherId",
                table: "Templates");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplatesPermisions_Permissions_PermisionId",
                table: "TemplatesPermisions");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplatesPermisions_Templates_TemplateId",
                table: "TemplatesPermisions");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplatesPermissionsOfUsers_Assistants_AssisstantId",
                table: "TemplatesPermissionsOfUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplatesPermissionsOfUsers_Templates_TemplateId",
                table: "TemplatesPermissionsOfUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_TutorModuleAccess_Models_ModuleId",
                table: "TutorModuleAccess");

            migrationBuilder.DropForeignKey(
                name: "FK_TutorModuleAccess_Teachers_TutorId",
                table: "TutorModuleAccess");

            migrationBuilder.DropForeignKey(
                name: "FK_UsersPermissions_Permissions_PermissionId",
                table: "UsersPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UsersPermissions_Users_UserId",
                table: "UsersPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTutor_Users_TutorId",
                table: "UserTutor");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTutor_Users_userId",
                table: "UserTutor");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoAnalytics_Teachers_TeacherId",
                table: "VideoAnalytics");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoScopes_Teachers_TeacherId",
                table: "VideoScopes");

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParticipantAUserId = table.Column<long>(type: "bigint", nullable: false),
                    ParticipantBUserId = table.Column<long>(type: "bigint", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastMessagePreview = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastMessageSenderUserId = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_Users_ParticipantAUserId",
                        column: x => x.ParticipantAUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Conversations_Users_ParticipantBUserId",
                        column: x => x.ParticipantBUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    SenderUserId = table.Column<long>(type: "bigint", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatMessages_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_Conversation_Sender_IsRead",
                table: "ChatMessages",
                columns: new[] { "ConversationId", "SenderUserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId_SentAt",
                table: "ChatMessages",
                columns: new[] { "ConversationId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SenderUserId",
                table: "ChatMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ParticipantA_LastMessageAt",
                table: "Conversations",
                columns: new[] { "ParticipantAUserId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ParticipantB_LastMessageAt",
                table: "Conversations",
                columns: new[] { "ParticipantBUserId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "UX_Conversations_Participants",
                table: "Conversations",
                columns: new[] { "ParticipantAUserId", "ParticipantBUserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantLoginActivity_Assistants_AssistantId",
                table: "AssistantLoginActivity",
                column: "AssistantId",
                principalTable: "Assistants",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_AuditTrial_Models_ModuleId",
                table: "AuditTrial",
                column: "ModuleId",
                principalTable: "Models",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_AuditTrial_Teachers_teacherId",
                table: "AuditTrial",
                column: "teacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_AutomatedTriggers_MessageTemplates_MessageTemplateId",
                table: "AutomatedTriggers",
                column: "MessageTemplateId",
                principalTable: "MessageTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_AutomatedTriggers_Teachers_TeacherId",
                table: "AutomatedTriggers",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_MessageBlocks_MessageTemplates_MessageTemplateId",
                table: "MessageBlocks",
                column: "MessageTemplateId",
                principalTable: "MessageTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_MessageLogs_Teachers_TeacherId",
                table: "MessageLogs",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_MessageTemplates_Teachers_TeacherId",
                table: "MessageTemplates",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_MessagingChannels_Teachers_TeacherId",
                table: "MessagingChannels",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_Permissions_Models_ModuleId",
                table: "Permissions",
                column: "ModuleId",
                principalTable: "Models",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Users_UserId",
                table: "RefreshTokens",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_Templates_Teachers_TeacherId",
                table: "Templates",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_TemplatesPermisions_Permissions_PermisionId",
                table: "TemplatesPermisions",
                column: "PermisionId",
                principalTable: "Permissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_TemplatesPermisions_Templates_TemplateId",
                table: "TemplatesPermisions",
                column: "TemplateId",
                principalTable: "Templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_TemplatesPermissionsOfUsers_Assistants_AssisstantId",
                table: "TemplatesPermissionsOfUsers",
                column: "AssisstantId",
                principalTable: "Assistants",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_TemplatesPermissionsOfUsers_Templates_TemplateId",
                table: "TemplatesPermissionsOfUsers",
                column: "TemplateId",
                principalTable: "Templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_TutorModuleAccess_Models_ModuleId",
                table: "TutorModuleAccess",
                column: "ModuleId",
                principalTable: "Models",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_TutorModuleAccess_Teachers_TutorId",
                table: "TutorModuleAccess",
                column: "TutorId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_UsersPermissions_Permissions_PermissionId",
                table: "UsersPermissions",
                column: "PermissionId",
                principalTable: "Permissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_UsersPermissions_Users_UserId",
                table: "UsersPermissions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_UserTutor_Users_TutorId",
                table: "UserTutor",
                column: "TutorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_UserTutor_Users_userId",
                table: "UserTutor",
                column: "userId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_VideoAnalytics_Teachers_TeacherId",
                table: "VideoAnalytics",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );

            migrationBuilder.AddForeignKey(
                name: "FK_VideoScopes_Teachers_TeacherId",
                table: "VideoScopes",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssistantLoginActivity_Assistants_AssistantId",
                table: "AssistantLoginActivity");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditTrial_Models_ModuleId",
                table: "AuditTrial");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditTrial_Teachers_teacherId",
                table: "AuditTrial");

            migrationBuilder.DropForeignKey(
                name: "FK_AutomatedTriggers_MessageTemplates_MessageTemplateId",
                table: "AutomatedTriggers");

            migrationBuilder.DropForeignKey(
                name: "FK_AutomatedTriggers_Teachers_TeacherId",
                table: "AutomatedTriggers");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageBlocks_MessageTemplates_MessageTemplateId",
                table: "MessageBlocks");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageLogs_Teachers_TeacherId",
                table: "MessageLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageTemplates_Teachers_TeacherId",
                table: "MessageTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_MessagingChannels_Teachers_TeacherId",
                table: "MessagingChannels");

            migrationBuilder.DropForeignKey(
                name: "FK_Permissions_Models_ModuleId",
                table: "Permissions");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Users_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Templates_Teachers_TeacherId",
                table: "Templates");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplatesPermisions_Permissions_PermisionId",
                table: "TemplatesPermisions");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplatesPermisions_Templates_TemplateId",
                table: "TemplatesPermisions");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplatesPermissionsOfUsers_Assistants_AssisstantId",
                table: "TemplatesPermissionsOfUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplatesPermissionsOfUsers_Templates_TemplateId",
                table: "TemplatesPermissionsOfUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_TutorModuleAccess_Models_ModuleId",
                table: "TutorModuleAccess");

            migrationBuilder.DropForeignKey(
                name: "FK_TutorModuleAccess_Teachers_TutorId",
                table: "TutorModuleAccess");

            migrationBuilder.DropForeignKey(
                name: "FK_UsersPermissions_Permissions_PermissionId",
                table: "UsersPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UsersPermissions_Users_UserId",
                table: "UsersPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTutor_Users_TutorId",
                table: "UserTutor");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTutor_Users_userId",
                table: "UserTutor");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoAnalytics_Teachers_TeacherId",
                table: "VideoAnalytics");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoScopes_Teachers_TeacherId",
                table: "VideoScopes");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantLoginActivity_Assistants_AssistantId",
                table: "AssistantLoginActivity",
                column: "AssistantId",
                principalTable: "Assistants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditTrial_Models_ModuleId",
                table: "AuditTrial",
                column: "ModuleId",
                principalTable: "Models",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditTrial_Teachers_teacherId",
                table: "AuditTrial",
                column: "teacherId",
                principalTable: "Teachers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AutomatedTriggers_MessageTemplates_MessageTemplateId",
                table: "AutomatedTriggers",
                column: "MessageTemplateId",
                principalTable: "MessageTemplates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AutomatedTriggers_Teachers_TeacherId",
                table: "AutomatedTriggers",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageBlocks_MessageTemplates_MessageTemplateId",
                table: "MessageBlocks",
                column: "MessageTemplateId",
                principalTable: "MessageTemplates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageLogs_Teachers_TeacherId",
                table: "MessageLogs",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageTemplates_Teachers_TeacherId",
                table: "MessageTemplates",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MessagingChannels_Teachers_TeacherId",
                table: "MessagingChannels",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Permissions_Models_ModuleId",
                table: "Permissions",
                column: "ModuleId",
                principalTable: "Models",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Users_UserId",
                table: "RefreshTokens",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Templates_Teachers_TeacherId",
                table: "Templates",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TemplatesPermisions_Permissions_PermisionId",
                table: "TemplatesPermisions",
                column: "PermisionId",
                principalTable: "Permissions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TemplatesPermisions_Templates_TemplateId",
                table: "TemplatesPermisions",
                column: "TemplateId",
                principalTable: "Templates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TemplatesPermissionsOfUsers_Assistants_AssisstantId",
                table: "TemplatesPermissionsOfUsers",
                column: "AssisstantId",
                principalTable: "Assistants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TemplatesPermissionsOfUsers_Templates_TemplateId",
                table: "TemplatesPermissionsOfUsers",
                column: "TemplateId",
                principalTable: "Templates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TutorModuleAccess_Models_ModuleId",
                table: "TutorModuleAccess",
                column: "ModuleId",
                principalTable: "Models",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TutorModuleAccess_Teachers_TutorId",
                table: "TutorModuleAccess",
                column: "TutorId",
                principalTable: "Teachers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UsersPermissions_Permissions_PermissionId",
                table: "UsersPermissions",
                column: "PermissionId",
                principalTable: "Permissions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UsersPermissions_Users_UserId",
                table: "UsersPermissions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserTutor_Users_TutorId",
                table: "UserTutor",
                column: "TutorId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserTutor_Users_userId",
                table: "UserTutor",
                column: "userId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoAnalytics_Teachers_TeacherId",
                table: "VideoAnalytics",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoScopes_Teachers_TeacherId",
                table: "VideoScopes",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id");
        }
    }
}
