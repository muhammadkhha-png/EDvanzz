using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
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


    //  ─── Assistant  ───
    public DbSet<Assistant> Assistants { get; set; }
    public DbSet<LoginActivityAssistantLog> AssistantLoginActivity { get; set; }
    public DbSet<AuditTrail> AuditTrial { get; set; }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
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
           .HasKey(ur => new { ur.userId, ur.TemplateId });
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