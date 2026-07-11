using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.ServiceContract;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Persistence;

/// <summary>
/// Database seeder for first-deploy data.
///
/// SEEDING ORDER (FK-safe, service-driven for all transactional tables):
///   1. Modules       — lookup table,    direct insert (no service owns reference data)
///   2. Permissions   — lookup table,    direct insert
///   3. Packages      — lookup table,    direct insert
///   4. Users         — tenant bootstrap, direct insert (IPasswordService for hashing)
///   5. Subscriptions — IAdminSubscriptionService  (service-driven)
///   6. Students      — ITeacherStudentService     (service-driven)
///   7. Sessions      — ISessionService            (service-driven)
///   8. Payments      — IPaymentService            (service-driven)
///
/// IDEMPOTENCY: Every step guards on a row-existence check. Safe on every startup.
///
/// DEFAULT PASSWORD: "Edvanz@2026" — must change on first login (REQ-ADM-003).
/// </summary>
public partial class DbInitializer
{
    // ════════════════════════════════════════════════
    // SEED CONSTANTS
    // ════════════════════════════════════════════════

    /// <summary>First-deploy default password. Super admin must change on first login (REQ-ADM-003).</summary>
    private const string DefaultSeedPassword = "Edvanz@2026";
    private const string EnglishLanguage     = "en";

    // Stable tenant identifiers — never change after first deploy
    private const string Teacher1Code = "T0000001";
    private const string Teacher2Code = "T0000002";
    private const string Teacher3Code = "T0000003";

    // Session name constants used to guard idempotency in session seed
    private const string Session1Name = "Session A1";
    private const string Session2Name = "Session B1";
    private const string Session3Name = "Session C1";

    // ════════════════════════════════════════════════
    // PUBLIC ENTRY POINT
    // ════════════════════════════════════════════════

    /// <param name="includeAssistantWalletDemoData">
    /// DEVELOPMENT ONLY. When true, provisions assistant1a's wallet and drives 120 collections
    /// through it so GET /api/v1/assistants/{assistantId}/wallet is exercisable with paging.
    /// Never enable outside Development — it inflates a real tenant's ledger.
    /// </param>
    public static async Task SeedAsync(
        EdvanzDbContext context,
        IPasswordService passwordService,
        ITeacherStudentService teacherStudentService,
        IAdminSubscriptionService adminSubscriptionService,
        ISessionService sessionService,
        IPaymentService paymentService,

        bool includeAssistantWalletDemoData = true)
    {
        await SeedModulesAsync(context);
        await SeedPermissionsAsync(context);
        await SeedStudentCapacityPackagesAsync(context);
        await SeedUsersAsync(context, passwordService);
        await SeedSubscriptionsAsync(context, adminSubscriptionService);
        await SeedTeacherStudentAndLinksAsync(context, teacherStudentService);
        await SeedOperationalSessionsAsync(context, sessionService);
        await SeedPaymentsAsync(context, paymentService);

        // Must run last: it adds students, and SeedPaymentsAsync selects the *first*
        // session-assigned student of each teacher.
        if (includeAssistantWalletDemoData)
            await SeedAssistantWalletDemoAsync(
                context, teacherStudentService, sessionService, paymentService);
    }
    // ════════════════════════════════════════════════
    // REFERENCE DATA — MODULES
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
            new Module { Name = "Messaging" },
            new Module { Name = "Videos" }
        );

        await context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════
    // REFERENCE DATA — PERMISSIONS
    // ════════════════════════════════════════════════

    private static async Task SeedPermissionsAsync(EdvanzDbContext context)
    {
        if (context.Permissions.Any()) return;

        var modules = await context.Models
            .ToDictionaryAsync(m => m.Name.Trim(), m => m.Id);

        var permissions = new List<Permission>
        {
            // ── Student ──────────────────────────────────────
            new Permission { Name = "ViewList",        ModuleId = modules["Student"], IsRestricted = false },
            new Permission { Name = "ViewProfile",     ModuleId = modules["Student"], IsRestricted = false },
            new Permission { Name = "Add",             ModuleId = modules["Student"], IsRestricted = false },
            new Permission { Name = "Edit",            ModuleId = modules["Student"], IsRestricted = false },
            new Permission { Name = "Delete",          ModuleId = modules["Student"], IsRestricted = false },
            new Permission { Name = "Import",          ModuleId = modules["Student"], IsRestricted = false },
            new Permission { Name = "ExportReports",   ModuleId = modules["Student"], IsRestricted = false },
            new Permission { Name = "ViewBarcodes",    ModuleId = modules["Student"], IsRestricted = false },

            // ── Session ───────────────────────────────────────
            new Permission { Name = "View",              ModuleId = modules["Session"], IsRestricted = false },
            new Permission { Name = "Create",            ModuleId = modules["Session"], IsRestricted = false },
            new Permission { Name = "Edit",              ModuleId = modules["Session"], IsRestricted = false },
            new Permission { Name = "Delete",            ModuleId = modules["Session"], IsRestricted = false },
            new Permission { Name = "ViewGroups",        ModuleId = modules["Session"], IsRestricted = false },
            new Permission { Name = "AssignStudents",    ModuleId = modules["Session"], IsRestricted = false },
            new Permission { Name = "ManageGroups",      ModuleId = modules["Session"], IsRestricted = false },
            new Permission { Name = "ViewMembership",    ModuleId = modules["Session"], IsRestricted = false },
            new Permission { Name = "ManageMembership",  ModuleId = modules["Session"], IsRestricted = false },

            // ── Attendance ────────────────────────────────────
            new Permission { Name = "Take",                 ModuleId = modules["Attendance"], IsRestricted = false },
            new Permission { Name = "Edit",                 ModuleId = modules["Attendance"], IsRestricted = false },
            new Permission { Name = "ViewHistory",          ModuleId = modules["Attendance"], IsRestricted = false },
            new Permission { Name = "ViewAbsenceOverview",  ModuleId = modules["Attendance"], IsRestricted = false },
            new Permission { Name = "GenerateReports",      ModuleId = modules["Attendance"], IsRestricted = false },

            // ── Payment ───────────────────────────────────────
            new Permission { Name = "Collect",              ModuleId = modules["Payment"], IsRestricted = false },
            new Permission { Name = "ViewHistory",          ModuleId = modules["Payment"], IsRestricted = false },
            new Permission { Name = "EditHistory",          ModuleId = modules["Payment"], IsRestricted = true  },
            new Permission { Name = "ViewUnpaidStudents",   ModuleId = modules["Payment"], IsRestricted = false },
            new Permission { Name = "ViewCollectorSummary", ModuleId = modules["Payment"], IsRestricted = false },
            new Permission { Name = "GenerateReports",      ModuleId = modules["Payment"], IsRestricted = false },

            // ── Event-Based Payment ───────────────────────────
            new Permission { Name = "View",            ModuleId = modules["Event-Based Payment"], IsRestricted = false },
            new Permission { Name = "Create",          ModuleId = modules["Event-Based Payment"], IsRestricted = false },
            new Permission { Name = "Edit",            ModuleId = modules["Event-Based Payment"], IsRestricted = false },
            new Permission { Name = "Delete",          ModuleId = modules["Event-Based Payment"], IsRestricted = false },
            new Permission { Name = "CollectPayment",  ModuleId = modules["Event-Based Payment"], IsRestricted = false },
            new Permission { Name = "GenerateReports", ModuleId = modules["Event-Based Payment"], IsRestricted = false },

            // ── Exams & Homework ──────────────────────────────
            new Permission { Name = "View",                           ModuleId = modules["Exams And Homework"], IsRestricted = false },
            new Permission { Name = "Create",                         ModuleId = modules["Exams And Homework"], IsRestricted = false },
            new Permission { Name = "Edit",                           ModuleId = modules["Exams And Homework"], IsRestricted = false },
            new Permission { Name = "Delete",                         ModuleId = modules["Exams And Homework"], IsRestricted = false },
            new Permission { Name = "RecordExamAttendanceAndGrades",  ModuleId = modules["Exams And Homework"], IsRestricted = false },
            new Permission { Name = "RecordHomeworkCompletion",       ModuleId = modules["Exams And Homework"], IsRestricted = false },
            new Permission { Name = "EnterPendingGrades",             ModuleId = modules["Exams And Homework"], IsRestricted = false },
            new Permission { Name = "GenerateReports",                ModuleId = modules["Exams And Homework"], IsRestricted = false },

            // ── Messaging ─────────────────────────────────────
            new Permission { Name = "ViewHistory",               ModuleId = modules["Messaging"], IsRestricted = false },
            new Permission { Name = "SendManual",                ModuleId = modules["Messaging"], IsRestricted = false },
            new Permission { Name = "ManageTemplates",           ModuleId = modules["Messaging"], IsRestricted = false },
            new Permission { Name = "ConfigureAutomatedTriggers",ModuleId = modules["Messaging"], IsRestricted = false },

            // ── Videos ────────────────────────────────────────
            new Permission { Name = "View",         ModuleId = modules["Videos"], IsRestricted = false },
            new Permission { Name = "ManageVideos", ModuleId = modules["Videos"], IsRestricted = false },
        };

        context.Permissions.AddRange(permissions);
        await context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════
    // REFERENCE DATA — STUDENT CAPACITY PACKAGES
    // ════════════════════════════════════════════════

    private static async Task SeedStudentCapacityPackagesAsync(EdvanzDbContext context)
    {
        if (context.StudentCapacityPackages.Any()) return;

        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        context.StudentCapacityPackages.AddRange(
            new StudentCapacityPackage { Name = "Up to 300",    MinStudents = 0,    MaxStudents = 300,  IsActive = true, DisplayOrder = 1, CreateAt = now },
            new StudentCapacityPackage { Name = "300 to 500",   MinStudents = 300,  MaxStudents = 500,  IsActive = true, DisplayOrder = 2, CreateAt = now },
            new StudentCapacityPackage { Name = "500 to 800",   MinStudents = 500,  MaxStudents = 800,  IsActive = true, DisplayOrder = 3, CreateAt = now },
            new StudentCapacityPackage { Name = "800 to 1200",  MinStudents = 800,  MaxStudents = 1200, IsActive = true, DisplayOrder = 4, CreateAt = now },
            new StudentCapacityPackage { Name = "1200 to 1500", MinStudents = 1200, MaxStudents = 1500, IsActive = true, DisplayOrder = 5, CreateAt = now },
            new StudentCapacityPackage { Name = "1500 to 3000", MinStudents = 1500, MaxStudents = 3000, IsActive = true, DisplayOrder = 6, CreateAt = now },
            new StudentCapacityPackage { Name = "3000+",        MinStudents = 3000, MaxStudents = null, IsActive = true, DisplayOrder = 7, CreateAt = now }
        );

        await context.SaveChangesAsync();
    }
}
