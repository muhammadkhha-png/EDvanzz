using Edvanz.Application.Dtos.Payment;
using Edvanz.Application.Dtos.Session;
using Edvanz.Application.Dtos.TeacherStudent;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Persistence;

/// <summary>
/// DEVELOPMENT-ONLY volume seed for the AssistantWallet screen
/// (GET /api/v1/assistants/{assistantId}/wallet). Partial of <see cref="DbInitializer"/>.
///
/// WHY THIS EXISTS:
///   The baseline seed never creates an AssistantWallet row, and it collects every payment
///   as the teacher — so GetAssistantWalletScreenAsync returns 404 (REQ-PAY-034/035 surface
///   is unreachable). This step provisions the wallet through the service and drives enough
///   collections through CollectPaymentAsync to exercise paging.
///
/// WHY SERVICE-DRIVEN (CLAUDE.md §9):
///   CollectPaymentAsync applies each payment to the earliest unpaid period (BR-PAY-001),
///   recomputes period status, updates StudentPaymentCounter, and increments the collector's
///   AssistantWallet (CurrentBalance / TotalCollected / TransactionCount / LastCollectionAt).
///   Direct inserts cannot reproduce that arithmetic.
///
/// IDEMPOTENCY:
///   Guarded on the collector's transaction count. Re-runs after the first are two SELECTs.
///
/// NEVER call this in Production — it inflates a real tenant's ledger.
/// </summary>
public partial class DbInitializer
{
    // ════════════════════════════════════════════════
    // DEMO SEED CONSTANTS
    // ════════════════════════════════════════════════

    /// <summary>Username of the assistant whose wallet is populated (teacher1's full delegate).</summary>
    private const string WalletDemoAssistantUsername = "assistant1a";

    /// <summary>Name prefix used as the idempotency marker for demo students.</summary>
    private const string WalletDemoStudentNamePrefix = "Demo Student";

    /// <summary>Students created to spread the collections across realistic payers.</summary>
    private const int WalletDemoStudentCount = 20;

    /// <summary>Transactions collected by the assistant. Comfortably above the 100-row minimum.</summary>
    private const int WalletDemoTransactionCount = 120;

    /// <summary>Hours subtracted per row when spreading CollectedAt backwards (120 × 6h ≈ 30 days).</summary>
    private const int WalletDemoHoursBetweenCollections = 6;

    /// <summary>Rotating amounts (EGP). Session amount is 100 → produces full and partial payments.</summary>
    private static readonly decimal[] WalletDemoAmounts = { 100m, 100m, 50m, 75m, 100m, 25m };

    /// <summary>Rotating offline-safe collection methods. Online methods are excluded (they need a transaction ref).</summary>
    private static readonly PaymentCollectionMethod[] WalletDemoMethods =
    {
        PaymentCollectionMethod.ManualCode,
        PaymentCollectionMethod.BarcodeScan
    };

    // ════════════════════════════════════════════════
    // ENTRY POINT
    // ════════════════════════════════════════════════

    /// <summary>
    /// Provisions assistant1a's wallet and drives <see cref="WalletDemoTransactionCount"/>
    /// collections through it. No-op when the wallet already holds enough transactions.
    /// </summary>
    private static async Task SeedAssistantWalletDemoAsync(
        EdvanzDbContext context,
        ITeacherStudentService teacherStudentService,
        ISessionService sessionService,
        IPaymentService paymentService)
    {
        var teacher = await context.Teachers
            .FirstOrDefaultAsync(t => t.TeacherCode == Teacher1Code);
        if (teacher is null) return;

        var assistant = await ResolveDemoAssistantAsync(context, teacher.Id);
        if (assistant is null) return;

        // 1. Wallet provisioning — the missing link that makes the endpoint reachable.
        await paymentService.EnsureAssistantWalletExistsAsync(
            teacher.Id, assistant.Id, assistant.UserId);

        // 2. Idempotency: already seeded?
        int existingCollections = await context.PaymentTransactions
            .CountAsync(t => t.TeacherId == teacher.Id
                          && t.CollectedByUserId == assistant.UserId);
        if (existingCollections >= WalletDemoTransactionCount) return;

        // 3. Students + session assignment (materializes the PaymentPeriods to pay against).
        long? sessionId = await ResolveSeededSessionIdAsync(context, teacher.Id);
        if (sessionId is null) return;

        var studentIds = await EnsureDemoStudentsAsync(
            context, teacherStudentService, sessionService, teacher.Id, sessionId.Value);
        if (studentIds.Count == 0) return;

        // 4. Collections — every one attributed to the assistant, so the wallet accumulates.
        await CollectDemoPaymentsAsync(
            paymentService, teacher.Id, assistant.UserId, sessionId.Value, studentIds);

        // 5. Spread the timestamps so paging is deterministic and the UI looks real.
        await SpreadDemoCollectionTimestampsAsync(context, teacher.Id, assistant.UserId);
    }

    // ════════════════════════════════════════════════
    // RESOLUTION HELPERS
    // ════════════════════════════════════════════════

    /// <summary>
    /// Resolves the demo assistant by username. Uses <c>Set&lt;T&gt;()</c> rather than a named
    /// DbSet property so the seeder stays decoupled from DbContext property naming.
    /// </summary>
    private static async Task<Assistant?> ResolveDemoAssistantAsync(
        EdvanzDbContext context, long teacherId)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Username == WalletDemoAssistantUsername);
        if (user is null) return null;

        return await context.Set<Assistant>()
            .FirstOrDefaultAsync(a => a.TeacherAccountId == teacherId && a.UserId == user.Id);
    }

    /// <summary>
    /// Reads the session id off a student the baseline session seed already assigned.
    /// Avoids duplicating the Session-entity query and the "Session A1" lookup.
    /// </summary>
    private static async Task<long?> ResolveSeededSessionIdAsync(
        EdvanzDbContext context, long teacherId)
    {
        return await context.TeacherStudents
            .Where(ts => ts.TeacherId == teacherId && ts.SessionId != null && !ts.IsDeleted)
            .Select(ts => ts.SessionId)
            .FirstOrDefaultAsync();
    }

    // ════════════════════════════════════════════════
    // STUDENTS
    // ════════════════════════════════════════════════

    /// <summary>
    /// Creates the demo students (idempotent on the name prefix) and assigns any unassigned
    /// ones to the seeded session. Assignment is what generates their PaymentPeriod rows.
    /// </summary>
    private static async Task<List<long>> EnsureDemoStudentsAsync(
        EdvanzDbContext context,
        ITeacherStudentService teacherStudentService,
        ISessionService sessionService,
        long teacherId,
        long sessionId)
    {
        int alreadyCreated = await context.TeacherStudents
            .CountAsync(ts => ts.TeacherId == teacherId
                           && !ts.IsDeleted
                           && ts.StudentName.StartsWith(WalletDemoStudentNamePrefix));

        for (int i = alreadyCreated; i < WalletDemoStudentCount; i++)
        {
            await teacherStudentService.CreateStudentAsync(teacherId, new CreateTeacherStudentDto
            {
                StudentName = $"{WalletDemoStudentNamePrefix} {i + 1:D2}",
                StudentPhoneNumber = $"0111000{i:D4}",
                ParentPhoneNumber = $"0122000{i:D4}"
            });
        }

        var unassigned = await context.TeacherStudents
            .Where(ts => ts.TeacherId == teacherId
                      && !ts.IsDeleted
                      && ts.SessionId == null
                      && ts.StudentName.StartsWith(WalletDemoStudentNamePrefix))
            .Select(ts => ts.Id)
            .ToListAsync();

        if (unassigned.Count > 0)
        {
            await sessionService.AssignStudentsAsync(new AssignStudentsToSessionDto
            {
                TeacherId = teacherId,
                SessionId = sessionId,
                StudentIds = unassigned
            });
        }

        return await context.TeacherStudents
            .Where(ts => ts.TeacherId == teacherId
                      && !ts.IsDeleted
                      && ts.SessionId == sessionId
                      && ts.StudentName.StartsWith(WalletDemoStudentNamePrefix))
            .OrderBy(ts => ts.Id)
            .Select(ts => ts.Id)
            .ToListAsync();
    }

    // ════════════════════════════════════════════════
    // COLLECTIONS
    // ════════════════════════════════════════════════

    /// <summary>
    /// Drives collections round-robin across the demo students, always attributed to the
    /// assistant's user id so <c>UpdateAssistantWalletAfterCollectionAsync</c> finds a wallet.
    ///
    /// Confirm flags are set because the seeder deliberately collects repeatedly from the same
    /// students on the same local date (REQ-PAY-020) and past the point where their earliest
    /// period is settled (REQ-PAY-026). Without them every call after the first returns a
    /// warning result and creates no transaction.
    /// </summary>
    private static async Task CollectDemoPaymentsAsync(
        IPaymentService paymentService,
        long teacherId,
        long assistantUserId,
        long sessionId,
        IReadOnlyList<long> studentIds)
    {
        for (int i = 0; i < WalletDemoTransactionCount; i++)
        {
            await paymentService.CollectPaymentAsync(new CollectPaymentDto
            {
                TeacherId = teacherId,
                TeacherStudentId = studentIds[i % studentIds.Count],
                SessionId = sessionId,
                Amount = WalletDemoAmounts[i % WalletDemoAmounts.Length],
                PaymentMethod = WalletDemoMethods[i % WalletDemoMethods.Length],
                CollectedByUserId = assistantUserId,
                DuplicateConfirmed = true,
                AlreadyPaidConfirmed = true
            });
        }
    }

    // ════════════════════════════════════════════════
    // TIMESTAMP SPREAD (dev-only presentation fix)
    // ════════════════════════════════════════════════

    /// <summary>
    /// DEV-ONLY. CollectPaymentAsync stamps <c>CollectedAt = DateTime.UtcNow</c> and the column
    /// is <c>datetime2(0)</c>, so a seed loop produces ~120 rows sharing one or two second-values.
    /// <c>GetCollectorTransactionsPagedAsync</c> orders by CollectedAt alone, making Skip/Take
    /// non-deterministic across pages.
    ///
    /// This rewrites ONLY the two display timestamps on rows this seeder created. It touches no
    /// monetary field, no period, no counter and no wallet aggregate — every invariant produced
    /// by the service remains exact. It is not row fabrication (CLAUDE.md §9): the rows were
    /// created by CollectPaymentAsync.
    /// </summary>
    private static async Task SpreadDemoCollectionTimestampsAsync(
        EdvanzDbContext context, long teacherId, long assistantUserId)
    {
        var transactions = await context.PaymentTransactions
            .Where(t => t.TeacherId == teacherId && t.CollectedByUserId == assistantUserId)
            .OrderBy(t => t.Id)
            .ToListAsync();

        if (transactions.Count == 0) return;

        DateTime cursor = DateTime.UtcNow;
        foreach (var transaction in transactions.OrderByDescending(t => t.Id))
        {
            transaction.CollectedAt = cursor;
            transaction.LocalCollectedAt = cursor;
            cursor = cursor.AddHours(-WalletDemoHoursBetweenCollections);
        }

        await context.SaveChangesAsync();
    }
}