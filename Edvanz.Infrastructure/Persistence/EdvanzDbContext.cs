using DocumentFormat.OpenXml.Vml.Office;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Entities.Chat;
using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Edvanz.Infrastructure.Persistence;

public class EdvanzDbContext(DbContextOptions<EdvanzDbContext> options) : DbContext(options)
{
    // ─── Existing tables ───
    public DbSet<User> Users { get; set; }
    public DbSet<UsersTutor> UserTutor { get; set; }
    public DbSet<UsersPermission> UsersPermissions { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<GoogleUser> GoogleUsers { get; set; }
    public DbSet<Module> Models { get; set; }
    public DbSet<Template> Templates { get; set; }
    public DbSet<TemplatePermissionsUsers> TemplatesPermissionsOfUsers { get; set; }
    public DbSet<TemplatePermisions> TemplatesPermisions { get; set; }
    public DbSet<TutorModule> TutorModuleAccess { get; set; }




    // ─── Teacher module tables ───
    public DbSet<Teacher> Teachers { get; set; }
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<TeacherSubject> TeacherSubjects { get; set; }
    public DbSet<StudentCapacityPackage> StudentCapacityPackages { get; set; }
    public DbSet<TeacherConfiguration> TeacherConfigurations { get; set; }
    public DbSet<TeacherProratedTier> TeacherProratedTiers { get; set; }
    public DbSet<TeacherSubscription> TeacherSubscriptions { get; set; }

    // ─── Student User module tables (AAM-FR-05) ───
    public DbSet<TeacherStudent> TeacherStudents { get; set; }
    public DbSet<StudentUser> StudentUsers { get; set; }
    public DbSet<StudentTeacherLink> StudentTeacherLinks { get; set; }

    // ─── Parent User module tables (AAM-FR-06) ───
    public DbSet<ParentUser> ParentUsers { get; set; }
    public DbSet<ParentChild> ParentChildren { get; set; }
    public DbSet<ParentChildTeacherLink> ParentChildTeacherLinks { get; set; }

    // ─── Session module tables (Module 2) ───
    public DbSet<Session> Sessions { get; set; }
    public DbSet<SessionGroup> SessionGroups { get; set; }
    public DbSet<SessionLink> SessionLinks { get; set; }

    // ─── Attendance Module (Module 3) ───
    public DbSet<SessionOccurrence> SessionOccurrences { get; set; }
    public DbSet<StudentSessionAssignment> StudentSessionAssignments { get; set; }
    public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
    public DbSet<AttendanceEditLog> AttendanceEditLogs { get; set; }
    public DbSet<StudentAbsenceCounter> StudentAbsenceCounters { get; set; }

    // ─── Payment Module (Module 4) ───
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    public DbSet<PaymentTransactionAllocation> PaymentTransactionAllocations { get; set; }
    public DbSet<PaymentPeriod> PaymentPeriods { get; set; }
    public DbSet<StudentPaymentCounter> StudentPaymentCounters { get; set; }
    public DbSet<AssistantWallet> AssistantWallets { get; set; }
    public DbSet<WalletResetLog> WalletResetLogs { get; set; }
    public DbSet<PaymentEditLog> PaymentEditLogs { get; set; }
    public DbSet<StudentDeparture> StudentDepartures { get; set; }
    public DbSet<SessionTransferEvent> SessionTransferEvents { get; set; }

    // ─── Event Payment Module (Module 5) ───
    public DbSet<PaymentEvent> PaymentEvents { get; set; }
    public DbSet<EventStudentObligation> EventStudentObligations { get; set; }
    public DbSet<EventPaymentTransaction> EventPaymentTransactions { get; set; }


    //  ─── Assistant  ───
    public DbSet<Assistant> Assistants { get; set; }
    public DbSet<LoginActivityAssistantLog> AssistantLoginActivity { get; set; }
    public DbSet<AuditTrail> AuditTrial { get; set; }
    // ─── Subscription Management Module (Module 11 — v1.2) ───
    public DbSet<PendingSubscriptionPayment> PendingSubscriptionPayments { get; set; }
    public DbSet<SubscriptionAlert> SubscriptionAlerts { get; set; }
    public DbSet<CapacityIncreaseRequest> CapacityIncreaseRequests { get; set; }
    public DbSet<SubscriptionPricingSetting> SubscriptionPricingSettings { get; set; }
    public DbSet<UserNotification> UserNotifications { get; set; }
    public DbSet<UserDeviceToken> UserDeviceTokens { get; set; }


    //  ─── Messaging  ───
    public DbSet<MessagingChannel> MessagingChannels => Set<MessagingChannel>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<MessageBlock> MessageBlocks => Set<MessageBlock>();
    public DbSet<AutomatedTrigger> AutomatedTriggers => Set<AutomatedTrigger>();
    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();

    // ════════════════════════════════════════════════════════════════════════════
    // REQUIRED DbSet<> ADDITIONS
    // ════════════════════════════════════════════════════════════════════════════
    // Add the following six properties to the EdvanzDbContext class body alongside
    // the existing DbSet declarations (e.g., near the other module DbSets):
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Exam &amp; Homework Module (Module 6) — assignment configuration templates.
    /// </summary>
    public DbSet<AssignmentTemplate> AssignmentTemplates => Set<AssignmentTemplate>();

    /// <summary>
    /// Exam &amp; Homework Module (Module 6) — targeting rules for assignment templates.
    /// </summary>
    public DbSet<AssignmentScope> AssignmentScopes => Set<AssignmentScope>();

    /// <summary>
    /// Exam &amp; Homework Module (Module 6) — materialized assignment instances per due date.
    /// </summary>
    public DbSet<AssignmentOccurrence> AssignmentOccurrences => Set<AssignmentOccurrence>();

    /// <summary>
    /// Exam &amp; Homework Module (Module 6) — per-student state on each assignment occurrence.
    /// Hot table backing the Assignment Tracking View and Grade Entry View.
    /// </summary>
    public DbSet<StudentAssignmentObligation> StudentAssignmentObligations
        => Set<StudentAssignmentObligation>();

    /// <summary>
    /// Exam &amp; Homework Module (Module 6) — append-only audit trail of obligation changes.
    /// </summary>
    public DbSet<StudentObligationAuditLog> StudentObligationAuditLogs
        => Set<StudentObligationAuditLog>();

    /// <summary>
    /// Exam &amp; Homework Module (Module 6) — JSON-snapshot record of deleted/stopped templates.
    /// </summary>
    public DbSet<AssignmentDeletionLog> AssignmentDeletionLogs => Set<AssignmentDeletionLog>();
    // ════════════════════════════════════════════════════════════════════════════
    // REQUIRED DbSet<> ADDITIONS — VIDEO CONTENT MANAGEMENT MODULE (Module 14)
    // ════════════════════════════════════════════════════════════════════════════
    // Splice these five properties into the EdvanzDbContext class body alongside
    // the existing module DbSets (e.g., right after the Module 6 declarations
    // "AssignmentDeletionLogs"). Using the lambda-Set<>() form for consistency with
    // the Messaging and Module 6 declarations.
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Video Content Management Module (Module 14) — canonical video records owned
    /// by teachers. One row per <c>POST /api/videos</c> call. Hard-deleted (no soft
    /// delete); deletion captures a JSON snapshot in <c>VideoAssetAudit</c>.
    /// </summary>
    public DbSet<VideoAsset> VideoAssets => Set<VideoAsset>();

    /// <summary>
    /// Video Content Management Module (Module 14) — targeting rules per video.
    /// Multi-row scope pattern mirroring <c>AssignmentScope</c> from Module 6.
    /// </summary>
    public DbSet<VideoScope> VideoScopes => Set<VideoScope>();

    /// <summary>
    /// Video Content Management Module (Module 14) — per-student per-video aggregate.
    /// Atomic UPSERT target via <c>ExecuteUpdateAsync</c> for multi-device watch tracking.
    /// </summary>
    public DbSet<VideoAnalytics> VideoAnalytics => Set<VideoAnalytics>();

    /// <summary>
    /// Video Content Management Module (Module 14) — append-only Open/Stop event log.
    /// Drives delta validation, resume calculation, and idempotency via ClientEventId.
    /// </summary>
    public DbSet<VideoWatchEvent> VideoWatchEvents => Set<VideoWatchEvent>();

    public DbSet<VideoAssetAudit> VideoAssetAudits => Set<VideoAssetAudit>();

    /// <summary>
    /// Video Content Management Module (Module 14) — organizational grouping
    /// of a teacher's videos (Track C / G-UNIT). Soft-deleted; optional
    /// relationship to <see cref="VideoAsset"/> (loose videos allowed).
    /// </summary>
    public DbSet<VideoUnit> VideoUnits => Set<VideoUnit>();

    /// <summary>
    /// Video Content Management Module (Module 14) — M:N join rows between
    /// <see cref="VideoAsset"/> and <see cref="VideoUnit"/>. A video can belong to
    /// multiple units; access is the union of the video's own scope OR any linked
    /// unit's scope.
    /// </summary>
    public DbSet<VideoAssetUnit> VideoAssetUnits => Set<VideoAssetUnit>();

    /// <summary>
    /// Video Content Management Module (Module 14) — collection-level (unit)
    /// Target Scope rows. Structurally identical to <see cref="VideoScope"/>
    /// but targets a <see cref="VideoUnit"/>. A student authorized by either
    /// a video's own scope OR its unit's scope gets access.
    /// </summary>
    public DbSet<VideoUnitScope> VideoUnitScopes => Set<VideoUnitScope>();

    // VideoAttachment table dropped — folded into the FileObject registry (see FileObjects DbSet).

    /// <summary>
    /// Video Content Management Module (Module 14) — an exam attached to a
    /// video at creation time. One video has at most one exam.
    /// </summary>
    public DbSet<VideoExam> VideoExams => Set<VideoExam>();

    /// <summary>
    /// Video Content Management Module (Module 14) — questions within a
    /// VideoExam.
    /// </summary>
    public DbSet<VideoExamQuestion> VideoExamQuestions => Set<VideoExamQuestion>();

    /// <summary>
    /// Video Content Management Module (Module 14) — answer options for a
    /// VideoExamQuestion, with the IsCorrect answer key.
    /// </summary>
    public DbSet<VideoExamQuestionOption> VideoExamQuestionOptions => Set<VideoExamQuestionOption>();

    /// <summary>
    /// Reference table of per-module free-tier creation quotas (see ModuleQuotaKeys).
    /// </summary>
    public DbSet<ModuleQuota> ModuleQuotas => Set<ModuleQuota>();

    /// <summary>
    /// Central file registry — one row per uploaded file, served through the gated
    /// <c>GET /api/files/{fileId}</c> endpoint. See <see cref="FileObject"/>.
    /// </summary>
    public DbSet<FileObject> FileObjects => Set<FileObject>();
    // ════════════════════════════════════════════════════════════════════════════
    // DIRECT CHAT (1:1 two-way messaging — supersedes AAM-FR-07 one-way)
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 1:1 direct-message conversations. One row per canonical participant pair.
    /// </summary>
    public DbSet<Conversation> Conversations => Set<Conversation>();

    /// <summary>
    /// Messages within a 1:1 conversation.
    /// </summary>
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    // ════════════════════════════════════════════════════════════════════════════
    // ONLINE EXAM MODULE — DbSet<> ADDITIONS
    // ════════════════════════════════════════════════════════════════════════════

    public DbSet<OnlineExam> OnlineExams => Set<OnlineExam>();
    public DbSet<OnlineExamQuestion> OnlineExamQuestions => Set<OnlineExamQuestion>();
    public DbSet<OnlineExamQuestionOption> OnlineExamQuestionOptions => Set<OnlineExamQuestionOption>();
    public DbSet<OnlineExamScope> OnlineExamScopes => Set<OnlineExamScope>();
    public DbSet<StudentOnlineExamReport> StudentOnlineExamReports => Set<StudentOnlineExamReport>();
    public DbSet<StudentQuestionAnswer> StudentQuestionAnswers => Set<StudentQuestionAnswer>();
    public DbSet<StudentQuestionAnswerOption> StudentQuestionAnswerOptions => Set<StudentQuestionAnswerOption>();

    // Student video-quiz attempt aggregate (Module 14) — video twin of the online-exam report tables.
    public DbSet<StudentVideoExamReport> StudentVideoExamReports => Set<StudentVideoExamReport>();
    public DbSet<StudentVideoExamAnswer> StudentVideoExamAnswers => Set<StudentVideoExamAnswer>();
    public DbSet<StudentVideoExamAnswerOption> StudentVideoExamAnswerOptions => Set<StudentVideoExamAnswerOption>();


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

        optionsBuilder.UseSqlServer(opt => opt.CommandTimeout(300));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ════════════════════════════════════════════════
        // EXISTING TABLE CONFIGURATION (preserved)
        // ════════════════════════════════════════════════

        #region Existing composite keys
        modelBuilder.Entity<UsersPermission>()
            .HasKey(ap => new { ap.UserId, ap.PermissionId });

        modelBuilder.Entity<UsersTutor>()
            .HasKey(ut => new { ut.userId, ut.TutorId });
        modelBuilder.Entity<TemplatePermissionsUsers>()
           .HasKey(ur => new { ur.AssisstantId, ur.TemplateId });
        modelBuilder.Entity<TemplatePermisions>()
           .HasKey(ur => new { ur.PermisionId, ur.TemplateId });
        modelBuilder.Entity<TutorModule>()
        .HasKey(ur => new { ur.TutorId, ur.ModuleId });

        #endregion

        #region Existing unique constraints
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>().Property(u => u.PhoneNumber)
            .HasMaxLength(20);

        // Phone is OPTIONAL. The unique index is FILTERED to non-null values so any number of
        // phone-less users are allowed, while a supplied phone stays globally unique. A non-filtered
        // unique index rejects a 2nd NULL/'' phone — that was the assistant-create "conflict with
        // existing data" bug. The old redundant non-unique IX_User_Phnoe is dropped.
        modelBuilder.Entity<User>().HasIndex(u => u.PhoneNumber)
              .IsUnique()
              .HasFilter("[PhoneNumber] IS NOT NULL")
              .HasDatabaseName("UX_Users_PhoneNumber");

        // National-ID image is a registry file reference (FileObject.Id), replacing the former
        // inline IdImage varbinary. Fluent-only, NoAction; no inverse navigation on FileObject.
        modelBuilder.Entity<User>()
            .HasOne<FileObject>()
            .WithMany()
            .HasForeignKey(u => u.IdImageFileId)
            .OnDelete(DeleteBehavior.NoAction);

        #endregion

        // ════════════════════════════════════════════════
        // TEACHER MODULE CONFIGURATION
        // ════════════════════════════════════════════════

        #region Teacher
        modelBuilder.Entity<Teacher>(entity =>
        {
            entity.ToTable("Teachers");

            // TeacherCode: unique, immutable, 8-digit (AAM-FR-03.3 / AAM-BR-05)
            entity.Property(t => t.TeacherCode)
                .HasMaxLength(8)
                .IsRequired();

            entity.HasIndex(t => t.TeacherCode)
                .IsUnique()
                .HasDatabaseName("IX_Teachers_TeacherCode");

            // UserId: one-to-one with User
            entity.HasIndex(t => t.UserId)
                .IsUnique()
                .HasDatabaseName("IX_Teachers_UserId");

            entity.HasOne(t => t.User)
                .WithOne()
                .HasForeignKey<Teacher>(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // CreatedByUser: audit trail
            entity.HasOne(t => t.CreatedByUser)
                .WithMany()
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // StudentCapacityPackage: optional FK
            entity.HasOne(t => t.StudentCapacityPackage)
                .WithMany(p => p.Teachers)
                .HasForeignKey(t => t.StudentCapacityPackageId)
                .OnDelete(DeleteBehavior.SetNull);

            // LanguagePreference: short code
            entity.Property(t => t.LanguagePreference)
                .HasMaxLength(5);

            // CustomSubject: free text
            entity.Property(t => t.CustomSubject)
                .HasMaxLength(200);

            // Soft-delete filter: queries exclude deleted records by default
            entity.HasQueryFilter(t => t.DeletedAt == null);

            // Performance index for active, non-deleted teachers
            entity.HasIndex(t => new { t.AccountStatus, t.DeletedAt })
                .HasDatabaseName("IX_Teachers_AccountStatus_DeletedAt");
        });
        #endregion

        #region Subject (lookup)
        modelBuilder.Entity<Subject>(entity =>
        {
            entity.ToTable("Subjects");

            entity.Property(s => s.NameEn)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(s => s.NameAr)
                .HasMaxLength(150)
                .IsRequired();

            entity.HasIndex(s => s.IsActive)
                .HasDatabaseName("IX_Subjects_IsActive");
        });
        #endregion

        #region TeacherSubject (junction)
        modelBuilder.Entity<TeacherSubject>(entity =>
        {
            entity.ToTable("TeacherSubjects");

            // Prevent duplicate teacher-subject assignments
            entity.HasIndex(ts => new { ts.TeacherId, ts.SubjectId })
                .IsUnique()
                .HasDatabaseName("IX_TeacherSubjects_TeacherId_SubjectId");

            entity.HasOne(ts => ts.Teacher)
                .WithMany(t => t.TeacherSubjects)
                .HasForeignKey(ts => ts.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(ts => ts.Subject)
                .WithMany(s => s.TeacherSubjects)
                .HasForeignKey(ts => ts.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        #endregion

        #region StudentCapacityPackage (lookup)
        modelBuilder.Entity<StudentCapacityPackage>(entity =>
        {
            entity.ToTable("StudentCapacityPackages");

            entity.Property(p => p.Name)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(p => p.IsActive)
                .HasDatabaseName("IX_StudentCapacityPackages_IsActive");
            // ── Pricing fields (added for Subscription Management Module — §5.6) ──

            // Money: decimal(10,2) consistent with all EGP financial columns.
            entity.Property(p => p.MonthlyPriceEGP)
                .HasColumnType("decimal(10,2)")
                .HasDefaultValue(0m);

            // Audit FK: keep the package row even if the admin user is removed.
            entity.HasOne(p => p.PriceUpdatedByUser)
                .WithMany()
                .HasForeignKey(p => p.PriceUpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        #endregion

        #region TeacherConfiguration (1:1 with Teacher)
        modelBuilder.Entity<TeacherConfiguration>(entity =>
        {
            entity.ToTable("TeacherConfigurations");

            // Enforce one-to-one: unique index on TeacherId
            entity.HasIndex(tc => tc.TeacherId)
                .IsUnique()
                .HasDatabaseName("IX_TeacherConfigurations_TeacherId");

            entity.HasOne(tc => tc.Teacher)
                .WithOne(t => t.Configuration)
                .HasForeignKey<TeacherConfiguration>(tc => tc.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        #endregion

        #region TeacherProratedTier (1:N from TeacherConfiguration)
        modelBuilder.Entity<TeacherProratedTier>(entity =>
        {
            entity.ToTable("TeacherProratedTiers");

            entity.Property(pt => pt.FractionRate)
                .HasColumnType("decimal(5,4)");

            // Enforce unique tier numbers per configuration
            entity.HasIndex(pt => new { pt.TeacherConfigurationId, pt.TierNumber })
                .IsUnique()
                .HasDatabaseName("IX_TeacherProratedTiers_ConfigId_TierNumber");

            entity.HasOne(pt => pt.TeacherConfiguration)
                .WithMany(tc => tc.ProratedTiers)
                .HasForeignKey(pt => pt.TeacherConfigurationId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        #endregion

        #region TeacherSubscription (1:N from Teacher)
        //modelBuilder.Entity<TeacherSubscription>(entity =>
        //{
        //    entity.ToTable("TeacherSubscriptions");

        //    entity.HasOne(s => s.Teacher)
        //        .WithMany(t => t.Subscriptions)
        //        .HasForeignKey(s => s.TeacherId)
        //        .OnDelete(DeleteBehavior.NoAction);

        //    entity.HasOne(s => s.CreatedByUser)
        //        .WithMany()
        //        .HasForeignKey(s => s.CreatedByUserId)
        //        .OnDelete(DeleteBehavior.SetNull);

        //    // Index for subscription expiry queries (AAM-FR-08, REQ-SUB-005)
        //    entity.HasIndex(s => new { s.TeacherId, s.EndDate })
        //        .HasDatabaseName("IX_TeacherSubscriptions_TeacherId_EndDate");

        //    entity.HasIndex(s => s.SubscriptionStatus)
        //        .HasDatabaseName("IX_TeacherSubscriptions_Status");
        //});
        modelBuilder.Entity<TeacherSubscription>(entity =>
        {
            entity.ToTable("TeacherSubscriptions");

            // ── Relationships (unchanged) ────────────────────────────────
            entity.HasOne(s => s.Teacher)
                .WithMany(t => t.Subscriptions)
                .HasForeignKey(s => s.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(s => s.CreatedByUser)
                .WithMany()
                .HasForeignKey(s => s.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // ── Monetary & concurrency mappings (NEW) ────────────────────
            entity.Property(s => s.AmountPaidEGP)
                .HasColumnType("decimal(10,2)");

            entity.Property(s => s.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.Property(s => s.TransactionReference)
                .HasMaxLength(100);

            // EncryptedPaymentDetails: nvarchar(max) by default — no explicit mapping needed.

            // ── Indexes ──────────────────────────────────────────────────

            // (RETAINED) Subscription expiry scan index — used by the reminder dispatcher
            // (REQ-SUB-005) and general end-date range queries.
            entity.HasIndex(s => new { s.TeacherId, s.EndDate })
                .HasDatabaseName("IX_TeacherSubscriptions_TeacherId_EndDate");

            // (NEW) End-date scan index — used by Section 9.5 Migration M1 and by the
            // historical reporting view.
            entity.HasIndex(s => s.EndDate)
                .HasDatabaseName("IX_TeacherSubscriptions_EndDate");

            // (NEW) FILTERED UNIQUE INDEX — enforces BR-SUB-006 at the database level.
            // Exactly one TeacherSubscription row per teacher has IsCurrent = true.
            // Two concurrent renewal confirmations racing to insert will collide here
            // (the second fails with SQL 2601 unique violation), and the service's
            // bounded retry catches and rolls back safely.
            entity.HasIndex(s => s.TeacherId)
                .HasFilter("[IsCurrent] = 1")
                .IsUnique()
                .HasDatabaseName("IX_TeacherSubscriptions_Current");

            // (REMOVED — DO NOT ADD BACK)
            //   The previous IX_TeacherSubscriptions_Status index is gone. The column
            //   it indexed no longer exists (Critique C-6 / D-08). Queries that filtered
            //   by status now derive status in-memory or via vw_TeacherSubscriptionStatus.
        });
        // ════════════════════════════════════════════════
        // SUBSCRIPTION MANAGEMENT MODULE CONFIGURATION (Module 11 — v1.2)
        // ════════════════════════════════════════════════

        #region PendingSubscriptionPayment (REQ-SUB-019, FR-SUB-035, BR-SUB-008)
        modelBuilder.Entity<PendingSubscriptionPayment>(entity =>
        {
            entity.ToTable("PendingSubscriptionPayments");

            // Money: decimal(10,2) consistent with all EGP financial columns in the system.
            entity.Property(p => p.AmountEGP)
                .HasColumnType("decimal(10,2)");

            // Paymob session lookup: nvarchar(200) per §5.2.
            entity.Property(p => p.PaymobSessionId)
                .HasMaxLength(200);

            // Manually entered tutor reference (Vodafone Cash / InstaPay tx id).
            entity.Property(p => p.SubmittedTransactionReference)
                .HasMaxLength(100);

            // RejectionReason: nvarchar(500), surfaced to the teacher in PaymentRejected notification.
            entity.Property(p => p.RejectionReason)
                .HasMaxLength(500);

            // EncryptedSubmittedDetails: nvarchar(max) by default — no explicit mapping needed.

            // ── Relationships ──
            // NoAction on the owning Teacher: pending payments are tenant-scoped.
            entity.HasOne(p => p.Teacher)
                .WithMany(t => t.PendingSubscriptionPayments)
                .HasForeignKey(p => p.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // ResolvedByUser is an audit FK — keep the row even if the admin user is removed.
            entity.HasOne(p => p.ResolvedByUser)
                .WithMany()
                .HasForeignKey(p => p.ResolvedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // ── Indexes (§9.1) ──

            // Admin pending-queue listing + per-teacher status lookup.
            entity.HasIndex(p => new { p.TeacherId, p.Status })
                .HasDatabaseName("IX_PendingSubscriptionPayments_TeacherId_Status");

            // Paymob webhook lookup: incoming callback knows only the Paymob session id.
            entity.HasIndex(p => p.PaymobSessionId)
                .HasDatabaseName("IX_PendingSubscriptionPayments_PaymobSessionId");
        });
        #endregion

        #region SubscriptionAlert (REQ-SUB-005, Critique C-7)
        modelBuilder.Entity<SubscriptionAlert>(entity =>
        {
            entity.ToTable("SubscriptionAlerts");

            // SubscriptionEndDate is part of the idempotency key — store as date (no time component).
            entity.Property(a => a.SubscriptionEndDate)
                .HasColumnType("date");

            // ── Relationships ──
            entity.HasOne(a => a.Teacher)
                .WithMany(t => t.SubscriptionAlerts)
                .HasForeignKey(a => a.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // ── Idempotency index (§9.1, FR-SUB-014) ──
            // Uniqueness is enforced at the DB so the per-teacher reminder job can race-safely
            // INSERT and lose to a competing worker without producing duplicate notifications.
            entity.HasIndex(a => new { a.TeacherId, a.SubscriptionEndDate, a.AlertDay })
                .IsUnique()
                .HasDatabaseName("IX_SubscriptionAlerts_Key");
        });
        #endregion

        #region SubscriptionPricingSetting (per-student pricing: renewal = capacity × rate)
        modelBuilder.Entity<SubscriptionPricingSetting>(entity =>
        {
            entity.ToTable("SubscriptionPricingSettings");

            // Money: decimal(10,2) consistent with all EGP financial columns in the system.
            entity.Property(p => p.PricePerStudentEGP)
                .HasColumnType("decimal(10,2)");

            // UpdatedByUser is an audit FK — keep the row even if the admin user is removed.
            entity.HasOne(p => p.UpdatedByUser)
                .WithMany()
                .HasForeignKey(p => p.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Seed the single settings row: 1 student = 2.50 EGP / month.
            // Static values only (HasData requirement).
            entity.HasData(new SubscriptionPricingSetting
            {
                Id = 1,
                PricePerStudentEGP = 2.50m,
                CreateAt = new DateTime(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc)
            });
        });
        #endregion

        #region CapacityIncreaseRequest (teacher-requested StudentCapacity raise, admin-approved)
        modelBuilder.Entity<CapacityIncreaseRequest>(entity =>
        {
            entity.ToTable("CapacityIncreaseRequests");

            entity.Property(r => r.Note)
                .HasMaxLength(500);

            // RejectionReason: nvarchar(500), surfaced to the teacher in the rejection notification.
            entity.Property(r => r.RejectionReason)
                .HasMaxLength(500);

            // ── Relationships ──
            // NoAction on the owning Teacher: requests are tenant-scoped audit rows.
            entity.HasOne(r => r.Teacher)
                .WithMany()
                .HasForeignKey(r => r.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // ResolvedByUser is an audit FK — keep the row even if the admin user is removed.
            entity.HasOne(r => r.ResolvedByUser)
                .WithMany()
                .HasForeignKey(r => r.ResolvedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // ── Indexes ──
            // One LIVE Pending request per teacher; terminal rows accumulate for audit.
            // Keep the [Status] literal in sync with CapacityRequestStatus.Pending = 1
            // (StudentTeacherLink filtered-index precedent).
            entity.HasIndex(r => r.TeacherId)
                .IsUnique()
                .HasFilter("[Status] = 1")
                .HasDatabaseName("UX_CapacityIncreaseRequests_Teacher_Pending");

            // Admin FIFO queue listing (Status = Pending, RequestedAt ASC).
            entity.HasIndex(r => new { r.Status, r.RequestedAt })
                .HasDatabaseName("IX_CapacityIncreaseRequests_Status_RequestedAt");
        });
        #endregion

        #region UserNotification (REQ-SUB-005 push records, Critique M-6)
        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.ToTable("UserNotifications");

            entity.Property(n => n.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(n => n.Body)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(n => n.DeepLinkPayload)
                .HasMaxLength(500);

            // ── Relationships ──
            entity.HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // ── Indexes (§9.1, M-6) ──
            // Single covering index for both the bell-icon unread-count query and the
            // paginated history list. Key order is (UserId, IsRead, SentAt DESC) — exactly
            // the predicate shape: WHERE UserId=@u AND IsRead=0 ORDER BY SentAt DESC.
            entity.HasIndex(n => new { n.UserId, n.IsRead, n.SentAt })
                .IsDescending(false, false, true)
                .HasDatabaseName("IX_UserNotifications_UserId_IsRead_SentAt");
        });
        #endregion

        #region UserDeviceToken (REQ-SUB-005 push delivery, D-06)
        modelBuilder.Entity<UserDeviceToken>(entity =>
        {
            entity.ToTable("UserDeviceTokens");

            entity.Property(d => d.FcmToken)
                .HasMaxLength(500)
                .IsRequired();

            // ── Relationships ──
            entity.HasOne(d => d.User)
                .WithMany(u => u.DeviceTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // ── Indexes (§9.1) ──

            // Upsert-on-(UserId, FcmToken): the register-fcm-token endpoint MERGEs by this key.
            entity.HasIndex(d => new { d.UserId, d.FcmToken })
                .IsUnique()
                .HasDatabaseName("IX_UserDeviceTokens_UserId_FcmToken");

            // Per-teacher reminder scan: filters IsActive=true tokens for a user.
            entity.HasIndex(d => new { d.UserId, d.IsActive })
                .HasDatabaseName("IX_UserDeviceTokens_UserId_IsActive");
        });
        #endregion
        #endregion

        // ════════════════════════════════════════════════
        // UPDATED: Assistant now references Teacher
        // ════════════════════════════════════════════════

        #region Assistant (updated FK)
        modelBuilder.Entity<Assistant>(entity =>
        {
            entity.ToTable("Assistants");

            entity.HasOne(a => a.Teacher)
                .WithMany()
                .HasForeignKey(a => a.TeacherAccountId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        #endregion

        // ════════════════════════════════════════════════
        // STUDENT USER MODULE CONFIGURATION (AAM-FR-05)
        // ════════════════════════════════════════════════

        #region TeacherStudent (teacher-scoped student record, Module 1)
        modelBuilder.Entity<TeacherStudent>(entity =>
        {
            entity.ToTable("TeacherStudents");

            // StudentName: mandatory, supports Arabic and English (REQ-STU-005 / REQ-STU-NFR-002)
            entity.Property(ts => ts.StudentName)
                .HasMaxLength(200)
                .IsRequired();

            // StudentCode: mandatory, unique per teacher (REQ-STU-010)
            entity.Property(ts => ts.StudentCode)
                .HasMaxLength(10)
                .IsRequired();
            entity.HasIndex(x => new { x.TeacherId, x.StudentPhoneNumber })
                .IsUnique()
                .HasFilter("[StudentPhoneNumber] IS NOT NULL AND [IsDeleted] = 0");
            // ParentPhoneNumber is deliberately NOT unique: one parent legitimately has
            // several children on the same teacher's roster, all sharing the parent's phone.
            // Non-unique index kept for messaging/lookup performance only.
            entity.HasIndex(x => new { x.TeacherId, x.ParentPhoneNumber });

            // Composite unique: StudentCode is unique within each teacher's account,
            // but ONLY among ACTIVE rows. Filtered on [IsDeleted] = 0 so a soft-deleted
            // student stops reserving its code — otherwise re-adding a student with a
            // manual code that was previously deleted throws a unique violation at INSERT
            // (surfaced as a 409), even though the app-layer StudentCodeExistsAsync check
            // — which honours the global soft-delete filter — reports the code as free.
            // Mirrors the StudentPhoneNumber filtered unique index above.
            entity.HasIndex(ts => new { ts.TeacherId, ts.StudentCode })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_TeacherStudents_TeacherId_StudentCode");

            // HashedToken: mandatory, auto-generated (AAM-NFR-03)
            entity.Property(ts => ts.HashedToken)
                .HasMaxLength(128)
                .IsRequired();

            // Phone numbers: optional, variable length
            entity.Property(ts => ts.StudentPhoneNumber)
                .HasMaxLength(20);

            entity.Property(ts => ts.ParentPhoneNumber)
                .HasMaxLength(20);

            // Barcode: optional, auto-generated (REQ-STU-047)
            entity.Property(ts => ts.Barcode)
                .HasMaxLength(50);

            // SessionId: nullable FK to Sessions table.
            // REQ-STU-004: "Assigned Session" is optional.
            // BR-SES-002: A student may only be assigned to one session at a time.
            // REQ-SES-042: SetNull when the assigned session is deleted.
            entity.Property(ts => ts.SessionId);

            // Session FK: SetNull when session is deleted — students become unassigned
            entity.HasOne(ts => ts.Session)
                .WithMany(s => s.TeacherStudents)
                .HasForeignKey(ts => ts.SessionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Teacher FK: NoAction delete when teacher account is removed
            entity.HasOne(ts => ts.Teacher)
                .WithMany()
                .HasForeignKey(ts => ts.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // Soft-delete filter: queries exclude deleted records by default
            entity.HasQueryFilter(ts => !ts.IsDeleted);

            // ── INDEXES ──

            // Performance index: active students per teacher (most common query path)
            entity.HasIndex(ts => new { ts.TeacherId, ts.IsDeleted })
                .HasDatabaseName("IX_TeacherStudents_TeacherId_IsDeleted");

            // Performance index: linking flow lookup — TeacherId + StudentCode + HashedToken
            entity.HasIndex(ts => new { ts.TeacherId, ts.StudentCode, ts.HashedToken })
                .HasDatabaseName("IX_TeacherStudents_LinkingLookup");

            // NEW: Performance index for filtering by assigned session (REQ-STU-036)
            // Also supports session student count queries (REQ-STU-UX-004)
            entity.HasIndex(ts => new { ts.TeacherId, ts.SessionId })
                .HasDatabaseName("IX_TeacherStudents_TeacherId_SessionId");

            // NEW: Performance index for recycle bin purge queries (REQ-STU-027/028)
            // Enables efficient lookup of expired soft-deleted records by deletion date
            entity.HasIndex(ts => new { ts.IsDeleted, ts.DeletedAt })
                .HasFilter("[IsDeleted] = 1")
                .HasDatabaseName("IX_TeacherStudents_RecycleBin_DeletedAt");

            // NEW: Performance index for student name search (REQ-STU-032)
            // Supports partial match queries on StudentName within a teacher scope
            entity.HasIndex(ts => new { ts.TeacherId, ts.StudentName })
                .HasDatabaseName("IX_TeacherStudents_TeacherId_StudentName");
        });
        #endregion

        #region StudentUser (student user account, AAM-FR-05)
        modelBuilder.Entity<StudentUser>(entity =>
        {
            entity.ToTable("StudentUsers");

            // UserId: one-to-one with User (same pattern as Teachers.UserId)
            entity.HasIndex(su => su.UserId)
                .IsUnique()
                .HasDatabaseName("IX_StudentUsers_UserId");

            entity.HasOne(su => su.User)
                .WithOne()
                .HasForeignKey<StudentUser>(su => su.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // StudentAccountCode: unique, immutable (AAM-FR-05.3)
            entity.Property(su => su.StudentAccountCode)
                .HasMaxLength(10)
                .IsRequired();

            entity.HasIndex(su => su.StudentAccountCode)
                .IsUnique()
                .HasDatabaseName("IX_StudentUsers_StudentAccountCode");

            // LanguagePreference: short code
            entity.Property(su => su.LanguagePreference)
                .HasMaxLength(5);

            // Soft-delete filter: queries exclude deleted records by default
            entity.HasQueryFilter(su => su.DeletedAt == null);

            // Performance index for active, non-deleted student users
            entity.HasIndex(su => new { su.AccountStatus, su.DeletedAt })
                .HasDatabaseName("IX_StudentUsers_AccountStatus_DeletedAt");
        });
        #endregion

        #region StudentTeacherLink (junction: StudentUser ↔ Teacher, request/approval flow)
        modelBuilder.Entity<StudentTeacherLink>(entity =>
        {
            entity.ToTable("StudentTeacherLinks");

            // Request snapshot fields (student-typed, shown to the teacher)
            entity.Property(stl => stl.RequestedStudentName).HasMaxLength(200);
            entity.Property(stl => stl.RequestedStudentCode).HasMaxLength(20);

            // Filtered unique: at most ONE live row (1=Active, 3=Pending) per
            // (student, teacher) pair. Terminal rows (Rejected/Unlinked/
            // RemovedByTeacher/CancelledByStudent) accumulate freely for audit,
            // so a student can re-request after a rejection or removal.
            // Keep the literal values in sync with the LinkStatus enum.
            entity.HasIndex(stl => new { stl.StudentUserId, stl.TeacherId })
                .IsUnique()
                .HasFilter("[LinkStatus] IN (1, 3)")
                .HasDatabaseName("IX_StudentTeacherLinks_StudentUserId_TeacherId");

            // StudentUser FK: NoAction delete when student user account is removed
            entity.HasOne(stl => stl.StudentUser)
                .WithMany(su => su.StudentTeacherLinks)
                .HasForeignKey(stl => stl.StudentUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Teacher FK: restrict — don't NoAction teacher deletion to student links
            entity.HasOne(stl => stl.Teacher)
                .WithMany()
                .HasForeignKey(stl => stl.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            // TeacherStudent FK: set null if teacher deletes the student record
            // The link survives but TeacherStudentId becomes null (degraded state)
            entity.HasOne(stl => stl.TeacherStudent)
                .WithMany(ts => ts.StudentTeacherLinks)
                .HasForeignKey(stl => stl.TeacherStudentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Performance index: teacher-side screens (pending requests inbox and
            // linked-students list both filter by TeacherId + LinkStatus)
            entity.HasIndex(stl => new { stl.TeacherId, stl.LinkStatus })
                .HasDatabaseName("IX_StudentTeacherLinks_TeacherId_LinkStatus");

            // Performance index: fast join to teacher's student record for data access
            entity.HasIndex(stl => stl.TeacherStudentId)
                .HasDatabaseName("IX_StudentTeacherLinks_TeacherStudentId");

            // Integrity: ONE student account per roster record — a TeacherStudent
            // can be claimed by at most one Active link. Enforces at DB level what
            // the accept flow checks in the service. The migration demotes any
            // pre-existing duplicate Active claims (keeps the newest) before
            // creating this index. Keep the literal in sync with LinkStatus.Active.
            entity.HasIndex(stl => stl.TeacherStudentId, "UX_StudentTeacherLinks_TeacherStudentId_Active")
                .IsUnique()
                .HasFilter("[LinkStatus] = 1 AND [TeacherStudentId] IS NOT NULL");

            // Performance index: active links filter (most common query path for dashboard)
            entity.HasIndex(stl => new { stl.StudentUserId, stl.LinkStatus })
                .HasDatabaseName("IX_StudentTeacherLinks_StudentUserId_LinkStatus");
        });
        #endregion

        // ════════════════════════════════════════════════
        // PARENT USER MODULE CONFIGURATION (AAM-FR-06)
        // ════════════════════════════════════════════════

        #region ParentUser (parent user account, AAM-FR-06)
        modelBuilder.Entity<ParentUser>(entity =>
        {
            entity.ToTable("ParentUsers");

            // UserId: one-to-one with User (same pattern as Teachers, StudentUsers)
            // The User table holds: FullName, Username, Email, PasswordHashed,
            // PhoneNumber, SecurityStamp, UserType = Parent. No auth fields here.
            entity.HasIndex(pu => pu.UserId)
                .IsUnique()
                .HasDatabaseName("IX_ParentUsers_UserId");

            entity.HasOne(pu => pu.User)
                .WithOne()
                .HasForeignKey<ParentUser>(pu => pu.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // LanguagePreference: short code ("en" or "ar")
            entity.Property(pu => pu.LanguagePreference)
                .HasMaxLength(5);

            // Soft-delete filter: queries exclude deleted records by default
            entity.HasQueryFilter(pu => pu.DeletedAt == null);

            // Performance index for active, non-deleted parent users
            entity.HasIndex(pu => new { pu.AccountStatus, pu.DeletedAt })
                .HasDatabaseName("IX_ParentUsers_AccountStatus_DeletedAt");
        });
        #endregion

        #region ParentChild (1:N from ParentUser, AAM-FR-06.3/06.4)
        modelBuilder.Entity<ParentChild>(entity =>
        {
            entity.ToTable("ParentChildren");

            // ParentUser FK: NoAction delete when parent account is removed
            entity.HasOne(pc => pc.ParentUser)
                .WithMany(pu => pu.Children)
                .HasForeignKey(pc => pc.ParentUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // StudentUser FK: optional (null for Method B manual profiles)
            // Restrict: don't NoAction student user deletion to parent child records
            entity.HasOne(pc => pc.StudentUser)
                .WithMany()
                .HasForeignKey(pc => pc.StudentUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ChildName: mandatory for both Method A and Method B
            entity.Property(pc => pc.ChildName)
                .HasMaxLength(200)
                .IsRequired();

            // Prevent duplicate Method A links: same parent cannot link the same student twice
            // Filtered unique index — only applies when StudentUserId IS NOT NULL (Method A)
            entity.HasIndex(pc => new { pc.ParentUserId, pc.StudentUserId })
                .IsUnique()
                .HasFilter("[StudentUserId] IS NOT NULL")
                .HasDatabaseName("IX_ParentChildren_ParentUserId_StudentUserId");

            // Performance index: active children per parent (dashboard query path)
            entity.HasIndex(pc => new { pc.ParentUserId, pc.IsActive })
                .HasDatabaseName("IX_ParentChildren_ParentUserId_IsActive");

            // The method filters on ParentUserId + StudentUserId + IsActive.
            // The existing filtered unique index (IX_ParentChildren_ParentUserId_StudentUserId)
            // only covers non-null StudentUserId and doesn't include IsActive,
            // forcing a scan for the AnyAsync check.
            // This composite index covers the exact query predicate for O(1) lookup.
            entity.HasIndex(pc => new { pc.ParentUserId, pc.StudentUserId, pc.IsActive })
                .HasFilter("[StudentUserId] IS NOT NULL")
                .HasDatabaseName("IX_ParentChildren_ParentUserId_StudentUserId_IsActive");
        });
        #endregion

        #region ParentChildTeacherLink (Method B teacher linking, AAM-FR-06.3/06.5)
        modelBuilder.Entity<ParentChildTeacherLink>(entity =>
        {
            entity.ToTable("ParentChildTeacherLinks");

            // Composite unique: a Method B child can only link to the same teacher once
            entity.HasIndex(pctl => new { pctl.ParentChildId, pctl.TeacherId })
                .IsUnique()
                .HasDatabaseName("IX_ParentChildTeacherLinks_ChildId_TeacherId");

            // ParentChild FK: NoAction delete when child record is removed
            entity.HasOne(pctl => pctl.ParentChild)
                .WithMany(pc => pc.TeacherLinks)
                .HasForeignKey(pctl => pctl.ParentChildId)
                .OnDelete(DeleteBehavior.NoAction);

            // Teacher FK: restrict — don't NoAction teacher deletion to parent links
            entity.HasOne(pctl => pctl.Teacher)
                .WithMany()
                .HasForeignKey(pctl => pctl.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            // TeacherStudent FK: set null if teacher deletes the student record
            // The link survives but TeacherStudentId becomes null (degraded state)
            entity.HasOne(pctl => pctl.TeacherStudent)
                .WithMany()
                .HasForeignKey(pctl => pctl.TeacherStudentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Performance index: reverse lookup — which parent children are linked to a teacher
            entity.HasIndex(pctl => pctl.TeacherId)
                .HasDatabaseName("IX_ParentChildTeacherLinks_TeacherId");

            // Performance index: active links filter for child dashboard
            entity.HasIndex(pctl => new { pctl.ParentChildId, pctl.LinkStatus })
                .HasDatabaseName("IX_ParentChildTeacherLinks_ChildId_LinkStatus");
        });
        #endregion

        // ════════════════════════════════════════════════
        // SESSION MODULE CONFIGURATION (Module 2)
        // ════════════════════════════════════════════════

        #region SessionGroup (REQ-SES-024/025)
        modelBuilder.Entity<SessionGroup>(entity =>
        {
            entity.ToTable("SessionGroups");

            entity.Property(g => g.GroupName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(g => g.Description)
                .HasMaxLength(1000);

            // Unique group name per teacher
            entity.HasIndex(g => new { g.TeacherId, g.GroupName })
                .IsUnique()
                .HasDatabaseName("IX_SessionGroups_TeacherId_GroupName");

            entity.HasIndex(g => g.TeacherId)
                .HasDatabaseName("IX_SessionGroups_TeacherId");

            entity.HasOne(g => g.Teacher)
                .WithMany()
                .HasForeignKey(g => g.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        #endregion

        #region Session (REQ-SES-001 through REQ-SES-015)
        modelBuilder.Entity<Session>(entity =>
        {
            entity.ToTable("Sessions");

            // SessionName: mandatory, supports Arabic and English (REQ-SES-003)
            entity.Property(s => s.SessionName)
                .HasMaxLength(200)
                .IsRequired();

            // BR-SES-001: Unique session name per teacher
            entity.HasIndex(s => new { s.TeacherId, s.SessionName })
                .IsUnique()
                .HasDatabaseName("IX_Sessions_TeacherId_SessionName");

            // SelectedDays: compact comma-separated string for weekly/biweekly day selection
            entity.Property(s => s.SelectedDays)
                .HasMaxLength(20)
                .IsUnicode(false);

            // SessionAmount: decimal(10,2) for EGP currency
            entity.Property(s => s.SessionAmount)
                .HasColumnType("decimal(10,2)");

            // StartDate/EndDate: date-only columns
            entity.Property(s => s.StartDate).HasColumnType("date");
            entity.Property(s => s.EndDate).HasColumnType("date");

            // StartTime: stored as time(7) in SQL Server
            entity.Property(s => s.StartTime).IsRequired();

            // DurationMinutes: smallint
            entity.Property(s => s.DurationMinutes).IsRequired();

            // Teacher FK: NoAction delete when teacher account is removed
            entity.HasOne(s => s.Teacher)
                .WithMany()
                .HasForeignKey(s => s.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // SessionGroup FK: SetNull when group is deleted (REQ-SES-031)
            entity.HasOne(s => s.SessionGroup)
                .WithMany(g => g.Sessions)
                .HasForeignKey(s => s.SessionGroupId)
                .OnDelete(DeleteBehavior.SetNull);

            // ── INDEXES ──

            // Primary tenant-scoped index for all session list queries
            entity.HasIndex(s => s.TeacherId)
                .HasDatabaseName("IX_Sessions_TeacherId");

            // Active/expired filtering (REQ-SES-015/045)
            entity.HasIndex(s => new { s.TeacherId, s.EndDate })
                .HasDatabaseName("IX_Sessions_TeacherId_EndDate");

            // Group drill-down queries (REQ-SES-027)
            entity.HasIndex(s => s.SessionGroupId)
                .HasDatabaseName("IX_Sessions_SessionGroupId");

            // Membership linking validation (BR-SES-003)
            entity.HasIndex(s => new { s.TeacherId, s.OccurrenceType, s.SelectedDays })
                .HasDatabaseName("IX_Sessions_TeacherId_OccurrenceType_SelectedDays");
        });
        #endregion

        #region SessionLink (REQ-SES-032 through REQ-SES-038)
        modelBuilder.Entity<SessionLink>(entity =>
        {
            entity.ToTable("SessionLinks");

            // Canonical ordering: SessionId < LinkedSessionId
            // One row per pair, symmetric lookup
            entity.HasIndex(sl => new { sl.SessionId, sl.LinkedSessionId })
                .IsUnique()
                .HasDatabaseName("IX_SessionLinks_SessionId_LinkedSessionId");

            // Reverse lookup index
            entity.HasIndex(sl => sl.LinkedSessionId)
                .HasDatabaseName("IX_SessionLinks_LinkedSessionId");

            // Session FK (lower Id side): NoAction delete
            entity.HasOne(sl => sl.Session)
                .WithMany(s => s.SessionLinksAsSource)
                .HasForeignKey(sl => sl.SessionId)
                .OnDelete(DeleteBehavior.NoAction);

            // LinkedSession FK (higher Id side): restrict to avoid multiple NoAction paths
            // Application code handles cleanup when deleting the higher-Id session
            entity.HasOne(sl => sl.LinkedSession)
                .WithMany(s => s.SessionLinksAsTarget)
                .HasForeignKey(sl => sl.LinkedSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        #endregion

        // ════════════════════════════════════════════════
        // ATTENDANCE MODULE CONFIGURATION (Module 3)
        // ════════════════════════════════════════════════

        #region SessionOccurrence (REQ-ATT-001 through 005)
        modelBuilder.Entity<SessionOccurrence>(entity =>
        {
            entity.ToTable("SessionOccurrences");

            entity.Property(o => o.OccurrenceDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(o => o.Status)
                .IsRequired()
                .HasDefaultValue(OccurrenceStatus.Pending);

            // Cross-session equivalence slot key ("weekly-slot position").
            entity.Property(o => o.WeekStartDate)
                .HasColumnType("date")
                .IsRequired()
                .HasDefaultValue(new DateTime(2000, 1, 1));

            entity.Property(o => o.DayPositionIndex)
                .IsRequired()
                .HasDefaultValue(1);

            // Unique: one occurrence per session per date
            entity.HasIndex(o => new { o.SessionId, o.OccurrenceDate })
                .IsUnique()
                .HasDatabaseName("IX_SessionOccurrences_SessionId_OccurrenceDate");

            // Equivalence lookup: resolve a session's occurrence for a slot, and gather all linked
            // sessions' occurrences sharing a slot. Unique — one occurrence per session per slot.
            entity.HasIndex(o => new { o.SessionId, o.WeekStartDate, o.DayPositionIndex })
                .IsUnique()
                .HasDatabaseName("IX_SessionOccurrences_SessionId_WeekStartDate_DayPositionIndex");

            // Workhorse index: "which sessions occur today for this teacher?"
            entity.HasIndex(o => new { o.TeacherId, o.OccurrenceDate })
                .HasDatabaseName("IX_SessionOccurrences_TeacherId_OccurrenceDate");

            // Dashboard filtering: completed vs. pending
            entity.HasIndex(o => new { o.TeacherId, o.Status })
                .HasDatabaseName("IX_SessionOccurrences_TeacherId_Status");

            // Teacher FK: NoAction — teacher deletion removes all occurrences
            entity.HasOne(o => o.Teacher)
                .WithMany()
                .HasForeignKey(o => o.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // Session FK: NoAction — session hard-delete removes occurrences
            entity.HasOne(o => o.Session)
                .WithMany()
                .HasForeignKey(o => o.SessionId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        #endregion

        #region StudentSessionAssignment (REQ-ATT-019/020/043-048)
        modelBuilder.Entity<StudentSessionAssignment>(entity =>
        {
            entity.ToTable("StudentSessionAssignments");

            entity.Property(a => a.SessionName)
                .HasMaxLength(200)
                .IsRequired();

            // Audit Fix: Denormalized student fields for post-purge display
            entity.Property(a => a.StudentName)
                .HasMaxLength(200);

            entity.Property(a => a.StudentCode)
                .HasMaxLength(20);

            entity.Property(a => a.AssignedAt)
                .IsRequired();

            entity.Property(a => a.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Active assignment lookup: "what session is this student currently in?"
            entity.HasIndex(a => new { a.TeacherStudentId, a.IsActive })
                .HasDatabaseName("IX_SSA_TeacherStudentId_IsActive");

            // Session student list: "who is assigned to this session?"
            entity.HasIndex(a => new { a.SessionId, a.IsActive })
                .HasDatabaseName("IX_SSA_SessionId_IsActive");

            // Timeline queries: all assignments for a student
            entity.HasIndex(a => new { a.TeacherId, a.TeacherStudentId })
                .HasDatabaseName("IX_SSA_TeacherId_TeacherStudentId");

            // Teacher FK: NoAction
            entity.HasOne(a => a.Teacher)
                .WithMany()
                .HasForeignKey(a => a.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // Audit Fix: TeacherStudent FK changed from NoAction to SetNull.
            // TeacherStudentId is now nullable (long?).
            // When the purge job hard-deletes the TeacherStudent row, SQL Server sets this to NULL.
            // Denormalized StudentName/StudentCode preserve display data after purge.
            entity.HasOne(a => a.TeacherStudent)
                .WithMany()
                .HasForeignKey(a => a.TeacherStudentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Session FK: NO ACTION — app nullifies before session delete
            entity.HasOne(a => a.Session)
                .WithMany()
                .HasForeignKey(a => a.SessionId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        #endregion

        #region AttendanceRecord (REQ-ATT-006/007/017/024/025, BR-ATT-002/005/006)
        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.ToTable("AttendanceRecords");

            entity.Property(r => r.SessionName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(r => r.OccurrenceDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(r => r.Status)
                .IsRequired();

            entity.Property(r => r.AttendanceMethod)
                .IsRequired();

            entity.Property(r => r.RecordedAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            entity.Property(r => r.LastEditedAt)
                .HasColumnType("datetime2(0)");

            entity.Property(r => r.CrossSessionName)
                .HasMaxLength(200);

            entity.Property(r => r.CrossSessionOccurrenceDate)
                .HasColumnType("date");

            // Step 7.2: Denormalized student fields for post-purge display
            entity.Property(r => r.StudentName)
                .HasMaxLength(200);

            entity.Property(r => r.StudentCode)
                .HasMaxLength(20);

            // FIX H3: Denormalized SessionGroupId for Report Type 5 (BR-ATT-005).
            // Survives session hard-delete so SessionGroupAttendance reports include all records.
            // Index enables efficient filtering for Report Type 5 queries.
            entity.HasIndex(r => new { r.TeacherId, r.SessionGroupId })
                .HasFilter("[SessionGroupId] IS NOT NULL")
                .HasDatabaseName("IX_AR_TeacherId_SessionGroupId");

            // BR-ATT-002: One attendance per student per occurrence (duplicate prevention)
            entity.HasIndex(r => new { r.TeacherStudentId, r.SessionOccurrenceId })
                .IsUnique()
                .HasFilter("[SessionOccurrenceId] IS NOT NULL AND [TeacherStudentId] IS NOT NULL")
                .HasDatabaseName("IX_AR_TeacherStudentId_SessionOccurrenceId");

            // Backup unique guard for after session deletion
            entity.HasIndex(r => new { r.TeacherStudentId, r.OccurrenceDate, r.SessionId })
                .IsUnique()
                .HasFilter("[TeacherStudentId] IS NOT NULL AND [SessionId] IS NOT NULL")
                .HasDatabaseName("IX_AR_TeacherStudentId_OccurrenceDate_SessionId");

            // Step 5.1: Post-deletion duplicate guard using denormalized SessionName
            entity.HasIndex(r => new { r.TeacherStudentId, r.OccurrenceDate, r.SessionName })
                .IsUnique()
                .HasFilter("[SessionOccurrenceId] IS NULL AND [SessionId] IS NULL AND [TeacherStudentId] IS NOT NULL")
                .HasDatabaseName("IX_AR_PostDeletion_DuplicateGuard");

            // Take Attendance / Edit Attendance: records for this session on this date
            entity.HasIndex(r => new { r.TeacherId, r.SessionId, r.OccurrenceDate })
                .HasDatabaseName("IX_AR_TeacherId_SessionId_OccurrenceDate");

            // Student timeline: all records for a student by date
            entity.HasIndex(r => new { r.TeacherStudentId, r.OccurrenceDate })
                .HasDatabaseName("IX_AR_TeacherStudentId_OccurrenceDate");

            // Consecutive absence calc: student + session + date + status
            entity.HasIndex(r => new { r.TeacherStudentId, r.SessionId, r.OccurrenceDate, r.Status })
                .HasDatabaseName("IX_AR_ConsecutiveAbsenceCalc");

            // Occurrence join
            entity.HasIndex(r => r.SessionOccurrenceId)
                .HasDatabaseName("IX_AR_SessionOccurrenceId");

            // Cross-teacher reporting
            entity.HasIndex(r => new { r.TeacherId, r.OccurrenceDate, r.Status })
                .HasDatabaseName("IX_AR_TeacherId_OccurrenceDate_Status");

            // Audit Fix: Cross-session query index
            entity.HasIndex(r => new { r.TeacherId, r.TeacherStudentId, r.IsCrossSession })
                .HasFilter("[IsCrossSession] = 1")
                .HasDatabaseName("IX_AR_CrossSession");

            // Teacher FK: NoAction
            entity.HasOne(r => r.Teacher)
                .WithMany()
                .HasForeignKey(r => r.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // SessionOccurrence FK: SET NULL — preserves record after occurrence cleanup
            entity.HasOne(r => r.SessionOccurrence)
                .WithMany(o => o.AttendanceRecords)
                .HasForeignKey(r => r.SessionOccurrenceId)
                .OnDelete(DeleteBehavior.SetNull);

            // Step 1.1: TeacherStudent FK: SET NULL — preserves record after student permanent purge
            // Changed from NoAction to SetNull. TeacherStudentId is now nullable (long?).
            // Denormalized StudentName/StudentCode preserve display data after purge.
            entity.HasOne(r => r.TeacherStudent)
                .WithMany()
                .HasForeignKey(r => r.TeacherStudentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Step 1.1: StudentSessionAssignment FK: SET NULL
            // Changed from NoAction to SetNull. StudentSessionAssignmentId is now nullable (long?).
            entity.HasOne(r => r.StudentSessionAssignment)
                .WithMany(a => a.AttendanceRecords)
                .HasForeignKey(r => r.StudentSessionAssignmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        #endregion

        #region AttendanceEditLog (REQ-ATT-025, BR-ATT-006)
        modelBuilder.Entity<AttendanceEditLog>(entity =>
        {
            entity.ToTable("AttendanceEditLogs");

            entity.Property(l => l.EditedAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            entity.Property(l => l.EditReason)
                .HasMaxLength(500);

            // Audit trail: all edits for a record
            entity.HasIndex(l => l.AttendanceRecordId)
                .HasDatabaseName("IX_AEL_AttendanceRecordId");

            // Audit Fix: Changed from NoAction to SetNull.
            // AttendanceRecordId is now nullable (long?).
            // When parent AttendanceRecord is deleted (REQ-ATT-024),
            // edit logs survive with null FK — preserving audit trail (BR-ATT-006).
            entity.HasOne(l => l.AttendanceRecord)
                .WithMany(r => r.EditLogs)
                .HasForeignKey(l => l.AttendanceRecordId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        #endregion

        #region StudentAbsenceCounter (REQ-ATT-021/029/030/031/047)
        modelBuilder.Entity<StudentAbsenceCounter>(entity =>
        {
            entity.ToTable("StudentAbsenceCounters");

            entity.Property(c => c.LastAbsenceDate)
                .HasColumnType("date");

            entity.Property(c => c.LastAbsenceSessionName)
                .HasMaxLength(200);

            entity.Property(c => c.LastAttendanceDate)
                .HasColumnType("date");

            // Audit Fix: Optimistic concurrency token for counter race condition prevention
            entity.Property(c => c.RowVersion)
                .IsRowVersion();

            // Unique: one counter per student per teacher
            entity.HasIndex(c => new { c.TeacherId, c.TeacherStudentId })
                .IsUnique()
                .HasDatabaseName("IX_SAC_TeacherId_TeacherStudentId");

            // Absence overview: sorted by consecutive absences
            entity.HasIndex(c => new { c.TeacherId, c.ConsecutiveAbsences })
                .HasDatabaseName("IX_SAC_TeacherId_ConsecutiveAbsences");

            // Fast lookup when marking attendance
            entity.HasIndex(c => c.TeacherStudentId)
                .HasDatabaseName("IX_SAC_TeacherStudentId");

            // Teacher FK: NoAction
            entity.HasOne(c => c.Teacher)
                .WithMany()
                .HasForeignKey(c => c.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // TeacherStudent FK: NO ACTION — cleaned up via app logic on purge
            entity.HasOne(c => c.TeacherStudent)
                .WithMany()
                .HasForeignKey(c => c.TeacherStudentId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        #endregion

        // ════════════════════════════════════════════════
        // PAYMENT MODULE CONFIGURATION (Module 4)
        // ════════════════════════════════════════════════

        #region PaymentTransaction (REQ-PAY-001/002/012)
        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("PaymentTransactions");

            // Financial precision: decimal(10,2) for EGP currency
            entity.Property(t => t.AmountDue).HasColumnType("decimal(10,2)");
            entity.Property(t => t.AmountPaid).HasColumnType("decimal(10,2)");

            // Timestamps: precision to the second
            entity.Property(t => t.CollectedAt).HasColumnType("datetime2(0)").IsRequired();
            entity.Property(t => t.LocalCollectedAt).HasColumnType("datetime2(0)").IsRequired();
            entity.Property(t => t.DeletedAt).HasColumnType("datetime2(0)");

            // String lengths
            entity.Property(t => t.StudentName).HasMaxLength(PaymentConstants.NameMaxLength);
            entity.Property(t => t.StudentCode).HasMaxLength(PaymentConstants.StudentCodeMaxLength);
            entity.Property(t => t.SessionName).HasMaxLength(PaymentConstants.NameMaxLength).IsRequired();
            entity.Property(t => t.OnlineTransactionRef).HasMaxLength(PaymentConstants.OnlineTransactionRefMaxLength);
            entity.Property(t => t.OfflineDeviceId).HasMaxLength(PaymentConstants.OfflineDeviceIdMaxLength);
            entity.Property(t => t.ProRatedTierLabel).HasMaxLength(PaymentConstants.ProRatedTierLabelMaxLength);

            // Optimistic concurrency
            entity.Property(t => t.RowVersion).IsRowVersion();

            // ── INDEXES ──


            // Global query filter: soft-deleted transactions excluded by default
            entity.HasQueryFilter(t => !t.IsDeleted);

            // Primary tenant-scoped query: all transactions for a teacher
            entity.HasIndex(t => new { t.TeacherId, t.IsDeleted })
                .HasDatabaseName("IX_PT_TeacherId_IsDeleted");

            // Student payment history: teacher + student + date
            entity.HasIndex(t => new { t.TeacherId, t.TeacherStudentId, t.CollectedAt })
                .HasDatabaseName("IX_PT_TeacherId_StudentId_CollectedAt");

            // Same-day duplicate detection: teacher + student + local date
            entity.HasIndex(t => new { t.TeacherId, t.TeacherStudentId, t.LocalCollectedAt })
                .HasDatabaseName("IX_PT_TeacherId_StudentId_LocalDate");

            // Session payment report: teacher + session + date
            entity.HasIndex(t => new { t.TeacherId, t.SessionId, t.CollectedAt })
                .HasDatabaseName("IX_PT_TeacherId_SessionId_CollectedAt");

            // Collector performance: teacher + collector + date
            entity.HasIndex(t => new { t.TeacherId, t.CollectedByUserId, t.CollectedAt })
                .HasDatabaseName("IX_PT_TeacherId_CollectorId_CollectedAt");

            // Period lookup
            entity.HasIndex(t => t.PaymentPeriodId)
                .HasDatabaseName("IX_PT_PaymentPeriodId");

            // Offline sync exactly-once: one transaction per client-generated
            // entry id per teacher. Filtered — online records carry NULL.
            entity.Property(t => t.ClientEntryId).HasMaxLength(64);
            entity.HasIndex(t => new { t.TeacherId, t.ClientEntryId })
                .IsUnique()
                .HasFilter("[ClientEntryId] IS NOT NULL")
                .HasDatabaseName("IX_PT_TeacherId_ClientEntryId");

            // ── FOREIGN KEYS ──

            // ── FOREIGN KEYS ──

            // Teacher FK: NoAction — all payment data deleted with teacher account
            entity.HasOne(t => t.Teacher)
                .WithMany()
                .HasForeignKey(t => t.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // TeacherStudent FK: SET NULL — record survives student permanent purge
            // Denormalized StudentName/StudentCode preserve display data
            entity.HasOne(t => t.TeacherStudent)
                .WithMany()
                .HasForeignKey(t => t.TeacherStudentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Session FK: NO ACTION — app nullifies before session hard-delete
            entity.HasOne(t => t.Session)
                .WithMany()
                .HasForeignKey(t => t.SessionId)
                .OnDelete(DeleteBehavior.NoAction);

            // SessionOccurrence FK: SET NULL
            entity.HasOne(t => t.SessionOccurrence)
                .WithMany()
                .HasForeignKey(t => t.SessionOccurrenceId)
                .OnDelete(DeleteBehavior.SetNull);

            // PaymentPeriod FK: SET NULL
            entity.HasOne(t => t.PaymentPeriod)
                .WithMany(p => p.PaymentTransactions)
                .HasForeignKey(t => t.PaymentPeriodId)
                .OnDelete(DeleteBehavior.SetNull);

            // StudentSessionAssignment FK: SET NULL
            entity.HasOne(t => t.StudentSessionAssignment)
                .WithMany()
                .HasForeignKey(t => t.StudentSessionAssignmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        #endregion

        #region PaymentTransactionAllocation (PAY-1: per-period settlement ledger)
        modelBuilder.Entity<PaymentTransactionAllocation>(entity =>
        {
            entity.ToTable("PaymentTransactionAllocations");

            entity.Property(a => a.AmountApplied).HasColumnType("decimal(10,2)");

            // ── INDEXES ──

            // Reverse a transaction: load all its allocations.
            entity.HasIndex(a => a.PaymentTransactionId)
                .HasDatabaseName("IX_PTA_PaymentTransactionId");

            // Period lookup (and the CASCADE parent side).
            entity.HasIndex(a => a.PaymentPeriodId)
                .HasDatabaseName("IX_PTA_PaymentPeriodId");

            // At most one live allocation per (transaction, period) — cascade top-ups increment the
            // existing row instead of inserting a duplicate. Filtered so a null period (should never
            // persist, but defensive) never trips the unique constraint.
            entity.HasIndex(a => new { a.PaymentTransactionId, a.PaymentPeriodId })
                .IsUnique()
                .HasFilter("[PaymentPeriodId] IS NOT NULL")
                .HasDatabaseName("UX_PTA_Transaction_Period");

            // ── FOREIGN KEYS ──

            // Teacher FK: NoAction — all payment data deleted with teacher account. Nav-less: this
            // internal ledger is only ever reached via its transaction/period, never queried by
            // teacher directly, so no Teacher navigation is carried.
            entity.HasOne<Teacher>()
                .WithMany()
                .HasForeignKey(a => a.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // Transaction FK: NoAction — transactions are only soft-deleted; reversal removes the
            // allocation rows explicitly, so no DB cascade is needed (and it keeps the table to a
            // single cascade FK, avoiding SQL Server's multiple-cascade-path restriction).
            entity.HasOne(a => a.PaymentTransaction)
                .WithMany(t => t.Allocations)
                .HasForeignKey(a => a.PaymentTransactionId)
                .OnDelete(DeleteBehavior.NoAction);

            // Period FK: CASCADE — periods are hard-deleted on student purge; their allocations
            // (slices of a now-gone obligation) go with them.
            entity.HasOne(a => a.PaymentPeriod)
                .WithMany()
                .HasForeignKey(a => a.PaymentPeriodId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        #endregion

        #region PaymentPeriod (BR-PAY-001: earliest unpaid period lookup)
        modelBuilder.Entity<PaymentPeriod>(entity =>
        {
            entity.ToTable("PaymentPeriods");

            // Financial precision
            entity.Property(p => p.AmountDue).HasColumnType("decimal(10,2)");
            entity.Property(p => p.AmountPaid).HasColumnType("decimal(10,2)");
            entity.Property(p => p.ProRatedFraction).HasColumnType("decimal(5,4)");

            // Date-only columns
            entity.Property(p => p.PeriodStart).HasColumnType("date");
            entity.Property(p => p.PeriodEnd).HasColumnType("date");

            // String lengths
            entity.Property(p => p.SessionName).HasMaxLength(PaymentConstants.NameMaxLength).IsRequired();
            entity.Property(p => p.StudentName).HasMaxLength(PaymentConstants.NameMaxLength);
            entity.Property(p => p.StudentCode).HasMaxLength(PaymentConstants.StudentCodeMaxLength);
            entity.Property(p => p.OriginSessionName).HasMaxLength(PaymentConstants.NameMaxLength);

            // ── INDEXES ──

            // HOT-PATH: "find earliest unpaid period" — O(1) via index scan
            // BR-PAY-001: This is the single most performance-critical query in the module
            entity.HasIndex(p => new { p.TeacherId, p.TeacherStudentId, p.PaymentStatus, p.PeriodSequence })
                .HasDatabaseName("IX_PP_EarliestUnpaid");

            // Session payment report and unpaid badge count
            entity.HasIndex(p => new { p.TeacherId, p.SessionId, p.PaymentStatus })
                .HasDatabaseName("IX_PP_TeacherId_SessionId_Status");

            // Student timeline (all periods for a student)
            entity.HasIndex(p => new { p.TeacherId, p.TeacherStudentId, p.PeriodSequence })
                .HasDatabaseName("IX_PP_TeacherId_StudentId_Sequence");

            // Dashboard aggregate queries: GROUP BY SessionId
            entity.HasIndex(p => new { p.TeacherId, p.SessionId, p.PeriodStart, p.PeriodEnd })
                .HasDatabaseName("IX_PP_TeacherId_SessionId_PeriodDates");

            // Screen: "students by status" — partial-paid classification (cross-student,
            // month-scoped) in GetStudentsByPaymentStatusPagedAsync.
            entity.HasIndex(p => new { p.TeacherId, p.PaymentStatus, p.PeriodStart })
                .HasDatabaseName("IX_PP_TeacherId_Status_PeriodStart");

            // ── FOREIGN KEYS ──

            // ── FOREIGN KEYS ──

            entity.HasOne(p => p.Teacher)
                .WithMany()
                .HasForeignKey(p => p.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // Session FK: NO ACTION — app nullifies before session hard-delete
            entity.HasOne(p => p.Session)
                .WithMany()
                .HasForeignKey(p => p.SessionId)
                .OnDelete(DeleteBehavior.NoAction);

            // TeacherStudent FK: SET NULL — period survives student purge
            entity.HasOne(p => p.TeacherStudent)
                .WithMany()
                .HasForeignKey(p => p.TeacherStudentId)
                .OnDelete(DeleteBehavior.SetNull);

            // StudentSessionAssignment FK: SET NULL
            entity.HasOne(p => p.StudentSessionAssignment)
                .WithMany()
                .HasForeignKey(p => p.StudentSessionAssignmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        #endregion

        #region StudentPaymentCounter (REQ-PAY-029/016/031)
        modelBuilder.Entity<StudentPaymentCounter>(entity =>
        {
            entity.ToTable("StudentPaymentCounters");

            // Financial precision
            entity.Property(c => c.TotalAmountPaid).HasColumnType("decimal(12,2)");
            entity.Property(c => c.TotalOutstanding).HasColumnType("decimal(12,2)");
            entity.Property(c => c.CustomPaymentAmount).HasColumnType("decimal(10,2)");
            entity.Property(c => c.LastPaymentSessionName).HasMaxLength(PaymentConstants.NameMaxLength);

            // Optimistic concurrency
            entity.Property(c => c.RowVersion).IsRowVersion();

            // Unique: one counter per student per teacher
            entity.HasIndex(c => new { c.TeacherId, c.TeacherStudentId })
                .IsUnique()
                .HasDatabaseName("IX_SPC_TeacherId_StudentId");

            // Unpaid overview: sorted by consecutive unpaid
            entity.HasIndex(c => new { c.TeacherId, c.ConsecutiveUnpaid })
                .HasDatabaseName("IX_SPC_TeacherId_ConsecutiveUnpaid");

            // Outstanding amount queries
            entity.HasIndex(c => new { c.TeacherId, c.TotalOutstanding })
                .HasDatabaseName("IX_SPC_TeacherId_TotalOutstanding");

            // Teacher FK: NoAction
            entity.HasOne(c => c.Teacher)
                .WithMany()
                .HasForeignKey(c => c.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // TeacherStudent FK: NO ACTION — cleaned up via app logic on purge
            entity.HasOne(c => c.TeacherStudent)
                .WithMany()
                .HasForeignKey(c => c.TeacherStudentId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        #endregion

        #region AssistantWallet (REQ-PAY-034/035/036)
        modelBuilder.Entity<AssistantWallet>(entity =>
        {
            entity.ToTable("AssistantWallets");

            entity.Property(w => w.CurrentBalance).HasColumnType("decimal(12,2)");
            entity.Property(w => w.TotalCollected).HasColumnType("decimal(14,2)");

            // Optimistic concurrency
            entity.Property(w => w.RowVersion).IsRowVersion();

            // Unique: one wallet per assistant per teacher
            entity.HasIndex(w => new { w.TeacherId, w.AssistantId })
                .IsUnique()
                .HasDatabaseName("IX_AW_TeacherId_AssistantId");

            // Fast lookup by user ID during collection
            entity.HasIndex(w => new { w.TeacherId, w.AssistantUserId })
                .HasDatabaseName("IX_AW_TeacherId_AssistantUserId");

            entity.HasOne(w => w.Teacher)
                .WithMany()
                .HasForeignKey(w => w.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // Assistant FK: NO ACTION — wallet preserved for historical wallet reset logs.
            // App logic handles wallet cleanup when assistant is deactivated/deleted.
            entity.HasOne(w => w.Assistant)
                .WithMany()
                .HasForeignKey(w => w.AssistantId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        #endregion

        #region WalletResetLog (REQ-PAY-037: permanent ledger)
        modelBuilder.Entity<WalletResetLog>(entity =>
        {
            entity.ToTable("WalletResetLogs");

            entity.Property(l => l.AmountReset).HasColumnType("decimal(12,2)");
            entity.Property(l => l.ResetAt).HasColumnType("datetime2(0)").IsRequired();
            entity.Property(l => l.AssistantName).HasMaxLength(PaymentConstants.NameMaxLength);

            entity.HasIndex(l => new { l.TeacherId, l.AssistantId })
                .HasDatabaseName("IX_WRL_TeacherId_AssistantId");

            entity.HasOne(l => l.Teacher)
                .WithMany()
                .HasForeignKey(l => l.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // Assistant FK: NO ACTION — log survives assistant deletion for ledger permanence
            entity.HasOne(l => l.Assistant)
                .WithMany()
                .HasForeignKey(l => l.AssistantId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(l => l.AssistantWallet)
                .WithMany()
                .HasForeignKey(l => l.AssistantWalletId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        #endregion

        #region PaymentEditLog (BR-PAY-002 audit trail)
        modelBuilder.Entity<PaymentEditLog>(entity =>
        {
            entity.ToTable("PaymentEditLogs");

            entity.Property(l => l.PreviousAmount).HasColumnType("decimal(10,2)");
            entity.Property(l => l.NewAmount).HasColumnType("decimal(10,2)");
            entity.Property(l => l.EditedAt).HasColumnType("datetime2(0)").IsRequired();
            entity.Property(l => l.EditReason).HasMaxLength(PaymentConstants.EditReasonMaxLength);

            // Audit trail: all edits for a transaction
            entity.HasIndex(l => l.PaymentTransactionId)
                .HasDatabaseName("IX_PEL_PaymentTransactionId");

            // PaymentTransaction FK: SET NULL — log survives parent deletion for audit
            entity.HasOne(l => l.PaymentTransaction)
                .WithMany(t => t.EditLogs)
                .HasForeignKey(l => l.PaymentTransactionId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        #endregion

        #region StudentDeparture (REQ-PAY-066/073)
        modelBuilder.Entity<StudentDeparture>(entity =>
        {
            entity.ToTable("StudentDepartures");

            entity.Property(d => d.FullPeriodAmount).HasColumnType("decimal(10,2)");
            entity.Property(d => d.ProRatedAmount).HasColumnType("decimal(10,2)");
            entity.Property(d => d.FinalAmount).HasColumnType("decimal(10,2)");
            entity.Property(d => d.OriginalCalculatedAmount).HasColumnType("decimal(10,2)");
            entity.Property(d => d.DepartedAt).HasColumnType("datetime2(0)").IsRequired();

            entity.Property(d => d.SessionName).HasMaxLength(PaymentConstants.NameMaxLength).IsRequired();
            entity.Property(d => d.StudentName).HasMaxLength(PaymentConstants.NameMaxLength);
            entity.Property(d => d.StudentCode).HasMaxLength(PaymentConstants.StudentCodeMaxLength);

            entity.HasIndex(d => new { d.TeacherId, d.TeacherStudentId })
                .HasDatabaseName("IX_SD_TeacherId_StudentId");

            entity.HasOne(d => d.Teacher)
                .WithMany()
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // TeacherStudent FK: SET NULL — departure record survives student purge
            entity.HasOne(d => d.TeacherStudent)
                .WithMany()
                .HasForeignKey(d => d.TeacherStudentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Session FK: NO ACTION — app nullifies before session hard-delete
            entity.HasOne(d => d.Session)
                .WithMany()
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        #endregion

        #region SessionTransferEvent (REQ-PAY-089: permanently retained)
        modelBuilder.Entity<SessionTransferEvent>(entity =>
        {
            entity.ToTable("SessionTransferEvents");

            entity.Property(t => t.OutstandingBalance).HasColumnType("decimal(10,2)");
            entity.Property(t => t.CreditBalance).HasColumnType("decimal(10,2)");
            entity.Property(t => t.TransferredAt).HasColumnType("datetime2(0)").IsRequired();

            entity.Property(t => t.SourceSessionName).HasMaxLength(PaymentConstants.NameMaxLength).IsRequired();
            entity.Property(t => t.DestinationSessionName).HasMaxLength(PaymentConstants.NameMaxLength).IsRequired();
            entity.Property(t => t.SourcePaymentType).HasMaxLength(20).IsRequired();
            entity.Property(t => t.DestinationPaymentType).HasMaxLength(20).IsRequired();
            entity.Property(t => t.StudentName).HasMaxLength(PaymentConstants.NameMaxLength);
            entity.Property(t => t.StudentCode).HasMaxLength(PaymentConstants.StudentCodeMaxLength);

            entity.HasIndex(t => new { t.TeacherId, t.TeacherStudentId })
                .HasDatabaseName("IX_STE_TeacherId_StudentId");

            entity.HasOne(t => t.Teacher)
                .WithMany()
                .HasForeignKey(t => t.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // TeacherStudent FK: SET NULL — transfer event survives student purge
            entity.HasOne(t => t.TeacherStudent)
                .WithMany()
                .HasForeignKey(t => t.TeacherStudentId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        #endregion

        // ════════════════════════════════════════════════
        // EVENT PAYMENT MODULE CONFIGURATION (Module 5)
        // ════════════════════════════════════════════════

        #region PaymentEvent (REQ-EVT-001/002)
        modelBuilder.Entity<PaymentEvent>(entity =>
        {
            entity.ToTable("PaymentEvents");

            entity.Property(e => e.EventName).HasMaxLength(PaymentConstants.EventNameMaxLength).IsRequired();
            entity.Property(e => e.EventAmount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.TotalExpectedRevenue).HasColumnType("decimal(14,2)");
            entity.Property(e => e.TotalCollectedRevenue).HasColumnType("decimal(14,2)");
            entity.Property(e => e.EventDate).HasColumnType("date");
            entity.Property(e => e.DeletedAt).HasColumnType("datetime2(0)");
            entity.Property(e => e.TargetScopeIds).HasMaxLength(PaymentConstants.TargetScopeIdsMaxLength);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            // Global query filter: soft-deleted events excluded by default
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasIndex(e => new { e.TeacherId, e.IsDeleted })
                .HasDatabaseName("IX_PE_TeacherId_IsDeleted");

            entity.HasOne(e => e.Teacher)
                .WithMany()
                .HasForeignKey(e => e.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        #endregion

        #region EventStudentObligation (BR-EVT-001/004)
        modelBuilder.Entity<EventStudentObligation>(entity =>
        {
            entity.ToTable("EventStudentObligations");

            entity.Property(o => o.AmountDue).HasColumnType("decimal(10,2)");
            entity.Property(o => o.AmountPaid).HasColumnType("decimal(10,2)");
            entity.Property(o => o.StudentName).HasMaxLength(PaymentConstants.NameMaxLength);
            entity.Property(o => o.StudentCode).HasMaxLength(PaymentConstants.StudentCodeMaxLength);

            // Unique: one obligation per student per event
            entity.HasIndex(o => new { o.PaymentEventId, o.TeacherStudentId })
                .IsUnique()
                .HasFilter("[TeacherStudentId] IS NOT NULL")
                .HasDatabaseName("IX_ESO_EventId_StudentId");

            // Event tracking: all obligations for an event by status
            entity.HasIndex(o => new { o.PaymentEventId, o.PaymentStatus })
                .HasDatabaseName("IX_ESO_EventId_Status");

            entity.HasOne(o => o.Teacher)
                .WithMany()
                .HasForeignKey(o => o.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // PaymentEvent FK: NoAction — obligation removed when event is deleted
            entity.HasOne(o => o.PaymentEvent)
                .WithMany(e => e.StudentObligations)
                .HasForeignKey(o => o.PaymentEventId)
                .OnDelete(DeleteBehavior.NoAction);

            // TeacherStudent FK: SET NULL — obligation survives student purge
            entity.HasOne(o => o.TeacherStudent)
                .WithMany()
                .HasForeignKey(o => o.TeacherStudentId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        #endregion

        #region EventPaymentTransaction (REQ-EVT-009/013/022)
        modelBuilder.Entity<EventPaymentTransaction>(entity =>
        {
            entity.ToTable("EventPaymentTransactions");

            entity.Property(t => t.AmountPaid).HasColumnType("decimal(10,2)");
            entity.Property(t => t.CollectedAt).HasColumnType("datetime2(0)").IsRequired();
            entity.Property(t => t.StudentName).HasMaxLength(PaymentConstants.NameMaxLength);
            entity.Property(t => t.StudentCode).HasMaxLength(PaymentConstants.StudentCodeMaxLength);
            entity.Property(t => t.EventName).HasMaxLength(PaymentConstants.EventNameMaxLength).IsRequired();
            entity.Property(t => t.OnlineTransactionRef).HasMaxLength(PaymentConstants.OnlineTransactionRefMaxLength);

            entity.HasIndex(t => new { t.TeacherId, t.PaymentEventId })
                .HasDatabaseName("IX_EPT_TeacherId_EventId");

            entity.HasOne(t => t.Teacher)
                .WithMany()
                .HasForeignKey(t => t.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // PaymentEvent FK: SET NULL — transaction survives event deletion (REQ-EVT-022)
            entity.HasOne(t => t.PaymentEvent)
                .WithMany(e => e.PaymentTransactions)
                .HasForeignKey(t => t.PaymentEventId)
                .OnDelete(DeleteBehavior.SetNull);

            // EventStudentObligation FK: SET NULL
            entity.HasOne(t => t.EventStudentObligation)
                .WithMany(o => o.EventPaymentTransactions)
                .HasForeignKey(t => t.EventStudentObligationId)
                .OnDelete(DeleteBehavior.SetNull);

            // TeacherStudent FK: SET NULL — transaction survives student purge
            entity.HasOne(t => t.TeacherStudent)
                .WithMany()
                .HasForeignKey(t => t.TeacherStudentId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        #endregion
        // ════════════════════════════════════════════════
        // Mesaging 
        // ════════════════════════════════════════════════
        #region messging Template 
        modelBuilder.Entity<MessageTemplate>()
       .HasIndex(t => t.Name).IsClustered(false);
        modelBuilder.Entity<MessageTemplate>()
      .HasIndex(t => t.TeacherId).IsClustered(false);
        modelBuilder.Entity<MessageTemplate>()
        .HasIndex(t => new { t.TeacherId, t.Name }).IsUnique()
        .HasDatabaseName("IX_MessageTemplate_TeacherId_Name");

        #endregion





// ════════════════════════════════════════════════════════════════════════════
// EXAM & HOMEWORK MODULE CONFIGURATION (Module 6)
// ════════════════════════════════════════════════════════════════════════════




        // ════════════════════════════════════════════════
        // EXAM & HOMEWORK MODULE CONFIGURATION (Module 6)
        // ════════════════════════════════════════════════

        #region AssignmentTemplate (REQ-EXH-001 through 013, 020/021, 034)
modelBuilder.Entity<AssignmentTemplate>(entity =>
{
    entity.ToTable("AssignmentTemplates");

    // ── COLUMN MAPPINGS ───────────────────────────────────────────────

    entity.Property(t => t.Name)
        .HasMaxLength(200)
        .IsRequired();

    // Optional: exam/assignment name may be a single Arabic-or-English value (stored in Name);
    // a separate Arabic name is no longer mandatory.
    entity.Property(t => t.NameAr)
        .HasMaxLength(200);

    entity.Property(t => t.Notes)
        .HasMaxLength(2000);

    entity.Property(t => t.AssignmentType)
        .IsRequired();

    entity.Property(t => t.RecurrencePattern)
        .IsRequired()
        .HasDefaultValue(RecurrencePattern.OneTime);

    entity.Property(t => t.RecurrenceEndDate)
        .HasColumnType("date");

    entity.Property(t => t.IsRecurring)
        .HasDefaultValue(false);

    entity.Property(t => t.IsRecurrenceStopped)
        .HasDefaultValue(false);

    // Grade fields — decimal(8,2) accommodates grades up to 999999.99
    entity.Property(t => t.MaxGrade)
        .HasColumnType("decimal(8,2)");

    entity.Property(t => t.PassingThreshold)
        .HasColumnType("decimal(8,2)");

    entity.Property(t => t.UpdatedAt)
        .HasColumnType("datetime2(0)")
        .IsRequired();

    entity.Property(t => t.RowVersion)
        .IsRowVersion()
        .IsConcurrencyToken();

    // ── RELATIONSHIPS ─────────────────────────────────────────────────

    // Teacher FK: NoAction — deleting a teacher deletes their templates.
    entity.HasOne(t => t.Teacher)
        .WithMany()
        .HasForeignKey(t => t.TeacherId)
        .OnDelete(DeleteBehavior.NoAction);

    // CreatedByUser: audit FK — keep the row even if the creator is removed.
    entity.HasOne(t => t.CreatedByUser)
        .WithMany()
        .HasForeignKey(t => t.CreatedByUserId)
        .OnDelete(DeleteBehavior.Restrict);

    // ── INDEXES ───────────────────────────────────────────────────────

    // (REQ-EXH-033) Assignment Overview list — tenant-scoped, filterable by type and recurrence.
    // Section 7.2 index #6.
    entity.HasIndex(t => new { t.TeacherId, t.AssignmentType, t.IsRecurring, t.CreateAt })
        .IsDescending(false, false, false, true)
        .IncludeProperties(t => new { t.Name, t.NameAr })
        .HasDatabaseName("IX_AssignmentTemplates_TeacherList");

    // Recurrence scheduler scan: which templates need new occurrences generated today?
    entity.HasIndex(t => new { t.IsRecurring, t.IsRecurrenceStopped, t.RecurrenceEndDate })
        .HasFilter("[IsRecurring] = 1 AND [IsRecurrenceStopped] = 0")
        .HasDatabaseName("IX_AssignmentTemplates_RecurrenceScheduler");
});
        #endregion

        #region AssignmentScope (REQ-EXH-002 through 003, 035)
        modelBuilder.Entity<AssignmentScope>(entity =>
        {
            entity.ToTable("AssignmentScopes");

            // ── COLUMN MAPPINGS ───────────────────────────────────────────────

            entity.Property(s => s.ScopeType)
                .IsRequired();

            // ── RELATIONSHIPS ─────────────────────────────────────────────────

            // Template FK: NoAction — scope rows die with their template.
            entity.HasOne(s => s.Template)
                .WithMany(t => t.Scopes)
                .HasForeignKey(s => s.TemplateId)
                .OnDelete(DeleteBehavior.NoAction);

            // Teacher FK: Restrict — Teacher → Template → Scope already NoActions; this
            // FK exists only for the denormalized TeacherId. NoAction avoids the
            // multiple-NoAction-paths SQL Server restriction.
            entity.HasOne(s => s.Teacher)
                .WithMany()
                .HasForeignKey(s => s.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // TeacherStudent FK: SetNull — if a student is purged, their scope row
            // becomes orphaned but the parent template/occurrence chain survives.
            // Service layer detects null targets and skips them at occurrence generation.
            entity.HasOne(s => s.TeacherStudent)
                .WithMany()
                .HasForeignKey(s => s.TeacherStudentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Session FK: SetNull — same reasoning. Session deletion is hard per BR-SES-004,
            // but a stale scope row is harmless because occurrences are already materialized.
            entity.HasOne(s => s.Session)
                .WithMany()
                .HasForeignKey(s => s.SessionId)
                .OnDelete(DeleteBehavior.SetNull);

            // SessionGroup FK: SetNull.
            entity.HasOne(s => s.SessionGroup)
                .WithMany()
                .HasForeignKey(s => s.SessionGroupId)
                .OnDelete(DeleteBehavior.SetNull);

            // ── CHECK CONSTRAINT ──────────────────────────────────────────────
            //
            // Enforces "exactly one of TeacherStudentId / SessionId / SessionGroupId is non-null"
            // and that the populated FK matches ScopeType. This is the database-level
            // safety net for the polymorphic-resolution pattern (design issue 3.4).
            //
            // ScopeType values: 0 = IndividualStudent, 1 = Session, 2 = SessionGroup.
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_AssignmentScopes_ExactlyOneTarget",
                @"(
            CASE WHEN [TeacherStudentId] IS NULL THEN 0 ELSE 1 END
          + CASE WHEN [SessionId]        IS NULL THEN 0 ELSE 1 END
          + CASE WHEN [SessionGroupId]   IS NULL THEN 0 ELSE 1 END
        ) = 1
        AND (
            ([ScopeType] = 0 AND [TeacherStudentId] IS NOT NULL)
         OR ([ScopeType] = 1 AND [SessionId]        IS NOT NULL)
         OR ([ScopeType] = 2 AND [SessionGroupId]   IS NOT NULL)
        )"));

            // ── INDEXES ───────────────────────────────────────────────────────

            // (Section 7.2 index #8) Scope-to-template lookup at occurrence generation.
            entity.HasIndex(s => new { s.TemplateId, s.ScopeType })
                .IncludeProperties(s => new { s.TeacherStudentId, s.SessionId, s.SessionGroupId })
                .HasDatabaseName("IX_AssignmentScopes_Template");

            // Reverse-lookup indexes for "is this student/session/group used in any template?"
            // Each is filtered to skip the null rows — keeps the indexes small.
            entity.HasIndex(s => s.TeacherStudentId)
                .HasFilter("[TeacherStudentId] IS NOT NULL")
                .HasDatabaseName("IX_AssignmentScopes_TeacherStudentId");

            entity.HasIndex(s => s.SessionId)
                .HasFilter("[SessionId] IS NOT NULL")
                .HasDatabaseName("IX_AssignmentScopes_SessionId");

            entity.HasIndex(s => s.SessionGroupId)
                .HasFilter("[SessionGroupId] IS NOT NULL")
                .HasDatabaseName("IX_AssignmentScopes_SessionGroupId");
        });
        #endregion

        #region AssignmentOccurrence (REQ-EXH-005, 007, 011, 046)
        modelBuilder.Entity<AssignmentOccurrence>(entity =>
        {
            entity.ToTable("AssignmentOccurrences");

            // ── COLUMN MAPPINGS ───────────────────────────────────────────────

            entity.Property(o => o.DueDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(o => o.OccurrenceNumber)
                .IsRequired();

            entity.Property(o => o.Status)
                .IsRequired()
                .HasDefaultValue(AssignmentOccurrenceStatus.Pending);

            entity.Property(o => o.MaxGradeSnapshot)
                .HasColumnType("decimal(8,2)");

            entity.Property(o => o.PassingThresholdSnapshot)
                .HasColumnType("decimal(8,2)");

            entity.Property(o => o.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            // ── RELATIONSHIPS ─────────────────────────────────────────────────

            // Template FK: NoAction — occurrence dies with template (REQ-EXH-037).
            entity.HasOne(o => o.Template)
                .WithMany(t => t.Occurrences)
                .HasForeignKey(o => o.TemplateId)
                .OnDelete(DeleteBehavior.NoAction);

            // Teacher FK: NoAction — Teacher → Template → Occurrence already NoActions;
            // this FK exists only for the denormalized TeacherId.
            entity.HasOne(o => o.Teacher)
                .WithMany()
                .HasForeignKey(o => o.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // Session anchor FK (exam): SET NULL — deleting a session preserves the exam
            // occurrence with a null anchor. Fluent-only per §4.1 (no [ForeignKey] annotation).
            entity.HasOne(o => o.Session)
                .WithMany()
                .HasForeignKey(o => o.SessionId)
                .OnDelete(DeleteBehavior.SetNull);

            // DuringSession link FK (exam): SET NULL — mirrors AttendanceRecord.SessionOccurrence.
            entity.HasOne(o => o.SessionOccurrence)
                .WithMany()
                .HasForeignKey(o => o.SessionOccurrenceId)
                .OnDelete(DeleteBehavior.SetNull);

            // ── INDEXES ───────────────────────────────────────────────────────

            // Unique: one occurrence per (template, OccurrenceNumber).
            entity.HasIndex(o => new { o.TemplateId, o.OccurrenceNumber })
                .IsUnique()
                .HasDatabaseName("UX_AssignmentOccurrences_Template_OccurrenceNumber");

            // (Section 7.2 index #7) Occurrence by date — drives REQ-EXH-046 reports.
            entity.HasIndex(o => new { o.TeacherId, o.DueDate, o.TemplateId })
                .IncludeProperties(o => new { o.Status, o.OccurrenceNumber })
                .HasDatabaseName("IX_AssignmentOccurrences_DueDate");

            // Status filter for grade-entry workflows: which occurrences are still open?
            entity.HasIndex(o => new { o.TeacherId, o.Status })
                .HasDatabaseName("IX_AssignmentOccurrences_TeacherId_Status");

            // Exam per-session grouping: occurrences of a session for a teacher (exam-view/home).
            entity.HasIndex(o => new { o.TeacherId, o.SessionId })
                .HasDatabaseName("IX_AssignmentOccurrences_TeacherId_SessionId");

            // Attendance→exam sync hot path: find the exam occurrence for a session occurrence.
            entity.HasIndex(o => o.SessionOccurrenceId)
                .HasDatabaseName("IX_AssignmentOccurrences_SessionOccurrenceId");
        });
        #endregion

        #region StudentAssignmentObligation (REQ-EXH-007, 016/017/019, 026, 030/031/032, NFR-001/002)
        modelBuilder.Entity<StudentAssignmentObligation>(entity =>
        {
            entity.ToTable("StudentAssignmentObligations");

            // ── COLUMN MAPPINGS ───────────────────────────────────────────────

            entity.Property(o => o.Status)
                .IsRequired()
                .HasDefaultValue(ObligationStatus.Pending);

            entity.Property(o => o.GradeValue)
                .HasColumnType("decimal(8,2)");

            entity.Property(o => o.IsGradeEntered)
                .HasDefaultValue(false);

            entity.Property(o => o.MarkedByScan)
                .HasDefaultValue(false);

            entity.Property(o => o.UpdatedAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            entity.Property(o => o.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            // ── RELATIONSHIPS ─────────────────────────────────────────────────

            // Occurrence FK: NoAction — obligation dies with occurrence (REQ-EXH-037).
            entity.HasOne(o => o.Occurrence)
                .WithMany(occ => occ.Obligations)
                .HasForeignKey(o => o.OccurrenceId)
                .OnDelete(DeleteBehavior.NoAction);

            // TeacherStudent FK: Restrict — student deletion does NOT auto-delete obligations.
            // Service layer must explicitly remove obligations for a student being purged
            // (consistent with how the codebase handles student purge in other modules
            // via NullifyStudentReferencesOnRecordsAsync-style methods).
            entity.HasOne(o => o.TeacherStudent)
                .WithMany()
                .HasForeignKey(o => o.TeacherStudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Teacher FK: NoAction — denormalized only; NoAction flows through the occurrence chain.
            entity.HasOne(o => o.Teacher)
                .WithMany()
                .HasForeignKey(o => o.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // LastUpdatedByUser: audit FK — SetNull keeps the row if the user is removed.
            entity.HasOne(o => o.LastUpdatedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastUpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // ── INDEXES ───────────────────────────────────────────────────────

            // (Section 7.2 index #5) UNIQUE SAFETY NET — one obligation per (occurrence, student).
            // REQ-EXH-003: write-time dedup safety net. A service-layer bug inserting a duplicate
            // hits SQL 2601 unique violation here instead of silently corrupting the tracking view.
            // Also serves the "does this student already have an obligation" lookup for REQ-EXH-035.
            entity.HasIndex(o => new { o.OccurrenceId, o.TeacherStudentId })
                .IsUnique()
                .HasDatabaseName("UX_StudentAssignmentObligations_Occurrence_Student");

            // (Section 7.2 index #1) Tracking view — REQ-EXH-030. Tenant-leading covering index.
            // Drives the < 2-second NFR (REQ-EXH-NFR-001) at 50K students.
            entity.HasIndex(o => new { o.TeacherId, o.OccurrenceId, o.Status })
                .IncludeProperties(o => new { o.TeacherStudentId, o.GradeValue, o.IsGradeEntered, o.UpdatedAt })
                .HasDatabaseName("IX_StudentAssignmentObligations_Tracking");

            // (Section 7.2 index #2) Student history — REQ-EXH-040.
            entity.HasIndex(o => new { o.TeacherId, o.TeacherStudentId, o.CreateAt })
                .IsDescending(false, false, true)
                .IncludeProperties(o => new { o.OccurrenceId, o.Status, o.GradeValue, o.IsGradeEntered })
                .HasDatabaseName("IX_StudentAssignmentObligations_StudentHistory");

            // (Section 7.2 index #3) Absence reports — REQ-EXH-041, 042. Filtered to absent states.
            // Status values: 2 = NotDone, 5 = DidNotAttend.
            entity.HasIndex(o => new { o.TeacherId, o.TeacherStudentId, o.OccurrenceId })
                .IncludeProperties(o => new { o.Status })
                .HasFilter("[Status] IN (2, 5)")
                .HasDatabaseName("IX_StudentAssignmentObligations_Absence");

            // (Section 7.2 index #4) Grade Entry View — REQ-EXH-026-A. Filtered to grade-pending states.
            // Status values: 3 = Attended (exam, grade pending), 6 = DoneWithoutGrade (graded HW, grade pending).
            entity.HasIndex(o => new { o.TeacherId, o.OccurrenceId })
                .IncludeProperties(o => new { o.TeacherStudentId })
                .HasFilter("[Status] IN (3, 6)")
                .HasDatabaseName("IX_StudentAssignmentObligations_PendingGrades");
        });
        #endregion

        #region StudentObligationAuditLog (REQ-EXH-040, 043, 044 — historical reproducibility)
        modelBuilder.Entity<StudentObligationAuditLog>(entity =>
        {
            entity.ToTable("StudentObligationAuditLogs");

            // ── COLUMN MAPPINGS ───────────────────────────────────────────────

            entity.Property(a => a.OldStatus).IsRequired();
            entity.Property(a => a.NewStatus).IsRequired();

            entity.Property(a => a.OldGradeValue).HasColumnType("decimal(8,2)");
            entity.Property(a => a.NewGradeValue).HasColumnType("decimal(8,2)");
            entity.Property(a => a.MaxGradeSnapshot).HasColumnType("decimal(8,2)");
            entity.Property(a => a.PassingThresholdSnapshot).HasColumnType("decimal(8,2)");

            entity.Property(a => a.ChangeReason).HasMaxLength(500);

            entity.Property(a => a.ChangedAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            // ── RELATIONSHIPS ─────────────────────────────────────────────────

            // Obligation FK: Restrict — audit log is NOT NoAction-deleted with the obligation.
            // Per design decision 5.4, the service layer detaches audit logs (or copies them
            // to an archive table) BEFORE the cascading hard delete fires. This is the same
            // pattern the codebase already uses for AttendanceEditLog (Step 5.1 audit fix).
            entity.HasOne(a => a.StudentObligation)
                .WithMany(o => o.AuditLogs)
                .HasForeignKey(a => a.StudentObligationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Teacher FK: NoAction — when a teacher is purged, their audit history goes with them.
            entity.HasOne(a => a.Teacher)
                .WithMany()
                .HasForeignKey(a => a.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // ChangedByUser FK: SetNull — keep the audit row if the user is removed.
            entity.HasOne(a => a.ChangedByUser)
                .WithMany()
                .HasForeignKey(a => a.ChangedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // ── INDEXES ───────────────────────────────────────────────────────

            // History lookup for a single obligation, ordered by time.
            entity.HasIndex(a => new { a.StudentObligationId, a.ChangedAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_StudentObligationAuditLogs_Obligation_ChangedAt");

            // Tenant-scoped audit dashboard query: "all changes by tutor X in date range".
            // Designed to align with future range partitioning by ChangedAt month.
            entity.HasIndex(a => new { a.TeacherId, a.ChangedAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_StudentObligationAuditLogs_TeacherId_ChangedAt");
        });
        #endregion

        #region AssignmentDeletionLog (REQ-EXH-012, 037 — JSON snapshot pattern)
        modelBuilder.Entity<AssignmentDeletionLog>(entity =>
        {
            entity.ToTable("AssignmentDeletionLogs");

            // ── COLUMN MAPPINGS ───────────────────────────────────────────────

            // TemplateId: NO foreign key — the referenced template is hard-deleted.
            entity.Property(d => d.TemplateId).IsRequired();

            entity.Property(d => d.DeletionType).IsRequired();

            entity.Property(d => d.StudentsAffected)
                .IsRequired()
                .HasDefaultValue(0);

            // JSON snapshot: nvarchar(max) — SQL Server stores efficiently and supports
            // JSON_VALUE projections if any field becomes a hotspot.
            entity.Property(d => d.TemplateSnapshotJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(d => d.DeletedAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            // ── RELATIONSHIPS ─────────────────────────────────────────────────

            // Teacher FK: NoAction — deletion logs are tenant-scoped.
            entity.HasOne(d => d.Teacher)
                .WithMany()
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // DeletedByUser FK: SetNull — keep the log row if the user is removed.
            entity.HasOne(d => d.DeletedByUser)
                .WithMany()
                .HasForeignKey(d => d.DeletedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // ── INDEXES ───────────────────────────────────────────────────────

            // Tenant-scoped audit dashboard query.
            entity.HasIndex(d => new { d.TeacherId, d.DeletedAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_AssignmentDeletionLogs_TeacherId_DeletedAt");

            // Reverse lookup: "find the deletion record for template X" (template is gone,
            // but the Id is preserved in this column for forensic queries).
            entity.HasIndex(d => d.TemplateId)
                .HasDatabaseName("IX_AssignmentDeletionLogs_TemplateId");
            // AssignmentDeletionLog.LastOccurrence
            entity.HasOne(d => d.LastOccurrence)
                .WithMany()
                .HasForeignKey(d => d.LastOccurrenceId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(d => d.LastOccurrenceId)
    .HasFilter("[LastOccurrenceId] IS NOT NULL")
    .HasDatabaseName("IX_AssignmentDeletionLogs_LastOccurrenceId");
        });
        #endregion

        // ════════════════════════════════════════════════
        // SEED DATA
        // ════════════════════════════════════════════════

        #region Seed: Subjects (Egyptian Ministry of Education)
        modelBuilder.Entity<Subject>().HasData(
            new Subject { Id = 1, NameEn = "Arabic Language", NameAr = "اللغة العربية", IsActive = true, DisplayOrder = 1, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 2, NameEn = "English Language", NameAr = "اللغة الإنجليزية", IsActive = true, DisplayOrder = 2, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 3, NameEn = "Mathematics", NameAr = "الرياضيات", IsActive = true, DisplayOrder = 3, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 4, NameEn = "Science", NameAr = "العلوم", IsActive = true, DisplayOrder = 4, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 5, NameEn = "Social Studies", NameAr = "الدراسات الاجتماعية", IsActive = true, DisplayOrder = 5, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 6, NameEn = "French Language", NameAr = "اللغة الفرنسية", IsActive = true, DisplayOrder = 6, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 7, NameEn = "German Language", NameAr = "اللغة الألمانية", IsActive = true, DisplayOrder = 7, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 8, NameEn = "Physics", NameAr = "الفيزياء", IsActive = true, DisplayOrder = 8, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 9, NameEn = "Chemistry", NameAr = "الكيمياء", IsActive = true, DisplayOrder = 9, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 10, NameEn = "Biology", NameAr = "الأحياء", IsActive = true, DisplayOrder = 10, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 11, NameEn = "Geography", NameAr = "الجغرافيا", IsActive = true, DisplayOrder = 11, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 12, NameEn = "History", NameAr = "التاريخ", IsActive = true, DisplayOrder = 12, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 13, NameEn = "Philosophy & Logic", NameAr = "الفلسفة والمنطق", IsActive = true, DisplayOrder = 13, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 14, NameEn = "Psychology", NameAr = "علم النفس", IsActive = true, DisplayOrder = 14, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 15, NameEn = "Italian Language", NameAr = "اللغة الإيطالية", IsActive = true, DisplayOrder = 15, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 16, NameEn = "Spanish Language", NameAr = "اللغة الإسبانية", IsActive = true, DisplayOrder = 16, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 17, NameEn = "Computer Science", NameAr = "علوم الحاسب", IsActive = true, DisplayOrder = 17, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 18, NameEn = "Religious Studies", NameAr = "التربية الدينية", IsActive = true, DisplayOrder = 18, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 19, NameEn = "Art Education", NameAr = "التربية الفنية", IsActive = true, DisplayOrder = 19, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = 20, NameEn = "Music Education", NameAr = "التربية الموسيقية", IsActive = true, DisplayOrder = 20, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
        #endregion

        #region Seed: StudentCapacityPackages (AAM-FR-04.1)
        //modelBuilder.Entity<StudentCapacityPackage>().HasData(
        //    new StudentCapacityPackage { Id = 1, Name = "Up to 300", MinStudents = 0, MaxStudents = 300, IsActive = true, DisplayOrder = 1, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
        //    new StudentCapacityPackage { Id = 2, Name = "300 to 500", MinStudents = 300, MaxStudents = 500, IsActive = true, DisplayOrder = 2, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
        //    new StudentCapacityPackage { Id = 3, Name = "500 to 800", MinStudents = 500, MaxStudents = 800, IsActive = true, DisplayOrder = 3, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
        //    new StudentCapacityPackage { Id = 4, Name = "800 to 1200", MinStudents = 800, MaxStudents = 1200, IsActive = true, DisplayOrder = 4, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
        //    new StudentCapacityPackage { Id = 5, Name = "1200 to 1500", MinStudents = 1200, MaxStudents = 1500, IsActive = true, DisplayOrder = 5, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
        //    new StudentCapacityPackage { Id = 6, Name = "1500 to 3000", MinStudents = 1500, MaxStudents = 3000, IsActive = true, DisplayOrder = 6, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
        //    new StudentCapacityPackage { Id = 7, Name = "3000+", MinStudents = 3000, MaxStudents = null, IsActive = true, DisplayOrder = 7, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        //);
        #endregion


        // DESIGN DECISIONS embedded here:
        //
        // 1. COMPOSITE-FK TENANT INTEGRITY:
        //    VideoScope, VideoAnalytics, and VideoWatchEvent each carry a denormalized
        //    TeacherId column. To make cross-tenant corruption impossible at the DB
        //    level, those tables declare a composite FK (VideoAssetId, TeacherId)
        //    pointing at VideoAssets.(Id, TeacherId). The target requires a UNIQUE
        //    index over (Id, TeacherId) on VideoAssets — declared as the FIRST index
        //    in the VideoAsset region.
        //
        // 2. NoAction CHAINS & SQL SERVER'S MULTIPLE-NoAction-PATHS RULE:
        //    SQL Server forbids two NoAction paths from one parent to a single child.
        //    NoAction paths in this module:
        //      Teacher → VideoAsset (NoAction)            ┐
        //      Teacher → TeacherStudent (existing NoAction) ├ both reach VideoAnalytics
        //      VideoAsset → VideoAnalytics (NoAction)       ┘   if both NoAction
        //    Resolution: VideoAsset → child NoActions stay ON. Teacher → VideoAsset is
        //    set to Restrict; teacher hard-delete is an app-layer transactional flow
        //    (same pattern Module 6 uses for AssignmentOccurrence.Teacher → NoAction).
        //    TeacherStudent → VideoAnalytics is also Restrict for the same reason.
        //    Application-level deletion code is responsible for the orderly teardown.
        //
        // 3. CHECK CONSTRAINTS ON VideoScopes:
        //    Two database-level CHECKs make a malformed scope row impossible:
        //      a) Exactly one of (TeacherStudentId, SessionId, SessionGroupId) is non-null.
        //      b) ScopeType matches whichever target FK is populated.
        //    Same pattern as AssignmentScope (Module 6).
        //
        // 4. INDEX STRATEGY:
        //    Every index is justified by a query in the spec's §4 query plans.
        //    Filtered scope-target indexes carry INCLUDE (VideoAssetId, AssignedAt) to
        //    cover the access-resolution path without a key lookup (per blocker).
        // ════════════════════════════════════════════════════════════════════════════

        #region VideoAsset (REQ-VCM-FR-01 / Module 14)
        modelBuilder.Entity<VideoAsset>(entity =>
        {
            entity.ToTable("VideoAssets");

            // ── COLUMN MAPPINGS ───────────────────────────────────────────────

            entity.Property(v => v.Title)
                .HasMaxLength(VideoConstants.TitleMaxLength)
                .IsRequired();

            entity.Property(v => v.Description)
                .HasMaxLength(VideoConstants.DescriptionMaxLength);

            entity.Property(v => v.SourceUrl)
                .HasMaxLength(VideoConstants.SourceUrlMaxLength)
                .IsRequired();

            entity.Property(v => v.SourceType)
                .HasConversion<byte>()
                .IsRequired();

            entity.Property(v => v.ExternalId)
                .HasMaxLength(VideoConstants.ExternalIdMaxLength)
                .IsRequired();

            entity.Property(v => v.DurationSeconds)
                .HasDefaultValue(0)
                .IsRequired();

            // Track D1 — Draft/Published gate. Default Published so existing
            // rows (created before this column existed) keep their current
            // student visibility on migration.
            entity.Property(v => v.Status)
                .HasConversion<byte>()
                .HasDefaultValue(VideoStatus.Published)
                .IsRequired();

            entity.Property(v => v.PublishDate)
                .HasColumnType("datetime2(0)");

            entity.Property(v => v.CreateAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            entity.Property(v => v.UpdatedAt)
                .HasColumnType("datetime2(0)");

            entity.Property(v => v.RowVersion)
                .IsRowVersion();

            // The video photo (cover image) is a registry file reference (FileObject.Id).
            // Fluent-only, NoAction (app-layer / GC cleanup); no inverse navigation on FileObject.
            entity.HasOne<FileObject>()
                .WithMany()
                .HasForeignKey(v => v.VideoPhotoFileId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.Property(v => v.IsDurationManuallySet)
                .HasDefaultValue(false)
                .IsRequired();

            // ── RELATIONSHIPS ─────────────────────────────────────────────────
            // Teacher FK: NO_ACTION at DB level.
            //
            // NoAction chain decision: Option 2 from Phase 2.2 review.
            // We do NOT NoAction Teacher → VideoAssets at the DB level because we want
            // NoAction on TeacherStudents → VideoAnalytics/VideoWatchEvents (per
            // architectural decision). SQL Server forbids two NoAction paths from one
            // parent (Teachers) to one child (VideoAnalytics / VideoWatchEvents), so
            // the Teacher → VideoAssets edge is broken at NO_ACTION and the app-layer
            // admin "hard-purge teacher" flow is responsible for clearing VCM rows in
            // the right order. Day-to-day teacher-deactivate uses soft-delete on the
            // Teacher row (HasQueryFilter), which doesn't trigger DB NoActions anyway.
            entity.HasOne(v => v.Teacher)
                .WithMany()
                .HasForeignKey(v => v.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // CreatedByUser FK: SET NULL — the video survives even if the actor User
            // account is permanently removed. CreatedByUserId becomes long? to allow
            // the FK to be nulled. Matches the pattern of Teachers.CreatedByUserId,
            // AttendanceEditLog.AttendanceRecordId, and StudentSessionAssignment.
            entity.HasOne(v => v.CreatedByUser)
                .WithMany()
                .HasForeignKey(v => v.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // ── INDEXES ───────────────────────────────────────────────────────

            // CRITICAL — composite-FK target. Children carrying denormalized TeacherId
            // declare a composite FK against (Id, TeacherId), which requires a unique
            // index over those columns on the parent.
            entity.HasIndex(v => new { v.Id, v.TeacherId })
                .IsUnique()
                .HasDatabaseName("UX_VideoAssets_Id_TeacherId");

            // Story B / Q1 — teacher's video list, newest first.
            entity.HasIndex(v => new { v.TeacherId, v.CreateAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_VideoAssets_TeacherId_CreatedAt");

            // Optional duplicate-detection index. Q6(a) says we silently allow duplicate
            // URLs, but the index still earns its keep on a future "warn me on duplicate"
            // toggle and on the audit-trail "did this URL ever exist?" lookup.
            entity.HasIndex(v => new { v.TeacherId, v.ExternalId })
                .HasDatabaseName("IX_VideoAssets_TeacherId_ExternalId");
        });
        #endregion

        #region VideoUnit (Track C / G-UNIT)
        modelBuilder.Entity<VideoUnit>(entity =>
        {
            entity.ToTable("VideoUnits");

            entity.Property(u => u.Title)
                .HasMaxLength(VideoConstants.TitleMaxLength)
                .IsRequired();

            entity.Property(u => u.Description)
                .HasMaxLength(VideoConstants.DescriptionMaxLength);

            entity.Property(u => u.CreateAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            entity.HasOne(u => u.Teacher)
                .WithMany()
                .HasForeignKey(u => u.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(u => u.CreatedByUser)
                .WithMany()
                .HasForeignKey(u => u.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(u => new { u.TeacherId, u.CreateAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_VideoUnits_TeacherId_CreatedAt");

            // CRITICAL — composite-FK target for VideoUnitScope's denormalized
            // TeacherId column, same pattern as UX_VideoAssets_Id_TeacherId.
            entity.HasIndex(u => new { u.Id, u.TeacherId })
                .IsUnique()
                .HasDatabaseName("UX_VideoUnits_Id_TeacherId");

            // Soft-delete filter: queries exclude deleted records by default
            entity.HasQueryFilter(u => u.DeletedAt == null);
        });
        #endregion

        #region VideoAssetUnit (Video↔Unit M:N join, Module 14)
        modelBuilder.Entity<VideoAssetUnit>(b =>
        {
            b.ToTable("VideoAssetUnits");

            b.HasKey(x => new { x.VideoAssetId, x.UnitId });

            b.HasOne(x => x.VideoAsset)
                .WithMany(v => v.AssetUnits)
                .HasForeignKey(x => x.VideoAssetId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(x => x.Unit)
                .WithMany(u => u.AssetUnits)
                .HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        #endregion

        #region VideoUnitScope (collection-level Target Scope — final decision)
        modelBuilder.Entity<VideoUnitScope>(entity =>
        {
            entity.ToTable("VideoUnitScopes");

            // ── COLUMN MAPPINGS ───────────────────────────────────────────────

            entity.Property(s => s.ScopeType)
                .HasConversion<byte>()
                .IsRequired();

            entity.Property(s => s.AssignedAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            entity.Property(s => s.CreateAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            // ── COMPOSITE-FK TENANT INTEGRITY ─────────────────────────────────
            // (VideoUnitId, TeacherId) → VideoUnits(Id, TeacherId). Same
            // rationale as VideoScope's composite FK to VideoAssets — see that
            // region's remarks. Do NOT add a second HasOne(s => s.Teacher)
            // declaration; EF Core 10 would merge it into this one and drop
            // the OnDelete clause.
            entity.HasOne(s => s.VideoUnit)
                .WithMany(u => u.Scopes)
                .HasForeignKey(s => new { s.VideoUnitId, s.TeacherId })
                .HasPrincipalKey(u => new { u.Id, u.TeacherId })
                .OnDelete(DeleteBehavior.NoAction);

            // Three target FKs — same NoAction rationale as VideoScope's.
            entity.HasOne(s => s.TeacherStudent)
                .WithMany()
                .HasForeignKey(s => s.TeacherStudentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(s => s.Session)
                .WithMany()
                .HasForeignKey(s => s.SessionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(s => s.SessionGroup)
                .WithMany()
                .HasForeignKey(s => s.SessionGroupId)
                .OnDelete(DeleteBehavior.NoAction);

            // AssignedByUser: RESTRICT — preserve audit reference.
            entity.HasOne(s => s.AssignedByUser)
                .WithMany()
                .HasForeignKey(s => s.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── CHECK CONSTRAINTS ─────────────────────────────────────────────
            // Same two shape rules as VideoScope. Cross-row homogeneity (all
            // rows for one unit share one ScopeType) is an Application-layer
            // rule — SQL Server CHECK constraints cannot reference sibling rows.

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_VideoUnitScopes_ExactlyOneTarget",
                "(CASE WHEN [TeacherStudentId] IS NOT NULL THEN 1 ELSE 0 END" +
                " + CASE WHEN [SessionId]        IS NOT NULL THEN 1 ELSE 0 END" +
                " + CASE WHEN [SessionGroupId]   IS NOT NULL THEN 1 ELSE 0 END) = 1"));

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_VideoUnitScopes_ScopeTypeMatchesFK",
                "([ScopeType] = 0 AND [TeacherStudentId] IS NOT NULL)" +
                " OR ([ScopeType] = 1 AND [SessionId] IS NOT NULL)" +
                " OR ([ScopeType] = 2 AND [SessionGroupId] IS NOT NULL)"));

            // ── INDEXES ───────────────────────────────────────────────────────

            entity.HasIndex(s => s.VideoUnitId)
                .IncludeProperties(s => new
                {
                    s.ScopeType,
                    s.TeacherStudentId,
                    s.SessionId,
                    s.SessionGroupId,
                    s.AssignedAt
                })
                .HasDatabaseName("IX_VideoUnitScopes_VideoUnitId");

            // Filtered indexes per scope target — drive the union access-check
            // (video scope OR unit scope) the same way VideoScope's do.
            entity.HasIndex(s => s.TeacherStudentId)
                .HasFilter("[TeacherStudentId] IS NOT NULL")
                .IncludeProperties(s => new { s.VideoUnitId, s.AssignedAt })
                .HasDatabaseName("IX_VideoUnitScopes_TeacherStudentId");

            entity.HasIndex(s => s.SessionId)
                .HasFilter("[SessionId] IS NOT NULL")
                .IncludeProperties(s => new { s.VideoUnitId, s.AssignedAt })
                .HasDatabaseName("IX_VideoUnitScopes_SessionId");

            entity.HasIndex(s => s.SessionGroupId)
                .HasFilter("[SessionGroupId] IS NOT NULL")
                .IncludeProperties(s => new { s.VideoUnitId, s.AssignedAt })
                .HasDatabaseName("IX_VideoUnitScopes_SessionGroupId");

            // Composite uniqueness per (unit, scope-type, target) — same
            // .HasFilter((string?)null) override rationale as VideoScope's
            // equivalent index.
            entity.HasIndex(s => new
            {
                s.VideoUnitId,
                s.ScopeType,
                s.TeacherStudentId,
                s.SessionId,
                s.SessionGroupId
            })
                .IsUnique()
                .HasFilter((string?)null)
                .HasDatabaseName("UX_VideoUnitScopes_Unit_Type_Target");
        });
        #endregion

        // VideoAttachment was folded into the central FileObject registry (a video's attachments
        // are FileObjects of category VideoAttachment back-referencing the video via VideoAssetId).

        #region FileObject (central file registry — gated /api/files/{fileId})
        modelBuilder.Entity<FileObject>(entity =>
        {
            entity.ToTable("FileObjects");

            entity.Property(f => f.PublicId).IsRequired();
            entity.HasIndex(f => f.PublicId)
                .IsUnique()
                .HasDatabaseName("UX_FileObjects_PublicId");

            entity.Property(f => f.BlobPath)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(f => f.ContentType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(f => f.OriginalName)
                .HasMaxLength(260)
                .IsRequired();

            entity.Property(f => f.Category).IsRequired();
            entity.Property(f => f.Status).IsRequired();

            entity.Property(f => f.CreateAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            // A video's attachments point back here (one-to-many). Fluent-only, NoAction
            // (app-layer cascade / GC-driven cleanup) — no inverse navigation on VideoAsset.
            entity.HasOne<VideoAsset>()
                .WithMany()
                .HasForeignKey(f => f.VideoAssetId)
                .OnDelete(DeleteBehavior.NoAction);

            // GC scans Status + CreateAt; attach/detach and the gated read look up by PublicId
            // (already uniquely indexed above).
            entity.HasIndex(f => f.Status)
                .HasDatabaseName("IX_FileObjects_Status");
        });
        #endregion

        #region VideoExam / VideoExamQuestion / VideoExamQuestionOption (merged-creation refactor)
        modelBuilder.Entity<VideoExam>(entity =>
        {
            entity.ToTable("VideoExams");

            entity.Property(e => e.Title)
                .HasMaxLength(300)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasMaxLength(2000);

            entity.Property(e => e.CreateAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            // Composite-FK tenant integrity, same pattern as VideoAttachment —
            // CASCADE (not NoAction): exams have no forensic-snapshot
            // requirement, so a video hard-delete removes the exam tree with
            // zero service-layer cleanup.
            entity.HasOne(e => e.VideoAsset)
                .WithMany()
                .HasForeignKey(e => new { e.VideoAssetId, e.TeacherId })
                .HasPrincipalKey(v => new { v.Id, v.TeacherId })
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.VideoAssetId)
                .IsUnique()
                .HasDatabaseName("UX_VideoExams_VideoAssetId"); // one exam per video
        });

        modelBuilder.Entity<VideoExamQuestion>(entity =>
        {
            entity.ToTable("VideoExamQuestions");

            entity.Property(q => q.Text)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(q => q.QuestionType)
                .HasConversion<byte>()
                .IsRequired();

            entity.Property(q => q.CreateAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            entity.HasOne(q => q.Exam)
                .WithMany(e => e.Questions)
                .HasForeignKey(q => q.ExamId)
                .OnDelete(DeleteBehavior.Cascade);

            // Optional question image — registry file reference (FileObject.Id). Fluent-only,
            // NoAction (app-layer / GC cleanup); no inverse navigation on FileObject.
            entity.HasOne<FileObject>()
                .WithMany()
                .HasForeignKey(q => q.ImageFileId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(q => q.ExamId)
                .HasDatabaseName("IX_VideoExamQuestions_ExamId");
        });

        modelBuilder.Entity<VideoExamQuestionOption>(entity =>
        {
            entity.ToTable("VideoExamQuestionOptions");

            entity.Property(o => o.Text)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(o => o.CreateAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            entity.HasOne(o => o.Question)
                .WithMany(q => q.Options)
                .HasForeignKey(o => o.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(o => o.QuestionId)
                .HasDatabaseName("IX_VideoExamQuestionOptions_QuestionId");
        });
        #endregion


        #region VideoScope (REQ-VCM-FR-02 / Module 14)
        modelBuilder.Entity<VideoScope>(entity =>
        {
            entity.ToTable("VideoScopes");

            // ── COLUMN MAPPINGS ───────────────────────────────────────────────

            entity.Property(s => s.ScopeType)
                .HasConversion<byte>()
                .IsRequired();

            entity.Property(s => s.AssignedAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            entity.Property(s => s.CreateAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            // ── COMPOSITE-FK TENANT INTEGRITY ─────────────────────────────────
            //
            // (VideoAssetId, TeacherId) → VideoAssets(Id, TeacherId).
            // This single declaration covers BOTH:
            //   - the structural relationship to the parent video (the child NoActions
            //     when the video is hard-deleted)
            //   - the tenant-integrity guarantee (TeacherId on the child must equal
            //     TeacherId on the parent, enforced by the composite FK target index)
            //
            // The s.Teacher navigation works through this same FK column. Do NOT add
            // a second HasOne(s => s.Teacher) declaration — EF Core 10 merges it into
            // this one and drops our OnDelete clause.
            entity.HasOne(s => s.VideoAsset)
                .WithMany(v => v.Scopes)
                .HasForeignKey(s => new { s.VideoAssetId, s.TeacherId })
                .HasPrincipalKey(v => new { v.Id, v.TeacherId })
                .OnDelete(DeleteBehavior.NoAction);

            // Three target FKs — each uses its own delete behavior because the parent
            // for each is a different entity (TeacherStudent / Session / SessionGroup),
            // and the columns don't collide with the composite FK above.
            //
            // TeacherStudent: NoAction — student permanent-purge takes their direct
            // scope rows with them (matches the entity's class-remarks contract).
            entity.HasOne(s => s.TeacherStudent)
                .WithMany()
                .HasForeignKey(s => s.TeacherStudentId)
                .OnDelete(DeleteBehavior.NoAction);

            // Session: NoAction at DB level — session deletion is handled in the app
            // layer because SQL Server forbids multiple NoAction paths from Teacher.
            entity.HasOne(s => s.Session)
                .WithMany()
                .HasForeignKey(s => s.SessionId)
                .OnDelete(DeleteBehavior.NoAction);

            // SessionGroup: NoAction for the same reason as Session.
            entity.HasOne(s => s.SessionGroup)
                .WithMany()
                .HasForeignKey(s => s.SessionGroupId)
                .OnDelete(DeleteBehavior.NoAction);

            // AssignedByUser: RESTRICT — preserve audit reference.
            entity.HasOne(s => s.AssignedByUser)
                .WithMany()
                .HasForeignKey(s => s.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── CHECK CONSTRAINTS ─────────────────────────────────────────────

            // (1) Exactly one of the three target FKs is populated.
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_VideoScopes_ExactlyOneTarget",
                "(CASE WHEN [TeacherStudentId] IS NOT NULL THEN 1 ELSE 0 END" +
                " + CASE WHEN [SessionId]        IS NOT NULL THEN 1 ELSE 0 END" +
                " + CASE WHEN [SessionGroupId]   IS NOT NULL THEN 1 ELSE 0 END) = 1"));

            // (2) ScopeType discriminator matches whichever target FK is populated.
            //     0 = IndividualStudent, 1 = Session, 2 = SessionGroup.
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_VideoScopes_ScopeTypeMatchesFK",
                "([ScopeType] = 0 AND [TeacherStudentId] IS NOT NULL)" +
                " OR ([ScopeType] = 1 AND [SessionId] IS NOT NULL)" +
                " OR ([ScopeType] = 2 AND [SessionGroupId] IS NOT NULL)"));

            // ── INDEXES ───────────────────────────────────────────────────────

            // NoAction-delete & "load all scopes for this video" path.
            entity.HasIndex(s => s.VideoAssetId)
                .IncludeProperties(s => new
                {
                    s.ScopeType,
                    s.TeacherStudentId,
                    s.SessionId,
                    s.SessionGroupId,
                    s.AssignedAt
                })
                .HasDatabaseName("IX_VideoScopes_VideoAssetId");

            // Filtered indexes per scope target — drive the access-check resolver.
            entity.HasIndex(s => s.TeacherStudentId)
                .HasFilter("[TeacherStudentId] IS NOT NULL")
                .IncludeProperties(s => new { s.VideoAssetId, s.AssignedAt })
                .HasDatabaseName("IX_VideoScopes_TeacherStudentId");

            entity.HasIndex(s => s.SessionId)
                .HasFilter("[SessionId] IS NOT NULL")
                .IncludeProperties(s => new { s.VideoAssetId, s.AssignedAt })
                .HasDatabaseName("IX_VideoScopes_SessionId");

            entity.HasIndex(s => s.SessionGroupId)
                .HasFilter("[SessionGroupId] IS NOT NULL")
                .IncludeProperties(s => new { s.VideoAssetId, s.AssignedAt })
                .HasDatabaseName("IX_VideoScopes_SessionGroupId");

            // Composite uniqueness per (video, scope-type, target). Prevents two
            // identical scope rows on the same video — for example, the teacher
            // accidentally adding the morning session twice through quick double-click.
            //
            // .HasFilter((string?)null) overrides EF Core 10's automatic
            // "all-nullable-key-columns IS NOT NULL ANDed" filter, which is logically
            // impossible here given CK_VideoScopes_ExactlyOneTarget. Without this
            // override the index is silently disabled by a never-matching filter.
            //
            // NULLs are treated as distinct values, but ScopeType discriminates and
            // the CHECK constraint guarantees only one target column is populated per
            // row, so a duplicate scope row will still trip the unique constraint.
            entity.HasIndex(s => new
            {
                s.VideoAssetId,
                s.ScopeType,
                s.TeacherStudentId,
                s.SessionId,
                s.SessionGroupId
            })
                .IsUnique()
                .HasFilter((string?)null)
                .HasDatabaseName("UX_VideoScopes_Video_Type_Target");
        });
        #endregion

        #region VideoAnalytics (REQ-VCM-FR-04 / Module 14)
        modelBuilder.Entity<VideoAnalytics>(entity =>
        {
            entity.ToTable("VideoAnalytics");

            // ── COLUMN MAPPINGS ───────────────────────────────────────────────

            entity.Property(a => a.OpenCount)
                .HasDefaultValue(0)
                .IsRequired();

            entity.Property(a => a.TotalWatchSeconds)
                .HasColumnType("bigint")
                .HasDefaultValue(0L)
                .IsRequired();

            entity.Property(a => a.FirstOpenedAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            entity.Property(a => a.LastUpdated)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            entity.Property(a => a.VideoDurationAtFirstWatch)
                .IsRequired();

            entity.Property(a => a.LastResumePositionSeconds)
                .HasDefaultValue(0)
                .IsRequired();

            entity.Property(a => a.CreateAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            // ── COMPOSITE-FK TENANT INTEGRITY ─────────────────────────────────
            //
            // Single composite-FK declaration. Do NOT add a separate Teacher FK —
            // see VideoScope region remarks.
            entity.HasOne(a => a.VideoAsset)
                .WithMany(v => v.Analytics)
                .HasForeignKey(a => new { a.VideoAssetId, a.TeacherId })
                .HasPrincipalKey(v => new { v.Id, v.TeacherId })
                .OnDelete(DeleteBehavior.NoAction);

            // TeacherStudent FK: NoAction at DB level.
            //
            // NoAction chain decision: Option 2 from Phase 2.2 review. The
            // Teacher → VideoAssets edge is set to NO_ACTION on the parent side to
            // resolve SQL Server's multiple-NoAction-paths rule, leaving this edge as
            // the single live NoAction chain reaching VideoAnalytics from a deleted
            // student. When a TeacherStudent row is permanently purged, their watch
            // history goes with them.
            entity.HasOne(a => a.TeacherStudent)
                .WithMany()
                .HasForeignKey(a => a.TeacherStudentId)
                .OnDelete(DeleteBehavior.NoAction);

            // ── INDEXES ───────────────────────────────────────────────────────

            // Atomic UPSERT key.
            entity.HasIndex(a => new { a.VideoAssetId, a.TeacherStudentId })
                .IsUnique()
                .HasDatabaseName("UX_VideoAnalytics_Video_Student");

            // Student video-list "did I open this?" check.
            entity.HasIndex(a => new { a.TeacherStudentId, a.VideoAssetId })
                .IncludeProperties(a => new
                {
                    a.OpenCount,
                    a.TotalWatchSeconds,
                    a.LastResumePositionSeconds,
                    a.LastUpdated
                })
                .HasDatabaseName("IX_VideoAnalytics_TeacherStudentId_VideoAssetId");

            // Teacher analytics report driver.
            entity.HasIndex(a => a.VideoAssetId)
                .IncludeProperties(a => new
                {
                    a.TeacherStudentId,
                    a.OpenCount,
                    a.TotalWatchSeconds,
                    a.LastUpdated
                })
                .HasDatabaseName("IX_VideoAnalytics_VideoAssetId_Includes");
        });
        #endregion

    

        #region VideoWatchEvent (REQ-VCM-FR-03 / Module 14)
        modelBuilder.Entity<VideoWatchEvent>(entity =>
        {
            entity.ToTable("VideoWatchEvents");

            // ── COLUMN MAPPINGS ───────────────────────────────────────────────

            entity.Property(e => e.DeviceId)
                .HasMaxLength(VideoConstants.DeviceIdMaxLength)
                .IsRequired();

            entity.Property(e => e.EventType)
                .HasConversion<byte>()
                .IsRequired();

            entity.Property(e => e.PositionSeconds)
                .IsRequired();

            entity.Property(e => e.DeltaSinceLastSeconds)
                .HasDefaultValue(0)
                .IsRequired();

            entity.Property(e => e.EventUtc)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            entity.Property(e => e.CreateAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            // ClientEventId is nullable Guid — older clients may omit it. Stored as
            // uniqueidentifier; idempotency is enforced via the filtered unique index
            // declared further down.
            entity.Property(e => e.ClientEventId)
                .HasColumnType("uniqueidentifier");

            // ── COMPOSITE-FK TENANT INTEGRITY ─────────────────────────────────

            entity.HasOne(e => e.VideoAsset)
                .WithMany(v => v.WatchEvents)
                .HasForeignKey(e => new { e.VideoAssetId, e.TeacherId })
                .HasPrincipalKey(v => new { v.Id, v.TeacherId })
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Teacher)
                .WithMany()
                .HasForeignKey(e => e.TeacherId)
                .HasPrincipalKey(t => t.Id)
                .OnDelete(DeleteBehavior.NoAction);

            // TeacherStudent FK: NoAction — see VideoAnalytics region remarks for the
            // NoAction-graph reasoning.
            entity.HasOne(e => e.TeacherStudent)
                .WithMany()
                .HasForeignKey(e => e.TeacherStudentId)
                .OnDelete(DeleteBehavior.NoAction);

            // ── INDEXES ───────────────────────────────────────────────────────

            // The hot path: "find the most recent prior event for this device" —
            // drives delta validation on every Stop call. Single seek + TOP 1.
            entity.HasIndex(e => new
            {
                e.TeacherStudentId,
                e.VideoAssetId,
                e.DeviceId,
                e.EventUtc
            })
                .IsDescending(false, false, false, true)
                .HasDatabaseName("IX_VWE_Student_Video_Device_TimeDesc");

            // Per-video timeline reporting + speeds the NoAction-delete scan.
            entity.HasIndex(e => new { e.VideoAssetId, e.EventUtc })
                .IsDescending(false, true)
                .HasDatabaseName("IX_VWE_VideoAssetId_EventUtcDesc");

            // Filtered unique index — server-side idempotency for retried events.
            // Filter excludes legacy rows with no ClientEventId so older clients
            // continue to work without the unique constraint biting them.
            entity.HasIndex(e => e.ClientEventId)
                .IsUnique()
                .HasFilter("[ClientEventId] IS NOT NULL")
                .HasDatabaseName("UX_VWE_ClientEventId");
        });
        #endregion

        #region VideoAssetAudit (REQ-VCM-BR-03 / Module 14)
        modelBuilder.Entity<VideoAssetAudit>(entity =>
        {
            entity.ToTable("VideoAssetAudits");

            // ── COLUMN MAPPINGS ───────────────────────────────────────────────

            // VideoAssetId is a plain bigint — NO foreign key. The whole reason this
            // table exists is that the parent video has been deleted.
            entity.Property(a => a.VideoAssetId)
                .HasColumnType("bigint")
                .IsRequired();

            entity.Property(a => a.Action)
                .HasMaxLength(VideoConstants.AuditActionMaxLength)
                .IsRequired();

            entity.Property(a => a.SnapshotJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(a => a.SnapshotArchiveUrl)
                .HasMaxLength(VideoConstants.SnapshotArchiveUrlMaxLength);

            entity.Property(a => a.DeletedAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            entity.Property(a => a.CreateAt)
                .HasColumnType("datetime2(0)")
                .IsRequired();

            // ── RELATIONSHIPS ─────────────────────────────────────────────────

            // Teacher FK: RESTRICT — audit must outlive ordinary tenant operations.
            entity.HasOne(a => a.Teacher)
                .WithMany()
                .HasForeignKey(a => a.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            // DeletedByUser FK: RESTRICT — preserve actor reference forever.
            entity.HasOne(a => a.DeletedByUser)
                .WithMany()
                .HasForeignKey(a => a.DeletedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // ── INDEXES ───────────────────────────────────────────────────────

            entity.HasIndex(a => new { a.TeacherId, a.DeletedAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_VideoAssetAudits_TeacherId_DeletedAt");

            entity.HasIndex(a => a.VideoAssetId)
                .HasDatabaseName("IX_VideoAssetAudits_VideoAssetId");
        });
        #endregion
        // ════════════════════════════════════════════════
        // DIRECT CHAT CONFIGURATION (1:1 two-way messaging)
        // ════════════════════════════════════════════════

        #region Conversation (direct-chat pair)
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable("Conversations");

            entity.Property(c => c.LastMessagePreview)
                .HasMaxLength(200);

            // Participant A (smaller User.Id). NoAction — app-layer cascade.
            entity.HasOne(c => c.ParticipantAUser)
                .WithMany()
                .HasForeignKey(c => c.ParticipantAUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Participant B (larger User.Id). NoAction — second path to Users, so it
            // MUST be NoAction (SQL Server forbids multiple cascade paths).
            entity.HasOne(c => c.ParticipantBUser)
                .WithMany()
                .HasForeignKey(c => c.ParticipantBUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // One LIVE conversation per pair. Filtered so a future soft-delete +
            // recreate does not collide on the historical row.
            entity.HasIndex(c => new { c.ParticipantAUserId, c.ParticipantBUserId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("UX_Conversations_Participants");

            // Conversation-list queries: "my conversations ordered by recency" hits
            // either participant side, so index both.
            entity.HasIndex(c => new { c.ParticipantAUserId, c.LastMessageAt })
                .HasDatabaseName("IX_Conversations_ParticipantA_LastMessageAt");

            entity.HasIndex(c => new { c.ParticipantBUserId, c.LastMessageAt })
                .HasDatabaseName("IX_Conversations_ParticipantB_LastMessageAt");

            entity.HasQueryFilter(c => !c.IsDeleted);
        });
        #endregion

        #region ChatMessage (direct-chat message)
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages");

            entity.Property(m => m.Body)
                .HasMaxLength(4000)
                .IsRequired();

            // Conversation FK: NoAction (app-layer). Soft-delete handled in services.
            entity.HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.NoAction);

            // Sender FK: NoAction on account purge.
            entity.HasOne(m => m.SenderUser)
                .WithMany()
                .HasForeignKey(m => m.SenderUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Thread paging (newest-first / oldest-first by SentAt within a conversation).
            entity.HasIndex(m => new { m.ConversationId, m.SentAt })
                .HasDatabaseName("IX_ChatMessages_ConversationId_SentAt");

            // Unread-for-me: messages in a conversation NOT sent by the reader and unread.
            entity.HasIndex(m => new { m.ConversationId, m.SenderUserId, m.IsRead })
                .HasDatabaseName("IX_ChatMessages_Conversation_Sender_IsRead");

            entity.HasQueryFilter(m => !m.IsDeleted);
        });
        #endregion

        #region ModuleQuota (free-tier per-module creation limits)
        modelBuilder.Entity<ModuleQuota>(entity =>
        {
            entity.ToTable("ModuleQuotas");
            entity.HasKey(q => q.Id);
            entity.Property(q => q.ModuleKey).IsRequired().HasMaxLength(64);
            entity.Property(q => q.Description).HasMaxLength(256);
            entity.HasIndex(q => q.ModuleKey).IsUnique().HasDatabaseName("UX_ModuleQuotas_ModuleKey");

            // Seed one row per known module. Static values only (HasData requirement).
            var seededAt = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
            var examsSeededAt = new DateTime(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc);
            entity.HasData(
                new ModuleQuota { Id = 1, ModuleKey = ModuleQuotaKeys.Students, FreeTierLimit = 1, CreateAt = seededAt },
                new ModuleQuota { Id = 2, ModuleKey = ModuleQuotaKeys.Sessions, FreeTierLimit = 1, CreateAt = seededAt },
                new ModuleQuota { Id = 3, ModuleKey = ModuleQuotaKeys.Assistants, FreeTierLimit = 0, CreateAt = seededAt },
                new ModuleQuota { Id = 4, ModuleKey = ModuleQuotaKeys.Groups, FreeTierLimit = 0, CreateAt = seededAt },
                new ModuleQuota { Id = 5, ModuleKey = ModuleQuotaKeys.Videos, FreeTierLimit = 1, CreateAt = seededAt },
                new ModuleQuota { Id = 6, ModuleKey = ModuleQuotaKeys.AssignmentTemplates, FreeTierLimit = 1, CreateAt = seededAt },
                new ModuleQuota { Id = 7, ModuleKey = ModuleQuotaKeys.Events, FreeTierLimit = 1, CreateAt = seededAt },
                new ModuleQuota { Id = 8, ModuleKey = ModuleQuotaKeys.MessageTemplates, FreeTierLimit = 1, CreateAt = seededAt },
                new ModuleQuota { Id = 9, ModuleKey = ModuleQuotaKeys.Triggers, FreeTierLimit = 0, CreateAt = seededAt },
                // Exam quotas added 2026-07-17 (paper + online exams were the only ungated creatables).
                new ModuleQuota { Id = 10, ModuleKey = ModuleQuotaKeys.Exams, FreeTierLimit = 1, CreateAt = examsSeededAt },
                new ModuleQuota { Id = 11, ModuleKey = ModuleQuotaKeys.OnlineExams, FreeTierLimit = 1, CreateAt = examsSeededAt }
            );
        });
        #endregion

        // ════════════════════════════════════════════════
        // ONLINE EXAM MODULE CONFIGURATION
        // ════════════════════════════════════════════════
        //
        // NoAction CHAIN DECISION (mirrors VideoAnalytics "Option 2" — see that region's
        // design-decision comment): Teacher reaches StudentOnlineExamReport via three edges
        // — Teacher→Report (direct, denorm TeacherId), Teacher→TeacherStudent→Report, and
        // Teacher→OnlineExam (a sibling edge, not itself feeding Report). Every FK in this
        // module is explicitly NoAction — none are left to EF Core's Cascade/ClientSetNull
        // convention defaults, which is what actually causes SQL Server's multi-path
        // migration failures. Teacher→OnlineExam is the one edge called out explicitly
        // (do-not-reintroduce #2) because it is the edge the app-layer purge
        // (IOnlineExamRepo.PurgeExamGraphAsync) is responsible for tearing down before a
        // teacher hard-purge can proceed — the DB will never cascade it for you.

        #region OnlineExam
        modelBuilder.Entity<OnlineExam>(entity =>
        {
            entity.ToTable("OnlineExams", t =>
            {
                t.HasCheckConstraint("CK_OnlineExams_PassPercentageRange",
                    "[PassPercentage] >= 0 AND [PassPercentage] <= 100");

                t.HasCheckConstraint("CK_OnlineExams_DateOrder",
                    "[StartDateTime] < [EndDateTime]");

                t.HasCheckConstraint("CK_OnlineExams_MaxViolationsRange",
                    "[MaxViolations] >= 0");
            });

            entity.Property(e => e.Title).HasMaxLength(250).IsRequired();

            entity.Property(e => e.BlockOnViolation).HasDefaultValue(false);
            entity.Property(e => e.MaxViolations).HasDefaultValue(2);

            entity.Property(e => e.PassPercentage).HasColumnType("decimal(5,2)").IsRequired();

            entity.Property(e => e.StartDateTime).HasColumnType("datetime2(0)").IsRequired();
            entity.Property(e => e.EndDateTime).HasColumnType("datetime2(0)").IsRequired();

            entity.Property(e => e.Status).HasConversion<byte>().IsRequired();

            entity.Property(e => e.CreateAt).HasColumnType("datetime2(0)").IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");

            entity.Property(e => e.RowVersion).IsRowVersion();

            // ── RELATIONSHIPS ─────────────────────────────────────────────────
            entity.HasOne(e => e.Teacher)
                .WithMany()
                .HasForeignKey(e => e.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.UpdatedByUser)
                .WithMany()
                .HasForeignKey(e => e.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // ── INDEXES ───────────────────────────────────────────────────────
            // CRITICAL — composite-FK target for OnlineExamScope. Required BEFORE the
            // OnlineExamScope region below (principal-key unique index).
            entity.HasIndex(e => new { e.Id, e.TeacherId })
                .IsUnique()
                .HasDatabaseName("UX_OnlineExams_Id_TeacherId");

            entity.HasIndex(e => new { e.TeacherId, e.Status })
                .HasDatabaseName("IX_OnlineExams_TeacherId_Status");

            entity.HasIndex(e => new { e.TeacherId, e.Title })
                .HasDatabaseName("IX_OnlineExams_TeacherId_Title");
          
        });
        #endregion

        #region OnlineExamQuestion
        modelBuilder.Entity<OnlineExamQuestion>(entity =>
        {
            entity.ToTable("OnlineExamQuestions", t =>
                t.HasCheckConstraint("CK_OnlineExamQuestions_DegreePositive", "[Degree] > 0"));

            entity.Property(q => q.QuestionText).IsRequired();
            entity.Property(q => q.QuestionType).HasConversion<byte>().IsRequired();
            entity.Property(q => q.Degree).HasColumnType("decimal(6,2)").IsRequired();
            entity.Property(q => q.CreateAt).HasColumnType("datetime2(0)").IsRequired();

            entity.HasOne(q => q.OnlineExam)
                .WithMany(e => e.Questions)
                .HasForeignKey(q => q.OnlineExamId)
                .OnDelete(DeleteBehavior.NoAction);

            // Optional question image — registry file reference (FileObject.Id). Fluent-only,
            // NoAction (app-layer / GC cleanup); no inverse navigation on FileObject.
            entity.HasOne<FileObject>()
                .WithMany()
                .HasForeignKey(q => q.ImageFileId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(q => new { q.OnlineExamId, q.SortOrder })
                .HasDatabaseName("IX_OnlineExamQuestions_OnlineExamId_SortOrder");
        });
        #endregion

        #region OnlineExamQuestionOption
        modelBuilder.Entity<OnlineExamQuestionOption>(entity =>
        {
            entity.ToTable("OnlineExamQuestionOptions");

            entity.Property(o => o.OptionText).IsRequired();
            entity.Property(o => o.CreateAt).HasColumnType("datetime2(0)").IsRequired();

            entity.HasOne(o => o.Question)
                .WithMany(q => q.Options)
                .HasForeignKey(o => o.QuestionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(o => new { o.QuestionId, o.SortOrder })
                .HasDatabaseName("IX_OnlineExamQuestionOptions_QuestionId_SortOrder");
        });
        #endregion

        #region OnlineExamScope
        modelBuilder.Entity<OnlineExamScope>(entity =>
        {
            entity.ToTable("OnlineExamScopes", t =>
            {
                t.HasCheckConstraint("CK_OnlineExamScopes_ExactlyOneTarget",
                    "(CASE WHEN [SessionId] IS NOT NULL THEN 1 ELSE 0 END" +
                    " + CASE WHEN [SessionGroupId] IS NOT NULL THEN 1 ELSE 0 END) = 1");

                t.HasCheckConstraint("CK_OnlineExamScopes_ScopeTypeMatchesFK",
                    "([ScopeType] = 1 AND [SessionId] IS NOT NULL)" +
                    " OR ([ScopeType] = 2 AND [SessionGroupId] IS NOT NULL)");
            });

            entity.Property(s => s.ScopeType).HasConversion<byte>().IsRequired();
            entity.Property(s => s.AssignedAt).HasColumnType("datetime2(0)").IsRequired();
            entity.Property(s => s.CreateAt).HasColumnType("datetime2(0)").IsRequired();

            // ── COMPOSITE-FK TENANT INTEGRITY ─────────────────────────────────
            // Single declaration covers both the structural link to OnlineExam AND the
            // tenant-integrity guarantee. Do NOT add a second HasOne(s => s.Teacher) —
            // EF Core 10 merges it into this one and drops the OnDelete clause (VideoScope gotcha).
            entity.HasOne(s => s.OnlineExam)
                .WithMany(e => e.Scopes)
                .HasForeignKey(s => new { s.OnlineExamId, s.TeacherId })
                .HasPrincipalKey(e => new { e.Id, e.TeacherId })
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(s => s.Session)
                .WithMany()
                .HasForeignKey(s => s.SessionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(s => s.SessionGroup)
                .WithMany()
                .HasForeignKey(s => s.SessionGroupId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(s => s.AssignedByUser)
                .WithMany()
                .HasForeignKey(s => s.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(s => s.Teacher)
        .WithMany()
        .HasForeignKey(s => s.TeacherId)
        .OnDelete(DeleteBehavior.NoAction);
            // ── INDEXES ───────────────────────────────────────────────────────
            entity.HasIndex(s => s.OnlineExamId)
                .IncludeProperties(s => new { s.ScopeType, s.SessionId, s.SessionGroupId, s.AssignedAt })
                .HasDatabaseName("IX_OnlineExamScopes_OnlineExamId");

            // .HasFilter((string?)null) overrides EF Core 10's automatic all-nullable-key
            // filter — logically impossible here given CK_OnlineExamScopes_ExactlyOneTarget.
            // Without the override the index is silently disabled (VideoScopes gotcha).
            entity.HasIndex(s => new { s.OnlineExamId, s.ScopeType, s.SessionId, s.SessionGroupId })
                .IsUnique()
                .HasFilter((string?)null)
                .HasDatabaseName("UX_OnlineExamScopes_Exam_Type_Target");
        });
        #endregion

        #region StudentOnlineExamReport
        modelBuilder.Entity<StudentOnlineExamReport>(entity =>
        {
            entity.ToTable("StudentOnlineExamReports");

            entity.Property(r => r.Score).HasColumnType("decimal(6,2)").IsRequired();
            entity.Property(r => r.Percentage).HasColumnType("decimal(5,2)").IsRequired();
            entity.Property(r => r.Status).HasConversion<byte>().IsRequired();
            entity.Property(r => r.ViolationCount).HasDefaultValue(0);
            entity.Property(r => r.SubmittedAt).HasColumnType("datetime2(0)");
            entity.Property(r => r.UpdatedAt).HasColumnType("datetime2(0)");
            entity.Property(r => r.CreateAt).HasColumnType("datetime2(0)").IsRequired();
            entity.Property(r => r.RowVersion).IsRowVersion();

            // Standalone FKs — NOT composite (this is its own aggregate root, §1). Both kept
            // as live NoAction chains per the module-level design-decision comment above.
            entity.HasOne(r => r.OnlineExam)
                .WithMany()
                .HasForeignKey(r => r.OnlineExamId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(r => r.TeacherStudent)
                .WithMany()
                .HasForeignKey(r => r.TeacherStudentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(r => r.Teacher)
                .WithMany()
                .HasForeignKey(r => r.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // ── INDEXES ───────────────────────────────────────────────────────
            entity.HasIndex(r => new { r.OnlineExamId, r.TeacherStudentId })
                .IsUnique()
                .HasDatabaseName("UX_StudentOnlineExamReports_Exam_Student");

            entity.HasIndex(r => new { r.OnlineExamId, r.Status })
                .IncludeProperties(r => r.Percentage)
                .HasDatabaseName("IX_StudentOnlineExamReports_OnlineExamId_Status");

            entity.HasIndex(r => new { r.OnlineExamId, r.SubmittedAt })
                .HasDatabaseName("IX_StudentOnlineExamReports_OnlineExamId_SubmittedAt");
        });
        #endregion

        #region StudentQuestionAnswer
        modelBuilder.Entity<StudentQuestionAnswer>(entity =>
        {
            entity.ToTable("StudentQuestionAnswers");

            entity.Property(a => a.AwardedDegree).HasColumnType("decimal(6,2)").IsRequired();
            entity.Property(a => a.CreateAt).HasColumnType("datetime2(0)").IsRequired();

            entity.HasOne(a => a.StudentReport)
                .WithMany(r => r.Answers)
                .HasForeignKey(a => a.StudentReportId)
                .OnDelete(DeleteBehavior.NoAction);

            // Cross-aggregate reference — no back-collection on OnlineExamQuestion.
            entity.HasOne(a => a.Question)
                .WithMany()
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(a => new { a.StudentReportId, a.QuestionId })
                .IsUnique()
                .HasDatabaseName("UX_StudentQuestionAnswers_Report_Question");
        });
        #endregion

        #region StudentQuestionAnswerOption
        modelBuilder.Entity<StudentQuestionAnswerOption>(entity =>
        {
            entity.ToTable("StudentQuestionAnswerOptions");

            entity.Property(o => o.CreateAt).HasColumnType("datetime2(0)").IsRequired();

            entity.HasOne(o => o.StudentQuestionAnswer)
                .WithMany(a => a.SelectedOptions)
                .HasForeignKey(o => o.StudentQuestionAnswerId)
                .OnDelete(DeleteBehavior.NoAction);

            // NoAction here specifically prevents a converging delete path on
            // OnlineExamQuestionOption (do-not-reintroduce #1) — cross-aggregate reference,
            // no back-collection on OnlineExamQuestionOption.
            entity.HasOne(o => o.QuestionOption)
                .WithMany()
                .HasForeignKey(o => o.QuestionOptionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(o => new { o.StudentQuestionAnswerId, o.QuestionOptionId })
                .IsUnique()
                .HasDatabaseName("UX_StudentQuestionAnswerOptions_Answer_Option");
        });
        #endregion

        // ════════════════════════════════════════════════
        // STUDENT VIDEO-QUIZ ATTEMPT AGGREGATE (Module 14)
        // ════════════════════════════════════════════════
        //
        // Video-module twin of StudentOnlineExamReport, but with a DIFFERENT delete posture:
        // the report tree is CASCADE off VideoAsset (same posture as the VideoExam tree — see
        // that region), so a video hard-delete / teacher-purge tears down attempts at the DB
        // level with zero service-layer cleanup. The report tree
        // (report → answer → answer-option) is DISJOINT from the exam tree
        // (exam → question → option) — no table is reachable by two cascade paths, so there is
        // no SQL Server multi-cascade-path conflict. VideoExamId / TeacherId / TeacherStudentId
        // (report) and VideoExamQuestionId / VideoExamQuestionOptionId (answer/option) are plain
        // denormalized columns with NO navigation FK, so a teacher's quiz replace-all and any
        // student/teacher purge are never blocked by these tables. Fluent API is the sole source
        // of FK/OnDelete truth (no [ForeignKey] annotations on these entities).

        #region StudentVideoExamReport
        modelBuilder.Entity<StudentVideoExamReport>(entity =>
        {
            entity.ToTable("StudentVideoExamReports");

            entity.Property(r => r.Score).HasColumnType("decimal(6,2)").IsRequired();
            entity.Property(r => r.Percentage).HasColumnType("decimal(5,2)").IsRequired();
            entity.Property(r => r.Status).HasConversion<byte>().IsRequired();
            entity.Property(r => r.SubmittedAt).HasColumnType("datetime2(0)");
            entity.Property(r => r.UpdatedAt).HasColumnType("datetime2(0)");
            entity.Property(r => r.CreateAt).HasColumnType("datetime2(0)").IsRequired();
            entity.Property(r => r.RowVersion).IsRowVersion();

            // CASCADE delete root — mirrors VideoExam's posture off VideoAsset.
            entity.HasOne(r => r.VideoAsset)
                .WithMany()
                .HasForeignKey(r => r.VideoAssetId)
                .OnDelete(DeleteBehavior.Cascade);

            // VideoExamId / TeacherStudentId / TeacherId are plain scalar columns (no navigation
            // FK) — intentional (see region remarks). EF maps them as scalars automatically.

            entity.HasIndex(r => new { r.VideoAssetId, r.TeacherStudentId })
                .IsUnique()
                .HasDatabaseName("UX_StudentVideoExamReports_Video_Student");
        });
        #endregion

        #region StudentVideoExamAnswer
        modelBuilder.Entity<StudentVideoExamAnswer>(entity =>
        {
            entity.ToTable("StudentVideoExamAnswers");

            entity.Property(a => a.AwardedDegree).HasColumnType("decimal(6,2)").IsRequired();
            entity.Property(a => a.CreateAt).HasColumnType("datetime2(0)").IsRequired();

            entity.HasOne(a => a.StudentVideoExamReport)
                .WithMany(r => r.Answers)
                .HasForeignKey(a => a.StudentVideoExamReportId)
                .OnDelete(DeleteBehavior.Cascade);

            // VideoExamQuestionId is a plain cross-aggregate reference (no FK) — see region remarks.

            entity.HasIndex(a => new { a.StudentVideoExamReportId, a.VideoExamQuestionId })
                .IsUnique()
                .HasDatabaseName("UX_StudentVideoExamAnswers_Report_Question");
        });
        #endregion

        #region StudentVideoExamAnswerOption
        modelBuilder.Entity<StudentVideoExamAnswerOption>(entity =>
        {
            entity.ToTable("StudentVideoExamAnswerOptions");

            entity.Property(o => o.CreateAt).HasColumnType("datetime2(0)").IsRequired();

            entity.HasOne(o => o.StudentVideoExamAnswer)
                .WithMany(a => a.SelectedOptions)
                .HasForeignKey(o => o.StudentVideoExamAnswerId)
                .OnDelete(DeleteBehavior.Cascade);

            // VideoExamQuestionOptionId is a plain cross-aggregate reference (no FK).

            entity.HasIndex(o => new { o.StudentVideoExamAnswerId, o.VideoExamQuestionOptionId })
                .IsUnique()
                .HasDatabaseName("UX_StudentVideoExamAnswerOptions_Answer_Option");
        });
        #endregion
    }
}