using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Edvanz.Infrastructure.Persistence;

public class EdvanzDbContext(DbContextOptions<EdvanzDbContext> options) : DbContext(options)
{
    // ─── Existing tables ───
    public DbSet<User> Users { get; set; }
    public DbSet<Assistant> Assistants { get; set; }
    public DbSet<UsersTutor> UserTutor { get; set; }
    public DbSet<AssistantPermission> AssistantPermissions { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    // ─── Teacher module tables ───
    public DbSet<Teacher> Teachers { get; set; }
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<TeacherSubject> TeacherSubjects { get; set; }
    public DbSet<StudentCapacityPackage> StudentCapacityPackages { get; set; }
    public DbSet<TeacherConfiguration> TeacherConfigurations { get; set; }
    public DbSet<TeacherProratedTier> TeacherProratedTiers { get; set; }
    public DbSet<TeacherSubscription> TeacherSubscriptions { get; set; }

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
        modelBuilder.Entity<AssistantPermission>()
            .HasKey(ap => new { ap.UserId, ap.PermissionId });

        modelBuilder.Entity<UsersTutor>()
            .HasKey(ut => new { ut.userId, ut.TutorId });
        #endregion

        #region Existing unique constraints
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.PhoneNumber)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
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