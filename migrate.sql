IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [Models] (
        [Id] bigint NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Models] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [Subjects] (
        [Id] bigint NOT NULL IDENTITY,
        [NameEn] nvarchar(150) NOT NULL,
        [NameAr] nvarchar(150) NOT NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Subjects] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] bigint NOT NULL IDENTITY,
        [UserType] int NOT NULL,
        [FullName] nvarchar(max) NOT NULL,
        [Username] nvarchar(450) NOT NULL,
        [Email] nvarchar(max) NULL,
        [PasswordHashed] nvarchar(max) NOT NULL,
        [SecurityStamp] nvarchar(max) NOT NULL,
        [PhoneNumber] nvarchar(20) NOT NULL,
        [IdImage] varbinary(max) NULL,
        [IsActive] bit NULL,
        [CreateByUserId] bigint NULL,
        [CreateAt] datetime2 NOT NULL,
        [IsVerified] bit NULL,
        [OtpCode] nvarchar(max) NULL,
        [OtpExpiry] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Users_CreateByUserId] FOREIGN KEY ([CreateByUserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [Permissions] (
        [Id] bigint NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [ModuleId] bigint NOT NULL,
        [IsRestricted] bit NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Permissions_Models_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [Models] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [GoogleUsers] (
        [Id] bigint NOT NULL IDENTITY,
        [GoogleId] nvarchar(max) NOT NULL,
        [Email] nvarchar(max) NOT NULL,
        [UserId] bigint NULL,
        [IsCompleted] bit NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_GoogleUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GoogleUsers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [ParentUsers] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [LanguagePreference] nvarchar(5) NULL,
        [AccountStatus] int NOT NULL,
        [DeactivatedAt] datetime2 NULL,
        [DeletedAt] datetime2 NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ParentUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ParentUsers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [Token] nvarchar(max) NOT NULL,
        [ExpiryDate] datetime2 NOT NULL,
        [IsRevoked] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [SecurityStamp] nvarchar(max) NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [StudentCapacityPackages] (
        [Id] bigint NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        [MinStudents] int NOT NULL,
        [MaxStudents] int NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [MonthlyPriceEGP] decimal(10,2) NOT NULL DEFAULT 0.0,
        [PriceUpdatedAt] datetime2 NULL,
        [PriceUpdatedByUserId] bigint NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_StudentCapacityPackages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudentCapacityPackages_Users_PriceUpdatedByUserId] FOREIGN KEY ([PriceUpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [StudentUsers] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [StudentAccountCode] nvarchar(10) NOT NULL,
        [LanguagePreference] nvarchar(5) NULL,
        [AccountStatus] int NOT NULL,
        [IsFirstLogin] bit NOT NULL,
        [DeactivatedAt] datetime2 NULL,
        [DeletedAt] datetime2 NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_StudentUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudentUsers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [UserDeviceTokens] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [FcmToken] nvarchar(500) NOT NULL,
        [Platform] tinyint NOT NULL,
        [RegisteredAt] datetime2 NOT NULL,
        [LastSeenAt] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserDeviceTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserDeviceTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [UserNotifications] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Body] nvarchar(1000) NOT NULL,
        [DeepLinkPayload] nvarchar(500) NULL,
        [SentAt] datetime2 NOT NULL,
        [IsRead] bit NOT NULL,
        [ReadAt] datetime2 NULL,
        [Category] tinyint NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserNotifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserNotifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [UserTutor] (
        [userId] bigint NOT NULL,
        [TutorId] bigint NOT NULL,
        CONSTRAINT [PK_UserTutor] PRIMARY KEY ([userId], [TutorId]),
        CONSTRAINT [FK_UserTutor_Users_TutorId] FOREIGN KEY ([TutorId]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_UserTutor_Users_userId] FOREIGN KEY ([userId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [UsersPermissions] (
        [UserId] bigint NOT NULL,
        [PermissionId] bigint NOT NULL,
        CONSTRAINT [PK_UsersPermissions] PRIMARY KEY ([UserId], [PermissionId]),
        CONSTRAINT [FK_UsersPermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]),
        CONSTRAINT [FK_UsersPermissions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [Teachers] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [TeacherCode] nvarchar(8) NOT NULL,
        [StudentCapacityPackageId] bigint NULL,
        [StudentCapacity] int NOT NULL,
        [LanguagePreference] nvarchar(5) NULL,
        [CustomSubject] nvarchar(200) NULL,
        [AccountStatus] int NOT NULL,
        [IsConfigurationCompleted] bit NOT NULL,
        [DeactivatedAt] datetime2 NULL,
        [DeletedAt] datetime2 NULL,
        [CreatedByUserId] bigint NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Teachers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Teachers_StudentCapacityPackages_StudentCapacityPackageId] FOREIGN KEY ([StudentCapacityPackageId]) REFERENCES [StudentCapacityPackages] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Teachers_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Teachers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [ParentChildren] (
        [Id] bigint NOT NULL IDENTITY,
        [ParentUserId] bigint NOT NULL,
        [LinkMethod] int NOT NULL,
        [StudentUserId] bigint NULL,
        [ChildName] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ParentChildren] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ParentChildren_ParentUsers_ParentUserId] FOREIGN KEY ([ParentUserId]) REFERENCES [ParentUsers] ([Id]),
        CONSTRAINT [FK_ParentChildren_StudentUsers_StudentUserId] FOREIGN KEY ([StudentUserId]) REFERENCES [StudentUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [AssignmentTemplates] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [AssignmentType] tinyint NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [IsRecurring] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RecurrencePattern] tinyint NOT NULL DEFAULT CAST(0 AS tinyint),
        [RecurrenceEndDate] date NULL,
        [IsRecurrenceStopped] bit NOT NULL DEFAULT CAST(0 AS bit),
        [TrackingMode] tinyint NULL,
        [MaxGrade] decimal(8,2) NULL,
        [PassingThreshold] decimal(8,2) NULL,
        [CreatedByUserId] bigint NOT NULL,
        [UpdatedAt] datetime2(0) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AssignmentTemplates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssignmentTemplates_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]),
        CONSTRAINT [FK_AssignmentTemplates_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [Assistants] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [TeacherAccountId] bigint NOT NULL,
        [LanguagePreference] nvarchar(max) NULL,
        [DeactivatedAt] datetime2 NULL,
        [AccountStatus] int NOT NULL,
        [DeletedAt] datetime2 NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Assistants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Assistants_Teachers_TeacherAccountId] FOREIGN KEY ([TeacherAccountId]) REFERENCES [Teachers] ([Id]),
        CONSTRAINT [FK_Assistants_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [MessageTemplates] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Channel] int NOT NULL,
        [RecipientTarget] int NOT NULL,
        [IsActive] bit NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MessageTemplates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MessageTemplates_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [MessagingChannels] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [ChannelType] int NOT NULL,
        [IsActive] bit NOT NULL,
        [Status] int NOT NULL,
        [EncryptedCredentials] nvarchar(max) NULL,
        [SenderIdOrNumber] nvarchar(max) NULL,
        [ConnectedAt] datetime2 NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MessagingChannels] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MessagingChannels_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [PaymentEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [EventName] nvarchar(300) NOT NULL,
        [EventAmount] decimal(10,2) NOT NULL,
        [TargetScopeType] tinyint NOT NULL,
        [TargetScopeIds] nvarchar(4000) NULL,
        [EventDate] date NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [TotalStudents] int NOT NULL,
        [TotalExpectedRevenue] decimal(14,2) NOT NULL,
        [TotalCollectedRevenue] decimal(14,2) NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2(0) NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentEvents_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [PendingSubscriptionPayments] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [PaymentMethod] tinyint NOT NULL,
        [PaymentChannel] tinyint NOT NULL,
        [Status] tinyint NOT NULL,
        [AmountEGP] decimal(10,2) NOT NULL,
        [PaymobSessionId] nvarchar(200) NULL,
        [SubmittedTransactionReference] nvarchar(100) NULL,
        [EncryptedSubmittedDetails] nvarchar(max) NULL,
        [RejectionReason] nvarchar(500) NULL,
        [InitiatedAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        [ResolvedByUserId] bigint NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PendingSubscriptionPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PendingSubscriptionPayments_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]),
        CONSTRAINT [FK_PendingSubscriptionPayments_Users_ResolvedByUserId] FOREIGN KEY ([ResolvedByUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [SessionGroups] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [GroupName] nvarchar(200) NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SessionGroups] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SessionGroups_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [SubscriptionAlerts] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [SubscriptionEndDate] date NOT NULL,
        [AlertDay] int NOT NULL,
        [SentAt] datetime2 NOT NULL,
        [ChannelsSent] tinyint NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SubscriptionAlerts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubscriptionAlerts_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [TeacherConfigurations] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [StudentCodeGenerationMode] int NOT NULL,
        [StudentCodeLanguage] int NOT NULL,
        [SessionNameMode] int NOT NULL,
        [SessionNameLanguage] int NOT NULL,
        [IsProratedPaymentEnabled] bit NOT NULL,
        [ConsecutiveAbsenceThreshold] int NOT NULL,
        [ConsecutiveUnpaidThreshold] int NOT NULL,
        [BarcodeDisplayMode] int NOT NULL,
        [StudentVisibilityAttendance] bit NOT NULL,
        [StudentVisibilityPayment] bit NOT NULL,
        [StudentVisibilityHomework] bit NOT NULL,
        [StudentVisibilityExamDefault] bit NOT NULL,
        [ParentVisibilityAttendance] bit NOT NULL,
        [ParentVisibilityPayment] bit NOT NULL,
        [ParentVisibilityHomework] bit NOT NULL,
        [ParentVisibilityExamDefault] bit NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TeacherConfigurations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeacherConfigurations_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [TeacherSubjects] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [SubjectId] bigint NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TeacherSubjects] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeacherSubjects_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeacherSubjects_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [TeacherSubscriptions] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [IsCurrent] bit NOT NULL,
        [PaymentMethod] tinyint NOT NULL,
        [PaymentChannel] tinyint NOT NULL,
        [AmountPaidEGP] decimal(10,2) NOT NULL,
        [TransactionReference] nvarchar(100) NULL,
        [EncryptedPaymentDetails] nvarchar(max) NULL,
        [PaymentConfirmedAt] datetime2 NOT NULL,
        [CreatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TeacherSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeacherSubscriptions_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]),
        CONSTRAINT [FK_TeacherSubscriptions_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [Templates] (
        [Id] bigint NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [TeacherId] bigint NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Templates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Templates_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [TutorModuleAccess] (
        [TutorId] bigint NOT NULL,
        [ModuleId] bigint NOT NULL,
        CONSTRAINT [PK_TutorModuleAccess] PRIMARY KEY ([TutorId], [ModuleId]),
        CONSTRAINT [FK_TutorModuleAccess_Models_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [Models] ([Id]),
        CONSTRAINT [FK_TutorModuleAccess_Teachers_TutorId] FOREIGN KEY ([TutorId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [VideoAssetAudits] (
        [Id] bigint NOT NULL IDENTITY,
        [VideoAssetId] bigint NOT NULL,
        [TeacherId] bigint NOT NULL,
        [Action] nvarchar(20) NOT NULL,
        [SnapshotJson] nvarchar(max) NOT NULL,
        [SnapshotArchiveUrl] nvarchar(500) NULL,
        [DeletedByUserId] bigint NULL,
        [DeletedAt] datetime2(0) NOT NULL,
        [CreateAt] datetime2(0) NOT NULL,
        CONSTRAINT [PK_VideoAssetAudits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VideoAssetAudits_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VideoAssetAudits_Users_DeletedByUserId] FOREIGN KEY ([DeletedByUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [VideoAssets] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [SourceUrl] nvarchar(500) NOT NULL,
        [SourceType] tinyint NOT NULL,
        [ExternalId] nvarchar(100) NOT NULL,
        [DurationSeconds] int NOT NULL DEFAULT 0,
        [CreatedByUserId] bigint NULL,
        [CreateAt] datetime2(0) NOT NULL,
        CONSTRAINT [PK_VideoAssets] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_VideoAssets_Id_TeacherId] UNIQUE ([Id], [TeacherId]),
        CONSTRAINT [FK_VideoAssets_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]),
        CONSTRAINT [FK_VideoAssets_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [AssignmentOccurrences] (
        [Id] bigint NOT NULL IDENTITY,
        [TemplateId] bigint NOT NULL,
        [TeacherId] bigint NOT NULL,
        [OccurrenceNumber] int NOT NULL,
        [DueDate] date NOT NULL,
        [Status] tinyint NOT NULL DEFAULT CAST(0 AS tinyint),
        [MaxGradeSnapshot] decimal(8,2) NULL,
        [PassingThresholdSnapshot] decimal(8,2) NULL,
        [TrackingModeSnapshot] tinyint NULL,
        [RowVersion] rowversion NOT NULL,
        [TotalStudentCount] int NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AssignmentOccurrences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssignmentOccurrences_AssignmentTemplates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [AssignmentTemplates] ([Id]),
        CONSTRAINT [FK_AssignmentOccurrences_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [AssistantLoginActivity] (
        [Id] bigint NOT NULL IDENTITY,
        [AssistantId] bigint NOT NULL,
        [ActionType] int NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        [DeviceOrBrowser] nvarchar(max) NULL,
        [IpAddress] nvarchar(max) NULL,
        CONSTRAINT [PK_AssistantLoginActivity] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssistantLoginActivity_Assistants_AssistantId] FOREIGN KEY ([AssistantId]) REFERENCES [Assistants] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [AssistantWallets] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [AssistantId] bigint NOT NULL,
        [AssistantUserId] bigint NOT NULL,
        [CurrentBalance] decimal(12,2) NOT NULL,
        [TotalCollected] decimal(14,2) NOT NULL,
        [TransactionCount] int NOT NULL,
        [LastCollectionAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AssistantWallets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssistantWallets_Assistants_AssistantId] FOREIGN KEY ([AssistantId]) REFERENCES [Assistants] ([Id]),
        CONSTRAINT [FK_AssistantWallets_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [AuditTrial] (
        [Id] bigint NOT NULL IDENTITY,
        [teacherId] bigint NOT NULL,
        [AssistantId] bigint NULL,
        [actionType] int NOT NULL,
        [ModuleId] bigint NOT NULL,
        [Desc] nvarchar(max) NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditTrial] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditTrial_Assistants_AssistantId] FOREIGN KEY ([AssistantId]) REFERENCES [Assistants] ([Id]),
        CONSTRAINT [FK_AuditTrial_Models_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [Models] ([Id]),
        CONSTRAINT [FK_AuditTrial_Teachers_teacherId] FOREIGN KEY ([teacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [AutomatedTriggers] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [MessageTemplateId] bigint NOT NULL,
        [EventType] int NOT NULL,
        [IsActive] bit NOT NULL,
        [SendTiming] int NOT NULL,
        [ScheduledTime] time NULL,
        [Scope] int NOT NULL,
        [SessionId] bigint NULL,
        [SessionGroupId] bigint NULL,
        [ThresholdValue] int NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AutomatedTriggers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AutomatedTriggers_MessageTemplates_MessageTemplateId] FOREIGN KEY ([MessageTemplateId]) REFERENCES [MessageTemplates] ([Id]),
        CONSTRAINT [FK_AutomatedTriggers_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [MessageBlocks] (
        [Id] bigint NOT NULL IDENTITY,
        [MessageTemplateId] bigint NOT NULL,
        [BlockType] int NOT NULL,
        [DynamicKey] int NULL,
        [CustomText] nvarchar(max) NULL,
        [SortOrder] int NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MessageBlocks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MessageBlocks_MessageTemplates_MessageTemplateId] FOREIGN KEY ([MessageTemplateId]) REFERENCES [MessageTemplates] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [MessageLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [StudentId] bigint NOT NULL,
        [StudentCode] nvarchar(max) NOT NULL,
        [StudentName] nvarchar(max) NOT NULL,
        [RecipientPhone] nvarchar(max) NOT NULL,
        [RecipientType] int NOT NULL,
        [MessageTemplateId] bigint NULL,
        [ResolvedContent] nvarchar(max) NOT NULL,
        [Channel] int NOT NULL,
        [Status] int NOT NULL,
        [FailureReason] nvarchar(max) NULL,
        [TriggerType] int NULL,
        [IsManual] bit NOT NULL,
        [SentAt] datetime2 NOT NULL,
        [DeliveredAt] datetime2 NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MessageLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MessageLogs_MessageTemplates_MessageTemplateId] FOREIGN KEY ([MessageTemplateId]) REFERENCES [MessageTemplates] ([Id]),
        CONSTRAINT [FK_MessageLogs_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [Sessions] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [SessionName] nvarchar(200) NOT NULL,
        [OccurrenceType] int NOT NULL,
        [SelectedDays] varchar(20) NULL,
        [MonthlyDayOfMonth] tinyint NULL,
        [PaymentType] int NOT NULL,
        [SessionAmount] decimal(10,2) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [StartTime] time NOT NULL,
        [DurationMinutes] smallint NOT NULL,
        [SessionGroupId] bigint NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Sessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Sessions_SessionGroups_SessionGroupId] FOREIGN KEY ([SessionGroupId]) REFERENCES [SessionGroups] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Sessions_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [TeacherProratedTiers] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherConfigurationId] bigint NOT NULL,
        [TierNumber] int NOT NULL,
        [ThresholdDayStart] int NOT NULL,
        [ThresholdDayEnd] int NOT NULL,
        [FractionRate] decimal(5,4) NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TeacherProratedTiers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeacherProratedTiers_TeacherConfigurations_TeacherConfigurationId] FOREIGN KEY ([TeacherConfigurationId]) REFERENCES [TeacherConfigurations] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [TemplatesPermisions] (
        [TemplateId] bigint NOT NULL,
        [PermisionId] bigint NOT NULL,
        CONSTRAINT [PK_TemplatesPermisions] PRIMARY KEY ([PermisionId], [TemplateId]),
        CONSTRAINT [FK_TemplatesPermisions_Permissions_PermisionId] FOREIGN KEY ([PermisionId]) REFERENCES [Permissions] ([Id]),
        CONSTRAINT [FK_TemplatesPermisions_Templates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [Templates] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [AssignmentDeletionLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [TemplateId] bigint NOT NULL,
        [TeacherId] bigint NOT NULL,
        [DeletionType] tinyint NOT NULL,
        [StudentsAffected] int NOT NULL DEFAULT 0,
        [TemplateSnapshotJson] nvarchar(max) NOT NULL,
        [DeletedByUserId] bigint NULL,
        [DeletedAt] datetime2(0) NULL,
        [LastOccurrenceId] bigint NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AssignmentDeletionLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssignmentDeletionLogs_AssignmentOccurrences_LastOccurrenceId] FOREIGN KEY ([LastOccurrenceId]) REFERENCES [AssignmentOccurrences] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_AssignmentDeletionLogs_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]),
        CONSTRAINT [FK_AssignmentDeletionLogs_Users_DeletedByUserId] FOREIGN KEY ([DeletedByUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [WalletResetLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [AssistantId] bigint NOT NULL,
        [AssistantWalletId] bigint NOT NULL,
        [AmountReset] decimal(12,2) NOT NULL,
        [ResetByUserId] bigint NOT NULL,
        [ResetAt] datetime2(0) NOT NULL,
        [AssistantName] nvarchar(200) NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_WalletResetLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WalletResetLogs_AssistantWallets_AssistantWalletId] FOREIGN KEY ([AssistantWalletId]) REFERENCES [AssistantWallets] ([Id]),
        CONSTRAINT [FK_WalletResetLogs_Assistants_AssistantId] FOREIGN KEY ([AssistantId]) REFERENCES [Assistants] ([Id]),
        CONSTRAINT [FK_WalletResetLogs_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [SessionLinks] (
        [Id] bigint NOT NULL IDENTITY,
        [SessionId] bigint NOT NULL,
        [LinkedSessionId] bigint NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SessionLinks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SessionLinks_Sessions_LinkedSessionId] FOREIGN KEY ([LinkedSessionId]) REFERENCES [Sessions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SessionLinks_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [SessionOccurrences] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [SessionId] bigint NOT NULL,
        [OccurrenceDate] date NOT NULL,
        [Status] tinyint NOT NULL DEFAULT CAST(0 AS tinyint),
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SessionOccurrences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SessionOccurrences_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([Id]),
        CONSTRAINT [FK_SessionOccurrences_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [TeacherStudents] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [StudentName] nvarchar(200) NOT NULL,
        [StudentCode] nvarchar(10) NOT NULL,
        [HashedToken] nvarchar(128) NOT NULL,
        [StudentPhoneNumber] nvarchar(20) NULL,
        [ParentPhoneNumber] nvarchar(20) NULL,
        [Barcode] nvarchar(50) NULL,
        [SessionId] bigint NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TeacherStudents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeacherStudents_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_TeacherStudents_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [TemplatesPermissionsOfUsers] (
        [TemplateId] bigint NOT NULL,
        [AssisstantId] bigint NOT NULL,
        [TemplatePermisionsPermisionId] bigint NULL,
        [TemplatePermisionsTemplateId] bigint NULL,
        CONSTRAINT [PK_TemplatesPermissionsOfUsers] PRIMARY KEY ([AssisstantId], [TemplateId]),
        CONSTRAINT [FK_TemplatesPermissionsOfUsers_Assistants_AssisstantId] FOREIGN KEY ([AssisstantId]) REFERENCES [Assistants] ([Id]),
        CONSTRAINT [FK_TemplatesPermissionsOfUsers_TemplatesPermisions_TemplatePermisionsPermisionId_TemplatePermisionsTemplateId] FOREIGN KEY ([TemplatePermisionsPermisionId], [TemplatePermisionsTemplateId]) REFERENCES [TemplatesPermisions] ([PermisionId], [TemplateId]),
        CONSTRAINT [FK_TemplatesPermissionsOfUsers_Templates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [Templates] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [AssignmentScopes] (
        [Id] bigint NOT NULL IDENTITY,
        [TemplateId] bigint NOT NULL,
        [TeacherId] bigint NOT NULL,
        [ScopeType] tinyint NOT NULL,
        [TeacherStudentId] bigint NULL,
        [SessionId] bigint NULL,
        [SessionGroupId] bigint NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AssignmentScopes] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_AssignmentScopes_ExactlyOneTarget] CHECK ((
                CASE WHEN [TeacherStudentId] IS NULL THEN 0 ELSE 1 END
              + CASE WHEN [SessionId]        IS NULL THEN 0 ELSE 1 END
              + CASE WHEN [SessionGroupId]   IS NULL THEN 0 ELSE 1 END
            ) = 1
            AND (
                ([ScopeType] = 0 AND [TeacherStudentId] IS NOT NULL)
             OR ([ScopeType] = 1 AND [SessionId]        IS NOT NULL)
             OR ([ScopeType] = 2 AND [SessionGroupId]   IS NOT NULL)
            )),
        CONSTRAINT [FK_AssignmentScopes_AssignmentTemplates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [AssignmentTemplates] ([Id]),
        CONSTRAINT [FK_AssignmentScopes_SessionGroups_SessionGroupId] FOREIGN KEY ([SessionGroupId]) REFERENCES [SessionGroups] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_AssignmentScopes_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_AssignmentScopes_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_AssignmentScopes_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [EventStudentObligations] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [PaymentEventId] bigint NOT NULL,
        [TeacherStudentId] bigint NULL,
        [StudentName] nvarchar(200) NULL,
        [StudentCode] nvarchar(20) NULL,
        [AmountDue] decimal(10,2) NOT NULL,
        [AmountPaid] decimal(10,2) NOT NULL,
        [PaymentStatus] tinyint NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_EventStudentObligations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EventStudentObligations_PaymentEvents_PaymentEventId] FOREIGN KEY ([PaymentEventId]) REFERENCES [PaymentEvents] ([Id]),
        CONSTRAINT [FK_EventStudentObligations_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_EventStudentObligations_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [ParentChildTeacherLinks] (
        [Id] bigint NOT NULL IDENTITY,
        [ParentChildId] bigint NOT NULL,
        [TeacherId] bigint NOT NULL,
        [TeacherStudentId] bigint NULL,
        [LinkStatus] int NOT NULL,
        [LinkedAt] datetime2 NOT NULL,
        [UnlinkedAt] datetime2 NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ParentChildTeacherLinks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ParentChildTeacherLinks_ParentChildren_ParentChildId] FOREIGN KEY ([ParentChildId]) REFERENCES [ParentChildren] ([Id]),
        CONSTRAINT [FK_ParentChildTeacherLinks_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_ParentChildTeacherLinks_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [SessionTransferEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [TeacherStudentId] bigint NULL,
        [SourceSessionId] bigint NULL,
        [SourceSessionName] nvarchar(200) NOT NULL,
        [DestinationSessionId] bigint NULL,
        [DestinationSessionName] nvarchar(200) NOT NULL,
        [PaymentStatusAtTransfer] tinyint NOT NULL,
        [OutstandingBalance] decimal(10,2) NOT NULL,
        [CreditBalance] decimal(10,2) NOT NULL,
        [SourcePaymentType] nvarchar(20) NOT NULL,
        [DestinationPaymentType] nvarchar(20) NOT NULL,
        [StudentName] nvarchar(200) NULL,
        [StudentCode] nvarchar(20) NULL,
        [TransferredAt] datetime2(0) NOT NULL,
        [TransferredByUserId] bigint NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SessionTransferEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SessionTransferEvents_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_SessionTransferEvents_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [StudentAbsenceCounters] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [TeacherStudentId] bigint NOT NULL,
        [ConsecutiveAbsences] int NOT NULL,
        [TotalAbsences] int NOT NULL,
        [TotalPresent] int NOT NULL,
        [TotalOccurrences] int NOT NULL,
        [LastAbsenceDate] date NULL,
        [LastAbsenceSessionName] nvarchar(200) NULL,
        [LastAbsenceSessionId] bigint NULL,
        [LastAttendanceDate] date NULL,
        [RowVersion] rowversion NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_StudentAbsenceCounters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudentAbsenceCounters_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]),
        CONSTRAINT [FK_StudentAbsenceCounters_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [StudentAssignmentObligations] (
        [Id] bigint NOT NULL IDENTITY,
        [OccurrenceId] bigint NOT NULL,
        [TeacherStudentId] bigint NOT NULL,
        [TeacherId] bigint NOT NULL,
        [Status] tinyint NOT NULL DEFAULT CAST(0 AS tinyint),
        [GradeValue] decimal(8,2) NULL,
        [IsGradeEntered] bit NOT NULL DEFAULT CAST(0 AS bit),
        [MarkedByScan] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ScannedAt] datetime2 NULL,
        [LastUpdatedByUserId] bigint NULL,
        [UpdatedAt] datetime2(0) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_StudentAssignmentObligations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudentAssignmentObligations_AssignmentOccurrences_OccurrenceId] FOREIGN KEY ([OccurrenceId]) REFERENCES [AssignmentOccurrences] ([Id]),
        CONSTRAINT [FK_StudentAssignmentObligations_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StudentAssignmentObligations_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]),
        CONSTRAINT [FK_StudentAssignmentObligations_Users_LastUpdatedByUserId] FOREIGN KEY ([LastUpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [StudentDepartures] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [TeacherStudentId] bigint NULL,
        [SessionId] bigint NULL,
        [SessionName] nvarchar(200) NOT NULL,
        [StudentName] nvarchar(200) NULL,
        [StudentCode] nvarchar(20) NULL,
        [PaymentStatusAtDeparture] tinyint NOT NULL,
        [TotalOccurrencesInPeriod] int NOT NULL,
        [AttendedOccurrences] int NOT NULL,
        [FullPeriodAmount] decimal(10,2) NOT NULL,
        [ProRatedAmount] decimal(10,2) NOT NULL,
        [FinalAmount] decimal(10,2) NOT NULL,
        [IsTutorOverride] bit NOT NULL,
        [OriginalCalculatedAmount] decimal(10,2) NOT NULL,
        [DepartureOutcome] tinyint NOT NULL,
        [ConfirmedByUserId] bigint NULL,
        [DepartedAt] datetime2(0) NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_StudentDepartures] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudentDepartures_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([Id]),
        CONSTRAINT [FK_StudentDepartures_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_StudentDepartures_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [StudentPaymentCounters] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [TeacherStudentId] bigint NOT NULL,
        [ConsecutiveUnpaid] int NOT NULL,
        [TotalUnpaidPeriods] int NOT NULL,
        [TotalPaidPeriods] int NOT NULL,
        [TotalAmountPaid] decimal(12,2) NOT NULL,
        [TotalOutstanding] decimal(12,2) NOT NULL,
        [CustomPaymentAmount] decimal(10,2) NULL,
        [LastPaymentDate] datetime2 NULL,
        [LastPaymentSessionName] nvarchar(200) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_StudentPaymentCounters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudentPaymentCounters_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]),
        CONSTRAINT [FK_StudentPaymentCounters_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [StudentSessionAssignments] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [TeacherStudentId] bigint NULL,
        [SessionId] bigint NULL,
        [SessionName] nvarchar(200) NOT NULL,
        [StudentName] nvarchar(200) NULL,
        [StudentCode] nvarchar(20) NULL,
        [AssignedAt] datetime2 NOT NULL,
        [UnassignedAt] datetime2 NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_StudentSessionAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudentSessionAssignments_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([Id]),
        CONSTRAINT [FK_StudentSessionAssignments_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_StudentSessionAssignments_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [StudentTeacherLinks] (
        [Id] bigint NOT NULL IDENTITY,
        [StudentUserId] bigint NOT NULL,
        [TeacherId] bigint NOT NULL,
        [TeacherStudentId] bigint NULL,
        [LinkStatus] int NOT NULL,
        [LinkedAt] datetime2 NOT NULL,
        [UnlinkedAt] datetime2 NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_StudentTeacherLinks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudentTeacherLinks_StudentUsers_StudentUserId] FOREIGN KEY ([StudentUserId]) REFERENCES [StudentUsers] ([Id]),
        CONSTRAINT [FK_StudentTeacherLinks_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_StudentTeacherLinks_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [VideoAnalytics] (
        [Id] bigint NOT NULL IDENTITY,
        [VideoAssetId] bigint NOT NULL,
        [TeacherId] bigint NOT NULL,
        [TeacherStudentId] bigint NOT NULL,
        [OpenCount] int NOT NULL DEFAULT 0,
        [TotalWatchSeconds] bigint NOT NULL DEFAULT CAST(0 AS bigint),
        [FirstOpenedAt] datetime2(0) NOT NULL,
        [LastUpdated] datetime2(0) NOT NULL,
        [VideoDurationAtFirstWatch] int NOT NULL,
        [LastResumePositionSeconds] int NOT NULL DEFAULT 0,
        [CreateAt] datetime2(0) NOT NULL,
        CONSTRAINT [PK_VideoAnalytics] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VideoAnalytics_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]),
        CONSTRAINT [FK_VideoAnalytics_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]),
        CONSTRAINT [FK_VideoAnalytics_VideoAssets_VideoAssetId_TeacherId] FOREIGN KEY ([VideoAssetId], [TeacherId]) REFERENCES [VideoAssets] ([Id], [TeacherId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [VideoScopes] (
        [Id] bigint NOT NULL IDENTITY,
        [VideoAssetId] bigint NOT NULL,
        [TeacherId] bigint NOT NULL,
        [ScopeType] tinyint NOT NULL,
        [TeacherStudentId] bigint NULL,
        [SessionId] bigint NULL,
        [SessionGroupId] bigint NULL,
        [AssignedByUserId] bigint NOT NULL,
        [AssignedAt] datetime2(0) NOT NULL,
        [CreateAt] datetime2(0) NOT NULL,
        CONSTRAINT [PK_VideoScopes] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_VideoScopes_ExactlyOneTarget] CHECK ((CASE WHEN [TeacherStudentId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [SessionId]        IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [SessionGroupId]   IS NOT NULL THEN 1 ELSE 0 END) = 1),
        CONSTRAINT [CK_VideoScopes_ScopeTypeMatchesFK] CHECK (([ScopeType] = 0 AND [TeacherStudentId] IS NOT NULL) OR ([ScopeType] = 1 AND [SessionId] IS NOT NULL) OR ([ScopeType] = 2 AND [SessionGroupId] IS NOT NULL)),
        CONSTRAINT [FK_VideoScopes_SessionGroups_SessionGroupId] FOREIGN KEY ([SessionGroupId]) REFERENCES [SessionGroups] ([Id]),
        CONSTRAINT [FK_VideoScopes_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([Id]),
        CONSTRAINT [FK_VideoScopes_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]),
        CONSTRAINT [FK_VideoScopes_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]),
        CONSTRAINT [FK_VideoScopes_Users_AssignedByUserId] FOREIGN KEY ([AssignedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VideoScopes_VideoAssets_VideoAssetId_TeacherId] FOREIGN KEY ([VideoAssetId], [TeacherId]) REFERENCES [VideoAssets] ([Id], [TeacherId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [VideoWatchEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [VideoAssetId] bigint NOT NULL,
        [TeacherId] bigint NOT NULL,
        [TeacherStudentId] bigint NOT NULL,
        [DeviceId] nvarchar(100) NOT NULL,
        [EventType] tinyint NOT NULL,
        [PositionSeconds] int NOT NULL,
        [DeltaSinceLastSeconds] int NOT NULL DEFAULT 0,
        [EventUtc] datetime2(0) NOT NULL,
        [ClientEventId] uniqueidentifier NULL,
        [CreateAt] datetime2(0) NOT NULL,
        CONSTRAINT [PK_VideoWatchEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VideoWatchEvents_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]),
        CONSTRAINT [FK_VideoWatchEvents_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]),
        CONSTRAINT [FK_VideoWatchEvents_VideoAssets_VideoAssetId_TeacherId] FOREIGN KEY ([VideoAssetId], [TeacherId]) REFERENCES [VideoAssets] ([Id], [TeacherId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [EventPaymentTransactions] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [PaymentEventId] bigint NULL,
        [EventStudentObligationId] bigint NULL,
        [TeacherStudentId] bigint NULL,
        [AmountPaid] decimal(10,2) NOT NULL,
        [PaymentMethod] tinyint NOT NULL,
        [CollectedByUserId] bigint NULL,
        [StudentName] nvarchar(200) NULL,
        [StudentCode] nvarchar(20) NULL,
        [EventName] nvarchar(300) NOT NULL,
        [CollectedAt] datetime2(0) NOT NULL,
        [IsOnlinePayment] bit NOT NULL,
        [OnlineTransactionRef] nvarchar(200) NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_EventPaymentTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EventPaymentTransactions_EventStudentObligations_EventStudentObligationId] FOREIGN KEY ([EventStudentObligationId]) REFERENCES [EventStudentObligations] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_EventPaymentTransactions_PaymentEvents_PaymentEventId] FOREIGN KEY ([PaymentEventId]) REFERENCES [PaymentEvents] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_EventPaymentTransactions_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_EventPaymentTransactions_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [StudentObligationAuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [StudentObligationId] bigint NOT NULL,
        [TeacherId] bigint NOT NULL,
        [OldStatus] tinyint NOT NULL,
        [NewStatus] tinyint NOT NULL,
        [OldGradeValue] decimal(8,2) NULL,
        [NewGradeValue] decimal(8,2) NULL,
        [MaxGradeSnapshot] decimal(8,2) NULL,
        [PassingThresholdSnapshot] decimal(8,2) NULL,
        [ChangeReason] nvarchar(500) NULL,
        [ChangedByUserId] bigint NULL,
        [ChangedAt] datetime2(0) NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_StudentObligationAuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudentObligationAuditLogs_StudentAssignmentObligations_StudentObligationId] FOREIGN KEY ([StudentObligationId]) REFERENCES [StudentAssignmentObligations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StudentObligationAuditLogs_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]),
        CONSTRAINT [FK_StudentObligationAuditLogs_Users_ChangedByUserId] FOREIGN KEY ([ChangedByUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [AttendanceRecords] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [SessionOccurrenceId] bigint NULL,
        [TeacherStudentId] bigint NULL,
        [StudentSessionAssignmentId] bigint NULL,
        [StudentName] nvarchar(200) NULL,
        [StudentCode] nvarchar(20) NULL,
        [SessionId] bigint NULL,
        [SessionName] nvarchar(200) NOT NULL,
        [OccurrenceDate] date NOT NULL,
        [SessionGroupId] bigint NULL,
        [Status] tinyint NOT NULL,
        [AttendanceMethod] tinyint NOT NULL,
        [IsCrossSession] bit NOT NULL,
        [CrossSessionId] bigint NULL,
        [CrossSessionName] nvarchar(200) NULL,
        [CrossSessionOccurrenceDate] date NULL,
        [RecordedAt] datetime2(0) NOT NULL,
        [RecordedByUserId] bigint NULL,
        [IsEdited] bit NOT NULL,
        [LastEditedAt] datetime2(0) NULL,
        [LastEditedByUserId] bigint NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AttendanceRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AttendanceRecords_SessionOccurrences_SessionOccurrenceId] FOREIGN KEY ([SessionOccurrenceId]) REFERENCES [SessionOccurrences] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_AttendanceRecords_StudentSessionAssignments_StudentSessionAssignmentId] FOREIGN KEY ([StudentSessionAssignmentId]) REFERENCES [StudentSessionAssignments] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_AttendanceRecords_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_AttendanceRecords_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [PaymentPeriods] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [SessionId] bigint NULL,
        [TeacherStudentId] bigint NULL,
        [StudentSessionAssignmentId] bigint NULL,
        [PeriodType] tinyint NOT NULL,
        [PeriodStart] date NOT NULL,
        [PeriodEnd] date NOT NULL,
        [AmountDue] decimal(10,2) NOT NULL,
        [AmountPaid] decimal(10,2) NOT NULL,
        [PaymentStatus] tinyint NOT NULL,
        [IsProRated] bit NOT NULL,
        [ProRatedFraction] decimal(5,4) NOT NULL,
        [PeriodSequence] int NOT NULL,
        [IsCarriedForward] bit NOT NULL,
        [OriginSessionName] nvarchar(200) NULL,
        [SessionName] nvarchar(200) NOT NULL,
        [StudentName] nvarchar(200) NULL,
        [StudentCode] nvarchar(20) NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentPeriods] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentPeriods_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([Id]),
        CONSTRAINT [FK_PaymentPeriods_StudentSessionAssignments_StudentSessionAssignmentId] FOREIGN KEY ([StudentSessionAssignmentId]) REFERENCES [StudentSessionAssignments] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_PaymentPeriods_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_PaymentPeriods_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [AttendanceEditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [AttendanceRecordId] bigint NULL,
        [PreviousStatus] tinyint NOT NULL,
        [NewStatus] tinyint NOT NULL,
        [PreviousAttendanceMethod] tinyint NOT NULL,
        [NewAttendanceMethod] tinyint NOT NULL,
        [EditedAt] datetime2(0) NOT NULL,
        [EditedByUserId] bigint NULL,
        [EditReason] nvarchar(500) NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AttendanceEditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AttendanceEditLogs_AttendanceRecords_AttendanceRecordId] FOREIGN KEY ([AttendanceRecordId]) REFERENCES [AttendanceRecords] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [PaymentTransactions] (
        [Id] bigint NOT NULL IDENTITY,
        [TeacherId] bigint NOT NULL,
        [TeacherStudentId] bigint NULL,
        [SessionId] bigint NULL,
        [SessionOccurrenceId] bigint NULL,
        [PaymentPeriodId] bigint NULL,
        [StudentSessionAssignmentId] bigint NULL,
        [AmountDue] decimal(10,2) NOT NULL,
        [AmountPaid] decimal(10,2) NOT NULL,
        [PaymentMethod] tinyint NOT NULL,
        [PaymentTransactionStatus] tinyint NOT NULL,
        [CollectedByUserId] bigint NULL,
        [StudentName] nvarchar(200) NULL,
        [StudentCode] nvarchar(20) NULL,
        [SessionName] nvarchar(200) NOT NULL,
        [CollectedAt] datetime2(0) NOT NULL,
        [LocalCollectedAt] datetime2(0) NOT NULL,
        [IsPartial] bit NOT NULL,
        [IsProRated] bit NOT NULL,
        [ProRatedTierLabel] nvarchar(200) NULL,
        [IsOnlinePayment] bit NOT NULL,
        [OnlineTransactionRef] nvarchar(200) NULL,
        [IsOfflineRecord] bit NOT NULL,
        [OfflineDeviceId] nvarchar(100) NULL,
        [SyncStatus] tinyint NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2(0) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentTransactions_PaymentPeriods_PaymentPeriodId] FOREIGN KEY ([PaymentPeriodId]) REFERENCES [PaymentPeriods] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_PaymentTransactions_SessionOccurrences_SessionOccurrenceId] FOREIGN KEY ([SessionOccurrenceId]) REFERENCES [SessionOccurrences] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_PaymentTransactions_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([Id]),
        CONSTRAINT [FK_PaymentTransactions_StudentSessionAssignments_StudentSessionAssignmentId] FOREIGN KEY ([StudentSessionAssignmentId]) REFERENCES [StudentSessionAssignments] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_PaymentTransactions_TeacherStudents_TeacherStudentId] FOREIGN KEY ([TeacherStudentId]) REFERENCES [TeacherStudents] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_PaymentTransactions_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE TABLE [PaymentEditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [PaymentTransactionId] bigint NULL,
        [EditAction] tinyint NOT NULL,
        [PreviousAmount] decimal(10,2) NOT NULL,
        [NewAmount] decimal(10,2) NOT NULL,
        [PreviousStatus] tinyint NOT NULL,
        [NewStatus] tinyint NOT NULL,
        [EditedByUserId] bigint NULL,
        [EditedAt] datetime2(0) NOT NULL,
        [EditReason] nvarchar(500) NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentEditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentEditLogs_PaymentTransactions_PaymentTransactionId] FOREIGN KEY ([PaymentTransactionId]) REFERENCES [PaymentTransactions] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreateAt', N'DisplayOrder', N'IsActive', N'NameAr', N'NameEn') AND [object_id] = OBJECT_ID(N'[Subjects]'))
        SET IDENTITY_INSERT [Subjects] ON;
    EXEC(N'INSERT INTO [Subjects] ([Id], [CreateAt], [DisplayOrder], [IsActive], [NameAr], [NameEn])
    VALUES (CAST(1 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 1, CAST(1 AS bit), N''اللغة العربية'', N''Arabic Language''),
    (CAST(2 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 2, CAST(1 AS bit), N''اللغة الإنجليزية'', N''English Language''),
    (CAST(3 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 3, CAST(1 AS bit), N''الرياضيات'', N''Mathematics''),
    (CAST(4 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 4, CAST(1 AS bit), N''العلوم'', N''Science''),
    (CAST(5 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 5, CAST(1 AS bit), N''الدراسات الاجتماعية'', N''Social Studies''),
    (CAST(6 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 6, CAST(1 AS bit), N''اللغة الفرنسية'', N''French Language''),
    (CAST(7 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 7, CAST(1 AS bit), N''اللغة الألمانية'', N''German Language''),
    (CAST(8 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 8, CAST(1 AS bit), N''الفيزياء'', N''Physics''),
    (CAST(9 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 9, CAST(1 AS bit), N''الكيمياء'', N''Chemistry''),
    (CAST(10 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 10, CAST(1 AS bit), N''الأحياء'', N''Biology''),
    (CAST(11 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 11, CAST(1 AS bit), N''الجغرافيا'', N''Geography''),
    (CAST(12 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 12, CAST(1 AS bit), N''التاريخ'', N''History''),
    (CAST(13 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 13, CAST(1 AS bit), N''الفلسفة والمنطق'', N''Philosophy & Logic''),
    (CAST(14 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 14, CAST(1 AS bit), N''علم النفس'', N''Psychology''),
    (CAST(15 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 15, CAST(1 AS bit), N''اللغة الإيطالية'', N''Italian Language''),
    (CAST(16 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 16, CAST(1 AS bit), N''اللغة الإسبانية'', N''Spanish Language''),
    (CAST(17 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 17, CAST(1 AS bit), N''علوم الحاسب'', N''Computer Science''),
    (CAST(18 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 18, CAST(1 AS bit), N''التربية الدينية'', N''Religious Studies''),
    (CAST(19 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 19, CAST(1 AS bit), N''التربية الفنية'', N''Art Education''),
    (CAST(20 AS bigint), ''2026-01-01T00:00:00.0000000Z'', 20, CAST(1 AS bit), N''التربية الموسيقية'', N''Music Education'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreateAt', N'DisplayOrder', N'IsActive', N'NameAr', N'NameEn') AND [object_id] = OBJECT_ID(N'[Subjects]'))
        SET IDENTITY_INSERT [Subjects] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AssignmentDeletionLogs_DeletedByUserId] ON [AssignmentDeletionLogs] ([DeletedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AssignmentDeletionLogs_LastOccurrenceId] ON [AssignmentDeletionLogs] ([LastOccurrenceId]) WHERE [LastOccurrenceId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AssignmentDeletionLogs_TeacherId_DeletedAt] ON [AssignmentDeletionLogs] ([TeacherId], [DeletedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AssignmentDeletionLogs_TemplateId] ON [AssignmentDeletionLogs] ([TemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AssignmentOccurrences_DueDate] ON [AssignmentOccurrences] ([TeacherId], [DueDate], [TemplateId]) INCLUDE ([Status], [OccurrenceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AssignmentOccurrences_TeacherId_Status] ON [AssignmentOccurrences] ([TeacherId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [UX_AssignmentOccurrences_Template_OccurrenceNumber] ON [AssignmentOccurrences] ([TemplateId], [OccurrenceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AssignmentScopes_SessionGroupId] ON [AssignmentScopes] ([SessionGroupId]) WHERE [SessionGroupId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AssignmentScopes_SessionId] ON [AssignmentScopes] ([SessionId]) WHERE [SessionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AssignmentScopes_TeacherId] ON [AssignmentScopes] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AssignmentScopes_TeacherStudentId] ON [AssignmentScopes] ([TeacherStudentId]) WHERE [TeacherStudentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AssignmentScopes_Template] ON [AssignmentScopes] ([TemplateId], [ScopeType]) INCLUDE ([TeacherStudentId], [SessionId], [SessionGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AssignmentTemplates_CreatedByUserId] ON [AssignmentTemplates] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AssignmentTemplates_RecurrenceScheduler] ON [AssignmentTemplates] ([IsRecurring], [IsRecurrenceStopped], [RecurrenceEndDate]) WHERE [IsRecurring] = 1 AND [IsRecurrenceStopped] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AssignmentTemplates_TeacherList] ON [AssignmentTemplates] ([TeacherId], [AssignmentType], [IsRecurring], [CreateAt] DESC) INCLUDE ([Name], [NameAr]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AssistantLoginActivity_AssistantId] ON [AssistantLoginActivity] ([AssistantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_Assistants_TeacherAccountId] ON [Assistants] ([TeacherAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_Assistants_UserId] ON [Assistants] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AssistantWallets_AssistantId] ON [AssistantWallets] ([AssistantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AW_TeacherId_AssistantId] ON [AssistantWallets] ([TeacherId], [AssistantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AW_TeacherId_AssistantUserId] ON [AssistantWallets] ([TeacherId], [AssistantUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AEL_AttendanceRecordId] ON [AttendanceEditLogs] ([AttendanceRecordId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AR_ConsecutiveAbsenceCalc] ON [AttendanceRecords] ([TeacherStudentId], [SessionId], [OccurrenceDate], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AR_CrossSession] ON [AttendanceRecords] ([TeacherId], [TeacherStudentId], [IsCrossSession]) WHERE [IsCrossSession] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AR_PostDeletion_DuplicateGuard] ON [AttendanceRecords] ([TeacherStudentId], [OccurrenceDate], [SessionName]) WHERE [SessionOccurrenceId] IS NULL AND [SessionId] IS NULL AND [TeacherStudentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AR_SessionOccurrenceId] ON [AttendanceRecords] ([SessionOccurrenceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AR_TeacherId_OccurrenceDate_Status] ON [AttendanceRecords] ([TeacherId], [OccurrenceDate], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AR_TeacherId_SessionGroupId] ON [AttendanceRecords] ([TeacherId], [SessionGroupId]) WHERE [SessionGroupId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AR_TeacherId_SessionId_OccurrenceDate] ON [AttendanceRecords] ([TeacherId], [SessionId], [OccurrenceDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AR_TeacherStudentId_OccurrenceDate] ON [AttendanceRecords] ([TeacherStudentId], [OccurrenceDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AR_TeacherStudentId_OccurrenceDate_SessionId] ON [AttendanceRecords] ([TeacherStudentId], [OccurrenceDate], [SessionId]) WHERE [TeacherStudentId] IS NOT NULL AND [SessionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AR_TeacherStudentId_SessionOccurrenceId] ON [AttendanceRecords] ([TeacherStudentId], [SessionOccurrenceId]) WHERE [SessionOccurrenceId] IS NOT NULL AND [TeacherStudentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AttendanceRecords_StudentSessionAssignmentId] ON [AttendanceRecords] ([StudentSessionAssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AuditTrial_AssistantId] ON [AuditTrial] ([AssistantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AuditTrial_ModuleId] ON [AuditTrial] ([ModuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AuditTrial_teacherId] ON [AuditTrial] ([teacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AutomatedTriggers_MessageTemplateId] ON [AutomatedTriggers] ([MessageTemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_AutomatedTriggers_TeacherId] ON [AutomatedTriggers] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_EPT_TeacherId_EventId] ON [EventPaymentTransactions] ([TeacherId], [PaymentEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_EventPaymentTransactions_EventStudentObligationId] ON [EventPaymentTransactions] ([EventStudentObligationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_EventPaymentTransactions_PaymentEventId] ON [EventPaymentTransactions] ([PaymentEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_EventPaymentTransactions_TeacherStudentId] ON [EventPaymentTransactions] ([TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_ESO_EventId_Status] ON [EventStudentObligations] ([PaymentEventId], [PaymentStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ESO_EventId_StudentId] ON [EventStudentObligations] ([PaymentEventId], [TeacherStudentId]) WHERE [TeacherStudentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_EventStudentObligations_TeacherId] ON [EventStudentObligations] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_EventStudentObligations_TeacherStudentId] ON [EventStudentObligations] ([TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_GoogleUsers_UserId] ON [GoogleUsers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_MessageBlocks_MessageTemplateId] ON [MessageBlocks] ([MessageTemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_MessageLogs_MessageTemplateId] ON [MessageLogs] ([MessageTemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_MessageLogs_TeacherId] ON [MessageLogs] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MessageTemplate_TeacherId_Name] ON [MessageTemplates] ([TeacherId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_MessageTemplates_Name] ON [MessageTemplates] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_MessageTemplates_TeacherId] ON [MessageTemplates] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_MessagingChannels_TeacherId] ON [MessagingChannels] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_ParentChildren_ParentUserId_IsActive] ON [ParentChildren] ([ParentUserId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ParentChildren_ParentUserId_StudentUserId] ON [ParentChildren] ([ParentUserId], [StudentUserId]) WHERE [StudentUserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ParentChildren_ParentUserId_StudentUserId_IsActive] ON [ParentChildren] ([ParentUserId], [StudentUserId], [IsActive]) WHERE [StudentUserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_ParentChildren_StudentUserId] ON [ParentChildren] ([StudentUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_ParentChildTeacherLinks_ChildId_LinkStatus] ON [ParentChildTeacherLinks] ([ParentChildId], [LinkStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ParentChildTeacherLinks_ChildId_TeacherId] ON [ParentChildTeacherLinks] ([ParentChildId], [TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_ParentChildTeacherLinks_TeacherId] ON [ParentChildTeacherLinks] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_ParentChildTeacherLinks_TeacherStudentId] ON [ParentChildTeacherLinks] ([TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_ParentUsers_AccountStatus_DeletedAt] ON [ParentUsers] ([AccountStatus], [DeletedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ParentUsers_UserId] ON [ParentUsers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PEL_PaymentTransactionId] ON [PaymentEditLogs] ([PaymentTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PE_TeacherId_IsDeleted] ON [PaymentEvents] ([TeacherId], [IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PaymentPeriods_SessionId] ON [PaymentPeriods] ([SessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PaymentPeriods_StudentSessionAssignmentId] ON [PaymentPeriods] ([StudentSessionAssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PaymentPeriods_TeacherStudentId] ON [PaymentPeriods] ([TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PP_EarliestUnpaid] ON [PaymentPeriods] ([TeacherId], [TeacherStudentId], [PaymentStatus], [PeriodSequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PP_TeacherId_SessionId_PeriodDates] ON [PaymentPeriods] ([TeacherId], [SessionId], [PeriodStart], [PeriodEnd]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PP_TeacherId_SessionId_Status] ON [PaymentPeriods] ([TeacherId], [SessionId], [PaymentStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PP_TeacherId_StudentId_Sequence] ON [PaymentPeriods] ([TeacherId], [TeacherStudentId], [PeriodSequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PaymentTransactions_SessionId] ON [PaymentTransactions] ([SessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PaymentTransactions_SessionOccurrenceId] ON [PaymentTransactions] ([SessionOccurrenceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PaymentTransactions_StudentSessionAssignmentId] ON [PaymentTransactions] ([StudentSessionAssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PaymentTransactions_TeacherStudentId] ON [PaymentTransactions] ([TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PT_PaymentPeriodId] ON [PaymentTransactions] ([PaymentPeriodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PT_TeacherId_CollectorId_CollectedAt] ON [PaymentTransactions] ([TeacherId], [CollectedByUserId], [CollectedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PT_TeacherId_IsDeleted] ON [PaymentTransactions] ([TeacherId], [IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PT_TeacherId_SessionId_CollectedAt] ON [PaymentTransactions] ([TeacherId], [SessionId], [CollectedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PT_TeacherId_StudentId_CollectedAt] ON [PaymentTransactions] ([TeacherId], [TeacherStudentId], [CollectedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PT_TeacherId_StudentId_LocalDate] ON [PaymentTransactions] ([TeacherId], [TeacherStudentId], [LocalCollectedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PendingSubscriptionPayments_PaymobSessionId] ON [PendingSubscriptionPayments] ([PaymobSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PendingSubscriptionPayments_ResolvedByUserId] ON [PendingSubscriptionPayments] ([ResolvedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_PendingSubscriptionPayments_TeacherId_Status] ON [PendingSubscriptionPayments] ([TeacherId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_Permissions_ModuleId] ON [Permissions] ([ModuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_SessionGroups_TeacherId] ON [SessionGroups] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SessionGroups_TeacherId_GroupName] ON [SessionGroups] ([TeacherId], [GroupName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_SessionLinks_LinkedSessionId] ON [SessionLinks] ([LinkedSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SessionLinks_SessionId_LinkedSessionId] ON [SessionLinks] ([SessionId], [LinkedSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SessionOccurrences_SessionId_OccurrenceDate] ON [SessionOccurrences] ([SessionId], [OccurrenceDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_SessionOccurrences_TeacherId_OccurrenceDate] ON [SessionOccurrences] ([TeacherId], [OccurrenceDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_SessionOccurrences_TeacherId_Status] ON [SessionOccurrences] ([TeacherId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_Sessions_SessionGroupId] ON [Sessions] ([SessionGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_Sessions_TeacherId] ON [Sessions] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_Sessions_TeacherId_EndDate] ON [Sessions] ([TeacherId], [EndDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_Sessions_TeacherId_OccurrenceType_SelectedDays] ON [Sessions] ([TeacherId], [OccurrenceType], [SelectedDays]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Sessions_TeacherId_SessionName] ON [Sessions] ([TeacherId], [SessionName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_SessionTransferEvents_TeacherStudentId] ON [SessionTransferEvents] ([TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_STE_TeacherId_StudentId] ON [SessionTransferEvents] ([TeacherId], [TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_SAC_TeacherId_ConsecutiveAbsences] ON [StudentAbsenceCounters] ([TeacherId], [ConsecutiveAbsences]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SAC_TeacherId_TeacherStudentId] ON [StudentAbsenceCounters] ([TeacherId], [TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_SAC_TeacherStudentId] ON [StudentAbsenceCounters] ([TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_StudentAssignmentObligations_Absence] ON [StudentAssignmentObligations] ([TeacherId], [TeacherStudentId], [OccurrenceId]) INCLUDE ([Status]) WHERE [Status] IN (2, 5)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentAssignmentObligations_LastUpdatedByUserId] ON [StudentAssignmentObligations] ([LastUpdatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_StudentAssignmentObligations_PendingGrades] ON [StudentAssignmentObligations] ([TeacherId], [OccurrenceId]) INCLUDE ([TeacherStudentId]) WHERE [Status] IN (3, 6)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentAssignmentObligations_StudentHistory] ON [StudentAssignmentObligations] ([TeacherId], [TeacherStudentId], [CreateAt] DESC) INCLUDE ([OccurrenceId], [Status], [GradeValue], [IsGradeEntered]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentAssignmentObligations_TeacherStudentId] ON [StudentAssignmentObligations] ([TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentAssignmentObligations_Tracking] ON [StudentAssignmentObligations] ([TeacherId], [OccurrenceId], [Status]) INCLUDE ([TeacherStudentId], [GradeValue], [IsGradeEntered], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [UX_StudentAssignmentObligations_Occurrence_Student] ON [StudentAssignmentObligations] ([OccurrenceId], [TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentCapacityPackages_IsActive] ON [StudentCapacityPackages] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentCapacityPackages_PriceUpdatedByUserId] ON [StudentCapacityPackages] ([PriceUpdatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_SD_TeacherId_StudentId] ON [StudentDepartures] ([TeacherId], [TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentDepartures_SessionId] ON [StudentDepartures] ([SessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentDepartures_TeacherStudentId] ON [StudentDepartures] ([TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentObligationAuditLogs_ChangedByUserId] ON [StudentObligationAuditLogs] ([ChangedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentObligationAuditLogs_Obligation_ChangedAt] ON [StudentObligationAuditLogs] ([StudentObligationId], [ChangedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentObligationAuditLogs_TeacherId_ChangedAt] ON [StudentObligationAuditLogs] ([TeacherId], [ChangedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_SPC_TeacherId_ConsecutiveUnpaid] ON [StudentPaymentCounters] ([TeacherId], [ConsecutiveUnpaid]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SPC_TeacherId_StudentId] ON [StudentPaymentCounters] ([TeacherId], [TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_SPC_TeacherId_TotalOutstanding] ON [StudentPaymentCounters] ([TeacherId], [TotalOutstanding]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentPaymentCounters_TeacherStudentId] ON [StudentPaymentCounters] ([TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_SSA_SessionId_IsActive] ON [StudentSessionAssignments] ([SessionId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_SSA_TeacherId_TeacherStudentId] ON [StudentSessionAssignments] ([TeacherId], [TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_SSA_TeacherStudentId_IsActive] ON [StudentSessionAssignments] ([TeacherStudentId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentTeacherLinks_StudentUserId_LinkStatus] ON [StudentTeacherLinks] ([StudentUserId], [LinkStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StudentTeacherLinks_StudentUserId_TeacherId] ON [StudentTeacherLinks] ([StudentUserId], [TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentTeacherLinks_TeacherId] ON [StudentTeacherLinks] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentTeacherLinks_TeacherStudentId] ON [StudentTeacherLinks] ([TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_StudentUsers_AccountStatus_DeletedAt] ON [StudentUsers] ([AccountStatus], [DeletedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StudentUsers_StudentAccountCode] ON [StudentUsers] ([StudentAccountCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StudentUsers_UserId] ON [StudentUsers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_Subjects_IsActive] ON [Subjects] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SubscriptionAlerts_Key] ON [SubscriptionAlerts] ([TeacherId], [SubscriptionEndDate], [AlertDay]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TeacherConfigurations_TeacherId] ON [TeacherConfigurations] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TeacherProratedTiers_ConfigId_TierNumber] ON [TeacherProratedTiers] ([TeacherConfigurationId], [TierNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_Teachers_AccountStatus_DeletedAt] ON [Teachers] ([AccountStatus], [DeletedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_Teachers_CreatedByUserId] ON [Teachers] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_Teachers_StudentCapacityPackageId] ON [Teachers] ([StudentCapacityPackageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Teachers_TeacherCode] ON [Teachers] ([TeacherCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Teachers_UserId] ON [Teachers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_TeacherStudents_LinkingLookup] ON [TeacherStudents] ([TeacherId], [StudentCode], [HashedToken]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TeacherStudents_RecycleBin_DeletedAt] ON [TeacherStudents] ([IsDeleted], [DeletedAt]) WHERE [IsDeleted] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_TeacherStudents_SessionId] ON [TeacherStudents] ([SessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_TeacherStudents_TeacherId_IsDeleted] ON [TeacherStudents] ([TeacherId], [IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_TeacherStudents_TeacherId_SessionId] ON [TeacherStudents] ([TeacherId], [SessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TeacherStudents_TeacherId_StudentCode] ON [TeacherStudents] ([TeacherId], [StudentCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_TeacherStudents_TeacherId_StudentName] ON [TeacherStudents] ([TeacherId], [StudentName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_TeacherSubjects_SubjectId] ON [TeacherSubjects] ([SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TeacherSubjects_TeacherId_SubjectId] ON [TeacherSubjects] ([TeacherId], [SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_TeacherSubscriptions_CreatedByUserId] ON [TeacherSubscriptions] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TeacherSubscriptions_Current] ON [TeacherSubscriptions] ([TeacherId]) WHERE [IsCurrent] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_TeacherSubscriptions_EndDate] ON [TeacherSubscriptions] ([EndDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_TeacherSubscriptions_TeacherId_EndDate] ON [TeacherSubscriptions] ([TeacherId], [EndDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_Templates_TeacherId] ON [Templates] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_TemplatesPermisions_TemplateId] ON [TemplatesPermisions] ([TemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_TemplatesPermissionsOfUsers_TemplateId] ON [TemplatesPermissionsOfUsers] ([TemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_TemplatesPermissionsOfUsers_TemplatePermisionsPermisionId_TemplatePermisionsTemplateId] ON [TemplatesPermissionsOfUsers] ([TemplatePermisionsPermisionId], [TemplatePermisionsTemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_TutorModuleAccess_ModuleId] ON [TutorModuleAccess] ([ModuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserDeviceTokens_UserId_FcmToken] ON [UserDeviceTokens] ([UserId], [FcmToken]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_UserDeviceTokens_UserId_IsActive] ON [UserDeviceTokens] ([UserId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_UserNotifications_UserId_IsRead_SentAt] ON [UserNotifications] ([UserId], [IsRead], [SentAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_Users_CreateByUserId] ON [Users] ([CreateByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Users_PhoneNumber] ON [Users] ([PhoneNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_UsersPermissions_PermissionId] ON [UsersPermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_UserTutor_TutorId] ON [UserTutor] ([TutorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoAnalytics_TeacherId] ON [VideoAnalytics] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoAnalytics_TeacherStudentId_VideoAssetId] ON [VideoAnalytics] ([TeacherStudentId], [VideoAssetId]) INCLUDE ([OpenCount], [TotalWatchSeconds], [LastResumePositionSeconds], [LastUpdated]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoAnalytics_VideoAssetId_Includes] ON [VideoAnalytics] ([VideoAssetId]) INCLUDE ([TeacherStudentId], [OpenCount], [TotalWatchSeconds], [LastUpdated]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoAnalytics_VideoAssetId_TeacherId] ON [VideoAnalytics] ([VideoAssetId], [TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [UX_VideoAnalytics_Video_Student] ON [VideoAnalytics] ([VideoAssetId], [TeacherStudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoAssetAudits_DeletedByUserId] ON [VideoAssetAudits] ([DeletedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoAssetAudits_TeacherId_DeletedAt] ON [VideoAssetAudits] ([TeacherId], [DeletedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoAssetAudits_VideoAssetId] ON [VideoAssetAudits] ([VideoAssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoAssets_CreatedByUserId] ON [VideoAssets] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoAssets_TeacherId_CreatedAt] ON [VideoAssets] ([TeacherId], [CreateAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoAssets_TeacherId_ExternalId] ON [VideoAssets] ([TeacherId], [ExternalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [UX_VideoAssets_Id_TeacherId] ON [VideoAssets] ([Id], [TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoScopes_AssignedByUserId] ON [VideoScopes] ([AssignedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_VideoScopes_SessionGroupId] ON [VideoScopes] ([SessionGroupId]) INCLUDE ([VideoAssetId], [AssignedAt]) WHERE [SessionGroupId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_VideoScopes_SessionId] ON [VideoScopes] ([SessionId]) INCLUDE ([VideoAssetId], [AssignedAt]) WHERE [SessionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoScopes_TeacherId] ON [VideoScopes] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_VideoScopes_TeacherStudentId] ON [VideoScopes] ([TeacherStudentId]) INCLUDE ([VideoAssetId], [AssignedAt]) WHERE [TeacherStudentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoScopes_VideoAssetId] ON [VideoScopes] ([VideoAssetId]) INCLUDE ([ScopeType], [TeacherStudentId], [SessionId], [SessionGroupId], [AssignedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoScopes_VideoAssetId_TeacherId] ON [VideoScopes] ([VideoAssetId], [TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE UNIQUE INDEX [UX_VideoScopes_Video_Type_Target] ON [VideoScopes] ([VideoAssetId], [ScopeType], [TeacherStudentId], [SessionId], [SessionGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoWatchEvents_TeacherId] ON [VideoWatchEvents] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VideoWatchEvents_VideoAssetId_TeacherId] ON [VideoWatchEvents] ([VideoAssetId], [TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VWE_Student_Video_Device_TimeDesc] ON [VideoWatchEvents] ([TeacherStudentId], [VideoAssetId], [DeviceId], [EventUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_VWE_VideoAssetId_EventUtcDesc] ON [VideoWatchEvents] ([VideoAssetId], [EventUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_VWE_ClientEventId] ON [VideoWatchEvents] ([ClientEventId]) WHERE [ClientEventId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_WalletResetLogs_AssistantId] ON [WalletResetLogs] ([AssistantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_WalletResetLogs_AssistantWalletId] ON [WalletResetLogs] ([AssistantWalletId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    CREATE INDEX [IX_WRL_TeacherId_AssistantId] ON [WalletResetLogs] ([TeacherId], [AssistantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618103029_init'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618103029_init', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [AssistantLoginActivity] DROP CONSTRAINT [FK_AssistantLoginActivity_Assistants_AssistantId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [AuditTrial] DROP CONSTRAINT [FK_AuditTrial_Models_ModuleId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [AuditTrial] DROP CONSTRAINT [FK_AuditTrial_Teachers_teacherId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [AutomatedTriggers] DROP CONSTRAINT [FK_AutomatedTriggers_MessageTemplates_MessageTemplateId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [AutomatedTriggers] DROP CONSTRAINT [FK_AutomatedTriggers_Teachers_TeacherId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [MessageBlocks] DROP CONSTRAINT [FK_MessageBlocks_MessageTemplates_MessageTemplateId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [MessageLogs] DROP CONSTRAINT [FK_MessageLogs_Teachers_TeacherId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [MessageTemplates] DROP CONSTRAINT [FK_MessageTemplates_Teachers_TeacherId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [MessagingChannels] DROP CONSTRAINT [FK_MessagingChannels_Teachers_TeacherId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [Permissions] DROP CONSTRAINT [FK_Permissions_Models_ModuleId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [RefreshTokens] DROP CONSTRAINT [FK_RefreshTokens_Users_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [Templates] DROP CONSTRAINT [FK_Templates_Teachers_TeacherId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [TemplatesPermisions] DROP CONSTRAINT [FK_TemplatesPermisions_Permissions_PermisionId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [TemplatesPermisions] DROP CONSTRAINT [FK_TemplatesPermisions_Templates_TemplateId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [TemplatesPermissionsOfUsers] DROP CONSTRAINT [FK_TemplatesPermissionsOfUsers_Assistants_AssisstantId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [TemplatesPermissionsOfUsers] DROP CONSTRAINT [FK_TemplatesPermissionsOfUsers_Templates_TemplateId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [TutorModuleAccess] DROP CONSTRAINT [FK_TutorModuleAccess_Models_ModuleId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [TutorModuleAccess] DROP CONSTRAINT [FK_TutorModuleAccess_Teachers_TutorId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [UsersPermissions] DROP CONSTRAINT [FK_UsersPermissions_Permissions_PermissionId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [UsersPermissions] DROP CONSTRAINT [FK_UsersPermissions_Users_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [UserTutor] DROP CONSTRAINT [FK_UserTutor_Users_TutorId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [UserTutor] DROP CONSTRAINT [FK_UserTutor_Users_userId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [VideoAnalytics] DROP CONSTRAINT [FK_VideoAnalytics_Teachers_TeacherId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [VideoScopes] DROP CONSTRAINT [FK_VideoScopes_Teachers_TeacherId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    CREATE TABLE [Conversations] (
        [Id] bigint NOT NULL IDENTITY,
        [ParticipantAUserId] bigint NOT NULL,
        [ParticipantBUserId] bigint NOT NULL,
        [LastMessageAt] datetime2 NULL,
        [LastMessagePreview] nvarchar(200) NULL,
        [LastMessageSenderUserId] bigint NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Conversations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Conversations_Users_ParticipantAUserId] FOREIGN KEY ([ParticipantAUserId]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_Conversations_Users_ParticipantBUserId] FOREIGN KEY ([ParticipantBUserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    CREATE TABLE [ChatMessages] (
        [Id] bigint NOT NULL IDENTITY,
        [ConversationId] bigint NOT NULL,
        [SenderUserId] bigint NOT NULL,
        [Body] nvarchar(4000) NOT NULL,
        [SentAt] datetime2 NOT NULL,
        [IsRead] bit NOT NULL,
        [ReadAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [CreateAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatMessages_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([Id]),
        CONSTRAINT [FK_ChatMessages_Users_SenderUserId] FOREIGN KEY ([SenderUserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_Conversation_Sender_IsRead] ON [ChatMessages] ([ConversationId], [SenderUserId], [IsRead]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_ConversationId_SentAt] ON [ChatMessages] ([ConversationId], [SentAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_SenderUserId] ON [ChatMessages] ([SenderUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    CREATE INDEX [IX_Conversations_ParticipantA_LastMessageAt] ON [Conversations] ([ParticipantAUserId], [LastMessageAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    CREATE INDEX [IX_Conversations_ParticipantB_LastMessageAt] ON [Conversations] ([ParticipantBUserId], [LastMessageAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Conversations_Participants] ON [Conversations] ([ParticipantAUserId], [ParticipantBUserId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [AssistantLoginActivity] ADD CONSTRAINT [FK_AssistantLoginActivity_Assistants_AssistantId] FOREIGN KEY ([AssistantId]) REFERENCES [Assistants] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [AuditTrial] ADD CONSTRAINT [FK_AuditTrial_Models_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [Models] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [AuditTrial] ADD CONSTRAINT [FK_AuditTrial_Teachers_teacherId] FOREIGN KEY ([teacherId]) REFERENCES [Teachers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [AutomatedTriggers] ADD CONSTRAINT [FK_AutomatedTriggers_MessageTemplates_MessageTemplateId] FOREIGN KEY ([MessageTemplateId]) REFERENCES [MessageTemplates] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [AutomatedTriggers] ADD CONSTRAINT [FK_AutomatedTriggers_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [MessageBlocks] ADD CONSTRAINT [FK_MessageBlocks_MessageTemplates_MessageTemplateId] FOREIGN KEY ([MessageTemplateId]) REFERENCES [MessageTemplates] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [MessageLogs] ADD CONSTRAINT [FK_MessageLogs_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [MessageTemplates] ADD CONSTRAINT [FK_MessageTemplates_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [MessagingChannels] ADD CONSTRAINT [FK_MessagingChannels_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [Permissions] ADD CONSTRAINT [FK_Permissions_Models_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [Models] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [RefreshTokens] ADD CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [Templates] ADD CONSTRAINT [FK_Templates_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [TemplatesPermisions] ADD CONSTRAINT [FK_TemplatesPermisions_Permissions_PermisionId] FOREIGN KEY ([PermisionId]) REFERENCES [Permissions] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [TemplatesPermisions] ADD CONSTRAINT [FK_TemplatesPermisions_Templates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [Templates] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [TemplatesPermissionsOfUsers] ADD CONSTRAINT [FK_TemplatesPermissionsOfUsers_Assistants_AssisstantId] FOREIGN KEY ([AssisstantId]) REFERENCES [Assistants] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [TemplatesPermissionsOfUsers] ADD CONSTRAINT [FK_TemplatesPermissionsOfUsers_Templates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [Templates] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [TutorModuleAccess] ADD CONSTRAINT [FK_TutorModuleAccess_Models_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [Models] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [TutorModuleAccess] ADD CONSTRAINT [FK_TutorModuleAccess_Teachers_TutorId] FOREIGN KEY ([TutorId]) REFERENCES [Teachers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [UsersPermissions] ADD CONSTRAINT [FK_UsersPermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [UsersPermissions] ADD CONSTRAINT [FK_UsersPermissions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [UserTutor] ADD CONSTRAINT [FK_UserTutor_Users_TutorId] FOREIGN KEY ([TutorId]) REFERENCES [Users] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [UserTutor] ADD CONSTRAINT [FK_UserTutor_Users_userId] FOREIGN KEY ([userId]) REFERENCES [Users] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [VideoAnalytics] ADD CONSTRAINT [FK_VideoAnalytics_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    ALTER TABLE [VideoScopes] ADD CONSTRAINT [FK_VideoScopes_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141459_add Chat_AddConversationAndMessage'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260627141459_add Chat_AddConversationAndMessage', N'10.0.5');
END;

COMMIT;
GO

