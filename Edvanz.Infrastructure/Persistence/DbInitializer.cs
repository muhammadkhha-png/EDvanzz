//using Edvanz.Domain.Entities;
//using Microsoft.EntityFrameworkCore;
//using System;
//using System.Collections.Generic;
//using System.Text;

//namespace Edvanz.Infrastructure.Persistence
//{
//    public class DbInitializer
//    {
//        public static async Task SeedAsync(EdvanzDbContext context)
//        {
//            if (!context.Models.Any())
//            {
//                context.Models.AddRange(
//                    new Module { Name = "Student" },
//                    new Module { Name = "Session" },
//                    new Module { Name = "Attendance" },
//                     new Module { Name = "Payment" },
//                     new Module { Name = "Event-Based Payment" },
//                     new Module { Name = "Exams And Homework" },
//                     new Module { Name = "Messaging" }

//                );

//                await context.SaveChangesAsync();
//            }
//            // 2️⃣ Get Modules with Ids
//            var modules = await context.Models
//                .ToDictionaryAsync(m => m.Name.Trim(), m => m.Id);

//            // 3️⃣ Seed Permissions
//            if (!context.Permissions.Any())
//            {
//                var permissions = new List<Permission>
//            {
//                // Student
//                new Permission { Name = "ViewList", ModuleId = modules["Student"], IsRestricted = false },
//                new Permission { Name = "ViewProfile", ModuleId = modules["Student"], IsRestricted = false },
//                new Permission { Name = "Add", ModuleId = modules["Student"], IsRestricted = false },
//                new Permission { Name = "Edit", ModuleId = modules["Student"], IsRestricted = false },
//                new Permission { Name = "Delete", ModuleId = modules["Student"], IsRestricted = false },
//                new Permission { Name = "Import", ModuleId = modules["Student"], IsRestricted = false },
//                new Permission { Name = "ExportReports", ModuleId = modules["Student"], IsRestricted = false },
//                new Permission { Name = "ViewBarcodes", ModuleId = modules["Student"], IsRestricted = false },
//                // Session
//                new Permission { Name = "View", ModuleId = modules["Session"], IsRestricted = false },
//                new Permission { Name = "Create", ModuleId = modules["Session"], IsRestricted = false },
//                new Permission { Name = "Edit", ModuleId = modules["Session"], IsRestricted = false },
//                new Permission { Name = "Delete", ModuleId = modules["Session"], IsRestricted = false },
//                new Permission { Name = "ViewGroups", ModuleId = modules["Session"], IsRestricted = false },
//                new Permission { Name = "AssignStudents", ModuleId = modules["Session"], IsRestricted = false },
//                new Permission { Name = "ManageGroups", ModuleId = modules["Session"], IsRestricted = false },
//                new Permission { Name = "ViewMembership", ModuleId = modules["Session"], IsRestricted = false },
//                new Permission { Name = "ManageMembership", ModuleId = modules["Session"], IsRestricted = false },
//                // Attendance
//                new Permission { Name = "Take", ModuleId = modules["Attendance"], IsRestricted = false },
//                new Permission { Name = "Edit", ModuleId = modules["Attendance"], IsRestricted = false },
//                new Permission { Name = "ViewHistory", ModuleId = modules["Attendance"], IsRestricted = false },
//                new Permission { Name = "ViewAbsenceOverview", ModuleId = modules["Attendance"], IsRestricted = false },
//                new Permission { Name = "GenerateReports", ModuleId = modules["Attendance"], IsRestricted = false },
//                // Payment
//                new Permission { Name = "Collect", ModuleId = modules["Payment"], IsRestricted = false },
//                new Permission { Name = "ViewHistor", ModuleId = modules["Payment"], IsRestricted = false },
//                new Permission { Name = "EditHistory", ModuleId = modules["Payment"], IsRestricted = true },
//                new Permission { Name = "ViewUnpaidStudents", ModuleId = modules["Payment"], IsRestricted = false },
//                new Permission { Name = "ViewCollectorSummary", ModuleId = modules["Payment"], IsRestricted = false },
//                new Permission { Name = "GenerateReports", ModuleId = modules["Payment"], IsRestricted = false },
//               // Event-Based Payment
//                new Permission { Name = "View", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
//                new Permission { Name = "Create", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
//                new Permission { Name = "Edit", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
//                new Permission { Name = "Delete", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
//                new Permission { Name = "CollectPayment", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
//                new Permission { Name = "GenerateReports", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
//                // Exams & Homework
//                new Permission { Name = "View", ModuleId = modules["Exams And Homework"], IsRestricted = false },
//                new Permission { Name = "Create", ModuleId = modules["Exams And Homework"], IsRestricted = false },
//                new Permission { Name = "Edit", ModuleId = modules["Exams And Homework"], IsRestricted = false },
//                new Permission { Name = "Delete", ModuleId = modules["Exams And Homework"], IsRestricted = false }, 
//                new Permission { Name = "RecordExamAttendanceAndGrades", ModuleId = modules["Exams And Homework"], IsRestricted = false },
//                new Permission { Name = "RecordHomeworkCompletion", ModuleId = modules["Exams And Homework"], IsRestricted = false },
//                new Permission { Name = "EnterPendingGrades", ModuleId = modules["Exams And Homework"], IsRestricted = false },
//                new Permission { Name = "GenerateReports", ModuleId = modules["Exams And Homework"], IsRestricted = false },
//                // Messaging
//                new Permission { Name = "ViewHistory", ModuleId = modules["Messaging"], IsRestricted = false },
//                new Permission { Name = "SendManual", ModuleId = modules["Messaging"], IsRestricted = false },
//                new Permission { Name = "ManageTemplates", ModuleId = modules["Messaging"], IsRestricted = false },
//                new Permission { Name = "ConfigureAutomatedTriggers", ModuleId = modules["Messaging"], IsRestricted = false },
//                //new Permission { Name = "ConfigureChannels", ModuleId = modules["Messaging"], IsRestricted = true } // to delete

//            };

//                context.Permissions.AddRange(permissions);

//                await context.SaveChangesAsync();
//            }

//            // 4️⃣ Seed StudentCapacityPackages
//            if (!context.StudentCapacityPackages.Any())
//            {
//                var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

//                var packages = new List<StudentCapacityPackage>
//    {
//        new StudentCapacityPackage
//        {
//            Name = "Up to 300",
//            MinStudents = 0,
//            MaxStudents = 300,
//            IsActive = true,
//            DisplayOrder = 1,
//            CreateAt = now,
//            MonthlyPriceEGP = 0 // will be updated by admin later
//        },
//        new StudentCapacityPackage
//        {
//            Name = "300 to 500",
//            MinStudents = 300,
//            MaxStudents = 500,
//            IsActive = true,
//            DisplayOrder = 2,
//            CreateAt = now,
//            MonthlyPriceEGP = 0
//        },
//        new StudentCapacityPackage
//        {
//            Name = "500 to 800",
//            MinStudents = 500,
//            MaxStudents = 800,
//            IsActive = true,
//            DisplayOrder = 3,
//            CreateAt = now,
//            MonthlyPriceEGP = 0
//        },
//        new StudentCapacityPackage
//        {
//            Name = "800 to 1200",
//            MinStudents = 800,
//            MaxStudents = 1200,
//            IsActive = true,
//            DisplayOrder = 4,
//            CreateAt = now,
//            MonthlyPriceEGP = 0
//        },
//        new StudentCapacityPackage
//        {
//            Name = "1200 to 1500",
//            MinStudents = 1200,
//            MaxStudents = 1500,
//            IsActive = true,
//            DisplayOrder = 5,
//            CreateAt = now,
//            MonthlyPriceEGP = 0
//        },
//        new StudentCapacityPackage
//        {
//            Name = "1500 to 3000",
//            MinStudents = 1500,
//            MaxStudents = 3000,
//            IsActive = true,
//            DisplayOrder = 6,
//            CreateAt = now,
//            MonthlyPriceEGP = 0
//        },
//        new StudentCapacityPackage
//        {
//            Name = "3000+",
//            MinStudents = 3000,
//            MaxStudents = null,
//            IsActive = true,
//            DisplayOrder = 7,
//            CreateAt = now,
//            MonthlyPriceEGP = 0
//        }
//    };

//                context.StudentCapacityPackages.AddRange(packages);
//                await context.SaveChangesAsync();
//            }
//        }
//    }

//}
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.ServiceContract;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Edvanz.Infrastructure.Persistence
{
    /// <summary>
    /// Database seeder for first-deploy data: lookup tables (Modules, Permissions,
    /// StudentCapacityPackages) and a baseline set of users covering every UserType.
    ///
    /// IDEMPOTENCY:
    ///   Every block guards on <c>!context.X.Any(...)</c>, so running this seeder
    ///   repeatedly does not duplicate rows. Safe to invoke on every application
    ///   startup.
    ///
    /// SECURITY:
    ///   Default password for ALL seeded users is <c>Edvanz@2026</c>. Per
    ///   REQ-ADM-003, the super admin MUST change this on first login. Document
    ///   this in your deployment runbook.
    ///
    /// ONION COMPLIANCE:
    ///   The seeder lives in the Infrastructure layer and consumes the Application
    ///   abstraction <see cref="IPasswordService"/> for password hashing — never
    ///   inlines crypto. This keeps hashing identical to the runtime registration
    ///   path so seeded passwords work with the existing Login flow.
    /// </summary>
    public class DbInitializer
    {
        // ════════════════════════════════════════════════
        // SEED CONSTANTS
        // ════════════════════════════════════════════════

        /// <summary>
        /// First-deploy default password applied to every seeded user. Documented
        /// in the deployment runbook; super admin must change on first login (REQ-ADM-003).
        /// </summary>
        private const string DefaultSeedPassword = "Edvanz@2026";

        private const string EnglishLanguage = "en";

        // ════════════════════════════════════════════════
        // PUBLIC ENTRY POINT
        // ════════════════════════════════════════════════

        /// <summary>
        /// Seeds the database with reference data and baseline user accounts.
        /// Order matters because of foreign keys — modules and permissions first,
        /// then users and their dependents.
        /// </summary>
        /// <param name="context">The application DbContext.</param>
        /// <param name="passwordService">Hashing service shared with the runtime app.</param>
        public static async Task SeedAsync(EdvanzDbContext context, IPasswordService passwordService)
        {
            await SeedModulesAsync(context);
            await SeedPermissionsAsync(context);
            await SeedStudentCapacityPackagesAsync(context);
            await SeedUsersAsync(context, passwordService);
        }

        // ════════════════════════════════════════════════
        // EXISTING SEED — MODULES (preserved)
        // ════════════════════════════════════════════════

        private static async Task SeedModulesAsync(EdvanzDbContext context)
        {
            if (context.Models.Any()) return;

            context.Models.AddRange(
                new Module { Name = "Student" },
                new Module { Name = "Session" },
                new Module { Name = "Attendance" },
                new Module { Name = "Payment" },
                new Module { Name = "Event-Based Payment" },
                new Module { Name = "Exams And Homework" },
                new Module { Name = "Messaging" }
            );

            await context.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════
        // EXISTING SEED — PERMISSIONS (preserved)
        // ════════════════════════════════════════════════

        private static async Task SeedPermissionsAsync(EdvanzDbContext context)
        {
            if (context.Permissions.Any()) return;

            var modules = await context.Models
                .ToDictionaryAsync(m => m.Name.Trim(), m => m.Id);

            var permissions = new List<Permission>
            {
                // Student
                new Permission { Name = "ViewList", ModuleId = modules["Student"], IsRestricted = false },
                new Permission { Name = "ViewProfile", ModuleId = modules["Student"], IsRestricted = false },
                new Permission { Name = "Add", ModuleId = modules["Student"], IsRestricted = false },
                new Permission { Name = "Edit", ModuleId = modules["Student"], IsRestricted = false },
                new Permission { Name = "Delete", ModuleId = modules["Student"], IsRestricted = false },
                new Permission { Name = "Import", ModuleId = modules["Student"], IsRestricted = false },
                new Permission { Name = "ExportReports", ModuleId = modules["Student"], IsRestricted = false },
                new Permission { Name = "ViewBarcodes", ModuleId = modules["Student"], IsRestricted = false },
                // Session
                new Permission { Name = "View", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "Create", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "Edit", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "Delete", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "ViewGroups", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "AssignStudents", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "ManageGroups", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "ViewMembership", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "ManageMembership", ModuleId = modules["Session"], IsRestricted = false },
                // Attendance
                new Permission { Name = "Take", ModuleId = modules["Attendance"], IsRestricted = false },
                new Permission { Name = "Edit", ModuleId = modules["Attendance"], IsRestricted = false },
                new Permission { Name = "ViewHistory", ModuleId = modules["Attendance"], IsRestricted = false },
                new Permission { Name = "ViewAbsenceOverview", ModuleId = modules["Attendance"], IsRestricted = false },
                new Permission { Name = "GenerateReports", ModuleId = modules["Attendance"], IsRestricted = false },
                // Payment
                new Permission { Name = "Collect", ModuleId = modules["Payment"], IsRestricted = false },
                new Permission { Name = "ViewHistor", ModuleId = modules["Payment"], IsRestricted = false },
                new Permission { Name = "EditHistory", ModuleId = modules["Payment"], IsRestricted = true },
                new Permission { Name = "ViewUnpaidStudents", ModuleId = modules["Payment"], IsRestricted = false },
                new Permission { Name = "ViewCollectorSummary", ModuleId = modules["Payment"], IsRestricted = false },
                new Permission { Name = "GenerateReports", ModuleId = modules["Payment"], IsRestricted = false },
                // Event-Based Payment
                new Permission { Name = "View", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
                new Permission { Name = "Create", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
                new Permission { Name = "Edit", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
                new Permission { Name = "Delete", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
                new Permission { Name = "CollectPayment", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
                new Permission { Name = "GenerateReports", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
                // Exams & Homework
                new Permission { Name = "View", ModuleId = modules["Exams And Homework"], IsRestricted = false },
                new Permission { Name = "Create", ModuleId = modules["Exams And Homework"], IsRestricted = false },
                new Permission { Name = "Edit", ModuleId = modules["Exams And Homework"], IsRestricted = false },
                new Permission { Name = "Delete", ModuleId = modules["Exams And Homework"], IsRestricted = false },
                new Permission { Name = "RecordExamAttendanceAndGrades", ModuleId = modules["Exams And Homework"], IsRestricted = false },
                new Permission { Name = "RecordHomeworkCompletion", ModuleId = modules["Exams And Homework"], IsRestricted = false },
                new Permission { Name = "EnterPendingGrades", ModuleId = modules["Exams And Homework"], IsRestricted = false },
                new Permission { Name = "GenerateReports", ModuleId = modules["Exams And Homework"], IsRestricted = false },
                // Messaging
                new Permission { Name = "ViewHistory", ModuleId = modules["Messaging"], IsRestricted = false },
                new Permission { Name = "SendManual", ModuleId = modules["Messaging"], IsRestricted = false },
                new Permission { Name = "ManageTemplates", ModuleId = modules["Messaging"], IsRestricted = false },
                new Permission { Name = "ConfigureAutomatedTriggers", ModuleId = modules["Messaging"], IsRestricted = false },
            };

            context.Permissions.AddRange(permissions);
            await context.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════
        // EXISTING SEED — STUDENT CAPACITY PACKAGES (preserved)
        // ════════════════════════════════════════════════

        private static async Task SeedStudentCapacityPackagesAsync(EdvanzDbContext context)
        {
            if (context.StudentCapacityPackages.Any()) return;

            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var packages = new List<StudentCapacityPackage>
            {
                new StudentCapacityPackage { Name = "Up to 300", MinStudents = 0, MaxStudents = 300, IsActive = true, DisplayOrder = 1, CreateAt = now },
                new StudentCapacityPackage { Name = "300 to 500", MinStudents = 300, MaxStudents = 500, IsActive = true, DisplayOrder = 2, CreateAt = now },
                new StudentCapacityPackage { Name = "500 to 800", MinStudents = 500, MaxStudents = 800, IsActive = true, DisplayOrder = 3, CreateAt = now },
                new StudentCapacityPackage { Name = "800 to 1200", MinStudents = 800, MaxStudents = 1200, IsActive = true, DisplayOrder = 4, CreateAt = now },
                new StudentCapacityPackage { Name = "1200 to 1500", MinStudents = 1200, MaxStudents = 1500, IsActive = true, DisplayOrder = 5, CreateAt = now },
                new StudentCapacityPackage { Name = "1500 to 3000", MinStudents = 1500, MaxStudents = 3000, IsActive = true, DisplayOrder = 6, CreateAt = now },
                new StudentCapacityPackage { Name = "3000+", MinStudents = 3000, MaxStudents = null, IsActive = true, DisplayOrder = 7, CreateAt = now }
            };

            context.StudentCapacityPackages.AddRange(packages);
            await context.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════
        // NEW SEED — USERS (first-deploy baseline)
        // ════════════════════════════════════════════════

        /// <summary>
        /// Seeds one user of every UserType plus the module/permission graph
        /// described in the class summary. Idempotent — re-running does nothing
        /// once the super-admin row exists.
        /// </summary>
        private static async Task SeedUsersAsync(EdvanzDbContext context, IPasswordService passwordService)
        {
            // Use the super admin's existence as the marker for "users already seeded".
            // A more granular guard isn't needed because the whole user-seed block
            // either ran completely or didn't — we wrap it in a transaction below.
            bool alreadySeeded = await context.Users
                .AnyAsync(u => u.UserType == UserType.SuperAdmin);

            if (alreadySeeded) return;

            string hashedPassword = passwordService.HashPassword(DefaultSeedPassword);
            DateTime now = DateTime.UtcNow;

            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                // ── 1. SuperAdmin (REQ-ADM-001: exactly one) ──
                var superAdmin = await SeedSuperAdminAsync(context, hashedPassword, now);

                // ── 2. Teachers ──
                // Teacher #1: assigned ALL 7 modules (the "fully-enabled" tutor).
                var teacher1 = await SeedTeacherAsync(
                    context, hashedPassword, now,
                    fullName: "Ahmed Mostafa",
                    username: "teacher1",
                    email: "teacher1@edvanz.example",
                    phoneNumber: "01000000001",
                    teacherCode: "T0000001",
                    studentCapacity: 300,
                    createdByUserId: superAdmin.Id);

                await AssignAllModulesToTeacherAsync(context, teacher1.Id);

                // Teacher #2: assigned only 3 modules (Student, Session, Attendance).
                var teacher2 = await SeedTeacherAsync(
                    context, hashedPassword, now,
                    fullName: "Mariam Hassan",
                    username: "teacher2",
                    email: "teacher2@edvanz.example",
                    phoneNumber: "01000000002",
                    teacherCode: "T0000002",
                    studentCapacity: 500,
                    createdByUserId: superAdmin.Id);

                await AssignSpecificModulesToTeacherAsync(
                    context, teacher2.Id,
                    moduleNames: new[] { "Student", "Session", "Attendance" });

                // ── 3. Assistants ──
                // Assistant 1A under teacher1: ALL permissions across all 7 modules
                // (the "full delegate"). Verifies the upper bound of the permission graph.
                await SeedAssistantAsync(
                    context, hashedPassword, now,
                    teacherId: teacher1.Id,
                    fullName: "Sara Ibrahim",
                    username: "assistant1a",
                    email: "assistant1a@edvanz.example",
                    phoneNumber: "01000000010",
                    permissionFilter: PermissionFilter.AllPermissions);

                // Assistant 1B under teacher1: a sensible read-mostly subset across
                // Student + Attendance only. Verifies the partial-permission path.
                await SeedAssistantAsync(
                    context, hashedPassword, now,
                    teacherId: teacher1.Id,
                    fullName: "Khaled Nasser",
                    username: "assistant1b",
                    email: "assistant1b@edvanz.example",
                    phoneNumber: "01000000011",
                    permissionFilter: PermissionFilter.PartialReadMostly);

                // Assistant 2A under teacher2: scoped to teacher2's open modules only.
                // Verifies that an assistant cannot have permissions outside the
                // tutor's enabled module set (mirrors AssistantService validation).
                await SeedAssistantAsync(
                    context, hashedPassword, now,
                    teacherId: teacher2.Id,
                    fullName: "Nour Adel",
                    username: "assistant2a",
                    email: "assistant2a@edvanz.example",
                    phoneNumber: "01000000020",
                    permissionFilter: PermissionFilter.WithinTutorModules,
                    tutorModuleNames: new[] { "Student", "Session", "Attendance" });

                // ── 4. Student ──
                await SeedStudentUserAsync(
                    context, hashedPassword, now,
                    fullName: "Youssef Tarek",
                    username: "student1",
                    email: "student1@edvanz.example",
                    phoneNumber: "01000000030",
                    studentAccountCode: "STU000001");

                // ── 5. Parent ──
                await SeedParentUserAsync(
                    context, hashedPassword, now,
                    fullName: "Hany Saleh",
                    username: "parent1",
                    email: "parent1@edvanz.example",
                    phoneNumber: "01000000040");

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ════════════════════════════════════════════════
        // PRIVATE — SUPER ADMIN
        // ════════════════════════════════════════════════

        private static async Task<User> SeedSuperAdminAsync(
            EdvanzDbContext context, string hashedPassword, DateTime now)
        {
            var superAdmin = new User
            {
                UserType = UserType.SuperAdmin,
                FullName = "Platform Administrator",
                Username = "superadmin",
                Email = "admin@edvanz.example",
                PhoneNumber = "01000000000",
                PasswordHashed = hashedPassword,
                IsActive = true,
                IsVerified = true,
                CreateAt = now
            };

            context.Users.Add(superAdmin);
            await context.SaveChangesAsync();
            return superAdmin;
        }

        // ════════════════════════════════════════════════
        // PRIVATE — TEACHER + DEFAULT CONFIGURATION
        // ════════════════════════════════════════════════

        /// <summary>
        /// Creates a User row of type Teacher, the matching Teacher row with the
        /// supplied 8-digit code, and a default TeacherConfiguration mirroring
        /// TeacherService.InitializeTeacherAsync so the teacher is immediately
        /// usable end-to-end.
        /// </summary>
        private static async Task<Teacher> SeedTeacherAsync(
            EdvanzDbContext context,
            string hashedPassword,
            DateTime now,
            string fullName,
            string username,
            string email,
            string phoneNumber,
            string teacherCode,
            int studentCapacity,
            long createdByUserId)
        {
            var user = new User
            {
                UserType = UserType.Teacher,
                FullName = fullName,
                Username = username,
                Email = email,
                PhoneNumber = phoneNumber,
                PasswordHashed = hashedPassword,
                IsActive = true,
                IsVerified = true,
                CreateByUserId = createdByUserId,
                CreateAt = now
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var teacher = new Teacher
            {
                UserId = user.Id,
                TeacherCode = teacherCode,
                StudentCapacity = studentCapacity,
                LanguagePreference = EnglishLanguage,
                AccountStatus = AccountStatus.Active,
                IsConfigurationCompleted = true,
                CreatedByUserId = createdByUserId,
                CreateAt = now
            };
            context.Teachers.Add(teacher);
            await context.SaveChangesAsync();

            // TeacherConfiguration carries sensible defaults via property
            // initializers; we only need to set the FK.
            var configuration = new TeacherConfiguration
            {
                TeacherId = teacher.Id,
                CreateAt = now
            };
            context.TeacherConfigurations.Add(configuration);
            await context.SaveChangesAsync();

            return teacher;
        }

        // ════════════════════════════════════════════════
        // PRIVATE — TEACHER MODULE ASSIGNMENT
        // ════════════════════════════════════════════════

        private static async Task AssignAllModulesToTeacherAsync(
            EdvanzDbContext context, long teacherId)
        {
            var moduleIds = await context.Models.Select(m => m.Id).ToListAsync();
            await AssignModuleIdsToTeacherAsync(context, teacherId, moduleIds);
        }

        private static async Task AssignSpecificModulesToTeacherAsync(
            EdvanzDbContext context, long teacherId, IReadOnlyCollection<string> moduleNames)
        {
            var moduleIds = await context.Models
                .Where(m => moduleNames.Contains(m.Name))
                .Select(m => m.Id)
                .ToListAsync();

            await AssignModuleIdsToTeacherAsync(context, teacherId, moduleIds);
        }

        private static async Task AssignModuleIdsToTeacherAsync(
            EdvanzDbContext context, long teacherId, IReadOnlyCollection<long> moduleIds)
        {
            var rows = moduleIds
                .Select(moduleId => new TutorModule
                {
                    TutorId = teacherId,
                    ModuleId = moduleId
                })
                .ToList();

            context.TutorModuleAccess.AddRange(rows);
            await context.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════
        // PRIVATE — ASSISTANT + PERMISSIONS
        // ════════════════════════════════════════════════

        /// <summary>
        /// Drives the permission set assigned to a seeded assistant.
        /// </summary>
        private enum PermissionFilter
        {
            /// <summary>Every Permission row in the database (full delegate).</summary>
            AllPermissions,

            /// <summary>
            /// A read-mostly subset focused on Student + Attendance modules. Matches
            /// the realistic shape of an assistant who handles attendance only.
            /// </summary>
            PartialReadMostly,

            /// <summary>
            /// Every permission that lives within the tutor's open modules. Mirrors
            /// the constraint enforced by AssistantService.InitializeAssistantAsync.
            /// </summary>
            WithinTutorModules
        }

        private static async Task SeedAssistantAsync(
            EdvanzDbContext context,
            string hashedPassword,
            DateTime now,
            long teacherId,
            string fullName,
            string username,
            string email,
            string phoneNumber,
            PermissionFilter permissionFilter,
            IReadOnlyCollection<string>? tutorModuleNames = null)
        {
            var user = new User
            {
                UserType = UserType.Assistant,
                FullName = fullName,
                Username = username,
                Email = email,
                PhoneNumber = phoneNumber,
                PasswordHashed = hashedPassword,
                IsActive = true,
                IsVerified = true,
                CreateAt = now
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var assistant = new Assistant
            {
                UserId = user.Id,
                TeacherAccountId = teacherId,
                AccountStatus = AccountStatus.Active,
                LanguagePreference = EnglishLanguage,
                CreateAt = now,
                UpdatedAt = now
            };
            context.Assistants.Add(assistant);
            await context.SaveChangesAsync();

            var permissionIds = await ResolvePermissionIdsAsync(
                context, permissionFilter, tutorModuleNames);

            if (permissionIds.Count == 0) return;

            var userPermissions = permissionIds
                .Select(permissionId => new UsersPermission
                {
                    UserId = user.Id,
                    PermissionId = permissionId
                })
                .ToList();

            context.UsersPermissions.AddRange(userPermissions);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Resolves the set of permission ids for an assistant per the chosen filter.
        /// </summary>
        private static async Task<IReadOnlyList<long>> ResolvePermissionIdsAsync(
            EdvanzDbContext context,
            PermissionFilter filter,
            IReadOnlyCollection<string>? tutorModuleNames)
        {
            switch (filter)
            {
                case PermissionFilter.AllPermissions:
                    return await context.Permissions
                        .Select(p => p.Id)
                        .ToListAsync();

                case PermissionFilter.PartialReadMostly:
                    // Hand-picked read-mostly slice. Names match the seeded permissions.
                    var partialKeys = new (string Module, string Permission)[]
                    {
                        ("Student", "ViewList"),
                        ("Student", "ViewProfile"),
                        ("Student", "ViewBarcodes"),
                        ("Attendance", "Take"),
                        ("Attendance", "ViewHistory"),
                        ("Attendance", "ViewAbsenceOverview")
                    };
                    return await GetPermissionIdsByNameTuplesAsync(context, partialKeys);

                case PermissionFilter.WithinTutorModules:
                    if (tutorModuleNames is null || tutorModuleNames.Count == 0)
                        return Array.Empty<long>();

                    return await context.Permissions
                        .Where(p => tutorModuleNames.Contains(p.module.Name))
                        .Select(p => p.Id)
                        .ToListAsync();

                default:
                    return Array.Empty<long>();
            }
        }

        private static async Task<IReadOnlyList<long>> GetPermissionIdsByNameTuplesAsync(
            EdvanzDbContext context,
            IReadOnlyCollection<(string Module, string Permission)> keys)
        {
            // Fetch the small candidate set once, then filter in memory — the
            // (module, permission) tuple has no direct LINQ-to-SQL translation.
            var moduleNames = keys.Select(k => k.Module).Distinct().ToList();

            var candidates = await context.Permissions
                .Include(p => p.module)
                .Where(p => moduleNames.Contains(p.module.Name))
                .ToListAsync();

            var keySet = new HashSet<(string, string)>(
                keys.Select(k => (k.Module, k.Permission)));

            return candidates
                .Where(p => keySet.Contains((p.module.Name, p.Name)))
                .Select(p => p.Id)
                .ToList();
        }

        // ════════════════════════════════════════════════
        // PRIVATE — STUDENT USER
        // ════════════════════════════════════════════════

        private static async Task SeedStudentUserAsync(
            EdvanzDbContext context,
            string hashedPassword,
            DateTime now,
            string fullName,
            string username,
            string email,
            string phoneNumber,
            string studentAccountCode)
        {
            var user = new User
            {
                UserType = UserType.Student,
                FullName = fullName,
                Username = username,
                Email = email,
                PhoneNumber = phoneNumber,
                PasswordHashed = hashedPassword,
                IsActive = true,
                IsVerified = true,
                CreateAt = now
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var studentUser = new StudentUser
            {
                UserId = user.Id,
                StudentAccountCode = studentAccountCode,
                LanguagePreference = EnglishLanguage,
                AccountStatus = AccountStatus.Active,
                IsFirstLogin = true,
                CreateAt = now
            };
            context.StudentUsers.Add(studentUser);
            await context.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════
        // PRIVATE — PARENT USER
        // ════════════════════════════════════════════════

        private static async Task SeedParentUserAsync(
            EdvanzDbContext context,
            string hashedPassword,
            DateTime now,
            string fullName,
            string username,
            string email,
            string phoneNumber)
        {
            var user = new User
            {
                UserType = UserType.Parent,
                FullName = fullName,
                Username = username,
                Email = email,
                PhoneNumber = phoneNumber,
                PasswordHashed = hashedPassword,
                IsActive = true,
                IsVerified = true,
                CreateAt = now
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var parentUser = new ParentUser
            {
                UserId = user.Id,
                LanguagePreference = EnglishLanguage,
                AccountStatus = AccountStatus.Active,
                CreateAt = now
            };
            context.ParentUsers.Add(parentUser);
            await context.SaveChangesAsync();
        }
    }
}