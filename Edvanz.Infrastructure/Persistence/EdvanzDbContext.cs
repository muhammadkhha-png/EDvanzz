using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
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

        #endregion

        #region Existing unique constraints
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.PhoneNumber)
            .IsUnique();


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
                .OnDelete(DeleteBehavior.Cascade);

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
                .OnDelete(DeleteBehavior.Cascade);
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
                .OnDelete(DeleteBehavior.Cascade);
        });
        #endregion

        #region TeacherSubscription (1:N from Teacher)
        modelBuilder.Entity<TeacherSubscription>(entity =>
        {
            entity.ToTable("TeacherSubscriptions");

            entity.HasOne(s => s.Teacher)
                .WithMany(t => t.Subscriptions)
                .HasForeignKey(s => s.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.CreatedByUser)
                .WithMany()
                .HasForeignKey(s => s.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for subscription expiry queries (AAM-FR-08, REQ-SUB-005)
            entity.HasIndex(s => new { s.TeacherId, s.EndDate })
                .HasDatabaseName("IX_TeacherSubscriptions_TeacherId_EndDate");

            entity.HasIndex(s => s.SubscriptionStatus)
                .HasDatabaseName("IX_TeacherSubscriptions_Status");
        });
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
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
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

            // Composite unique: StudentCode is unique within each teacher's account
            entity.HasIndex(ts => new { ts.TeacherId, ts.StudentCode })
                .IsUnique()
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

            // Teacher FK: cascade delete when teacher account is removed
            entity.HasOne(ts => ts.Teacher)
                .WithMany()
                .HasForeignKey(ts => ts.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

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

        #region StudentTeacherLink (junction: StudentUser ↔ Teacher, AAM-FR-05.5)
        modelBuilder.Entity<StudentTeacherLink>(entity =>
        {
            entity.ToTable("StudentTeacherLinks");

            // Composite unique: a student can only link to the same teacher once
            // (prevents duplicate dashboard entries per AAM-FR-05.7)
            entity.HasIndex(stl => new { stl.StudentUserId, stl.TeacherId })
                .IsUnique()
                .HasDatabaseName("IX_StudentTeacherLinks_StudentUserId_TeacherId");

            // StudentUser FK: cascade delete when student user account is removed
            entity.HasOne(stl => stl.StudentUser)
                .WithMany(su => su.StudentTeacherLinks)
                .HasForeignKey(stl => stl.StudentUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Teacher FK: restrict — don't cascade teacher deletion to student links
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

            // Performance index: reverse lookup — which student users are linked to a teacher
            entity.HasIndex(stl => stl.TeacherId)
                .HasDatabaseName("IX_StudentTeacherLinks_TeacherId");

            // Performance index: fast join to teacher's student record for data access
            entity.HasIndex(stl => stl.TeacherStudentId)
                .HasDatabaseName("IX_StudentTeacherLinks_TeacherStudentId");

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

            // ParentUser FK: cascade delete when parent account is removed
            entity.HasOne(pc => pc.ParentUser)
                .WithMany(pu => pu.Children)
                .HasForeignKey(pc => pc.ParentUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // StudentUser FK: optional (null for Method B manual profiles)
            // Restrict: don't cascade student user deletion to parent child records
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

            // ParentChild FK: cascade delete when child record is removed
            entity.HasOne(pctl => pctl.ParentChild)
                .WithMany(pc => pc.TeacherLinks)
                .HasForeignKey(pctl => pctl.ParentChildId)
                .OnDelete(DeleteBehavior.Cascade);

            // Teacher FK: restrict — don't cascade teacher deletion to parent links
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

            // Unique group name per teacher
            entity.HasIndex(g => new { g.TeacherId, g.GroupName })
                .IsUnique()
                .HasDatabaseName("IX_SessionGroups_TeacherId_GroupName");

            entity.HasIndex(g => g.TeacherId)
                .HasDatabaseName("IX_SessionGroups_TeacherId");

            entity.HasOne(g => g.Teacher)
                .WithMany()
                .HasForeignKey(g => g.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);
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

            // Teacher FK: cascade delete when teacher account is removed
            entity.HasOne(s => s.Teacher)
                .WithMany()
                .HasForeignKey(s => s.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

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

            // Session FK (lower Id side): cascade delete
            entity.HasOne(sl => sl.Session)
                .WithMany(s => s.SessionLinksAsSource)
                .HasForeignKey(sl => sl.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            // LinkedSession FK (higher Id side): restrict to avoid multiple cascade paths
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

            // Unique: one occurrence per session per date
            entity.HasIndex(o => new { o.SessionId, o.OccurrenceDate })
                .IsUnique()
                .HasDatabaseName("IX_SessionOccurrences_SessionId_OccurrenceDate");

            // Workhorse index: "which sessions occur today for this teacher?"
            entity.HasIndex(o => new { o.TeacherId, o.OccurrenceDate })
                .HasDatabaseName("IX_SessionOccurrences_TeacherId_OccurrenceDate");

            // Dashboard filtering: completed vs. pending
            entity.HasIndex(o => new { o.TeacherId, o.Status })
                .HasDatabaseName("IX_SessionOccurrences_TeacherId_Status");

            // Teacher FK: CASCADE — teacher deletion removes all occurrences
            entity.HasOne(o => o.Teacher)
                .WithMany()
                .HasForeignKey(o => o.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            // Session FK: CASCADE — session hard-delete removes occurrences
            entity.HasOne(o => o.Session)
                .WithMany()
                .HasForeignKey(o => o.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
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

            // Teacher FK: CASCADE
            entity.HasOne(a => a.Teacher)
                .WithMany()
                .HasForeignKey(a => a.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

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

            // Teacher FK: CASCADE
            entity.HasOne(r => r.Teacher)
                .WithMany()
                .HasForeignKey(r => r.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

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

            // Audit Fix: Changed from Cascade to SetNull.
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

            // Teacher FK: CASCADE
            entity.HasOne(c => c.Teacher)
                .WithMany()
                .HasForeignKey(c => c.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

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

            // ── FOREIGN KEYS ──

            // Teacher FK: CASCADE — all payment data deleted with teacher account
            entity.HasOne(t => t.Teacher)
                .WithMany()
                .HasForeignKey(t => t.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

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

            // ── FOREIGN KEYS ──

            entity.HasOne(p => p.Teacher)
                .WithMany()
                .HasForeignKey(p => p.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

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

            // Teacher FK: CASCADE
            entity.HasOne(c => c.Teacher)
                .WithMany()
                .HasForeignKey(c => c.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

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
                .OnDelete(DeleteBehavior.Cascade);

            // Assistant FK: CASCADE — wallet deleted when assistant is deleted
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
                .OnDelete(DeleteBehavior.Cascade);

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
                .OnDelete(DeleteBehavior.Cascade);

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
                .OnDelete(DeleteBehavior.Cascade);

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

            entity.HasIndex(e => new { e.TeacherId, e.IsDeleted })
                .HasDatabaseName("IX_PE_TeacherId_IsDeleted");

            entity.HasOne(e => e.Teacher)
                .WithMany()
                .HasForeignKey(e => e.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);
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
                .OnDelete(DeleteBehavior.Cascade);

            // PaymentEvent FK: CASCADE — obligation removed when event is deleted
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
                .OnDelete(DeleteBehavior.Cascade);

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
        modelBuilder.Entity<StudentCapacityPackage>().HasData(
            new StudentCapacityPackage { Id = 1, Name = "Up to 300", MinStudents = 0, MaxStudents = 300, IsActive = true, DisplayOrder = 1, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new StudentCapacityPackage { Id = 2, Name = "300 to 500", MinStudents = 300, MaxStudents = 500, IsActive = true, DisplayOrder = 2, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new StudentCapacityPackage { Id = 3, Name = "500 to 800", MinStudents = 500, MaxStudents = 800, IsActive = true, DisplayOrder = 3, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new StudentCapacityPackage { Id = 4, Name = "800 to 1200", MinStudents = 800, MaxStudents = 1200, IsActive = true, DisplayOrder = 4, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new StudentCapacityPackage { Id = 5, Name = "1200 to 1500", MinStudents = 1200, MaxStudents = 1500, IsActive = true, DisplayOrder = 5, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new StudentCapacityPackage { Id = 6, Name = "1500 to 3000", MinStudents = 1500, MaxStudents = 3000, IsActive = true, DisplayOrder = 6, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new StudentCapacityPackage { Id = 7, Name = "3000+", MinStudents = 3000, MaxStudents = null, IsActive = true, DisplayOrder = 7, CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
        #endregion
    }
}