using Edvanz.Application.Dtos.Session;
using Edvanz.Application.Dtos.Subscription;
using Edvanz.Application.Dtos.TeacherStudent;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Persistence;

/// <summary>
/// Service-driven seeds for teacher subscriptions, teacher students, and operational sessions.
/// Partial of <see cref="DbInitializer"/>.
///
/// WHY SERVICE-DRIVEN:
///   Each step delegates to the real Application service — the same code path the API
///   uses — so every derived artifact (occurrences, payment periods, absence counters,
///   auto-generated codes, barcodes, hashed tokens) is produced with full business-logic
///   consistency and never drifts from runtime behavior.
///
/// IDEMPOTENCY:
///   Subscriptions: guarded on IsCurrent row existence per teacher.
///   Students:      guarded on any active TeacherStudent row per teacher.
///   Sessions:      guarded on SessionId == null (unassigned student) per teacher.
/// </summary>
public partial class DbInitializer
{
    // ════════════════════════════════════════════════
    // SUBSCRIPTIONS — IAdminSubscriptionService
    // ════════════════════════════════════════════════

    /// <summary>
    /// Activates a 1-year subscription for every teacher that has no current row.
    /// Uses the SuperAdminOverride channel — no payment flow, no cache-miss risk.
    /// </summary>
    private static async Task SeedSubscriptionsAsync(
        EdvanzDbContext context,
        IAdminSubscriptionService adminSubscriptionService)
    {
        var superAdmin = await context.Users
            .FirstOrDefaultAsync(u => u.UserType == UserType.SuperAdmin);
        if (superAdmin is null) return;

        var unseededTeachers = await context.Teachers
            .Where(t => !context.TeacherSubscriptions.Any(ts => ts.TeacherId == t.Id && ts.IsCurrent))
            .ToListAsync();

        if (!unseededTeachers.Any()) return;

        var now = DateTime.UtcNow;
        foreach (var teacher in unseededTeachers)
        {
            await adminSubscriptionService.ActivateAsync(superAdmin.Id, new AdminActivateRequest
            {
                TeacherId = teacher.Id,
                StartDate = now,
                EndDate   = now.AddYears(1)
            });
        }
    }

    // ════════════════════════════════════════════════
    // TEACHER STUDENTS — ITeacherStudentService
    // ════════════════════════════════════════════════

    /// <summary>
    /// Creates one teacher-scoped student under each baseline teacher via the real
    /// student service. The service auto-generates the student code (first student
    /// under each teacher gets "A1"), barcode, and hashed token — no raw inserts.
    /// </summary>
    private static async Task SeedTeacherStudentAndLinksAsync(
        EdvanzDbContext context,
        ITeacherStudentService teacherStudentService)
    {
        var teacher1 = await context.Teachers.FirstOrDefaultAsync(t => t.TeacherCode == Teacher1Code);
        var teacher2 = await context.Teachers.FirstOrDefaultAsync(t => t.TeacherCode == Teacher2Code);
        if (teacher1 is null || teacher2 is null) return;

        if (!await context.TeacherStudents.AnyAsync(ts => ts.TeacherId == teacher1.Id && !ts.IsDeleted))
        {
            await teacherStudentService.CreateStudentAsync(teacher1.Id, new CreateTeacherStudentDto
            {
                StudentName        = "Youssef Tarek",
                StudentPhoneNumber = "01000000031",
                ParentPhoneNumber  = "01000000041"
            });
        }

        if (!await context.TeacherStudents.AnyAsync(ts => ts.TeacherId == teacher2.Id && !ts.IsDeleted))
        {
            await teacherStudentService.CreateStudentAsync(teacher2.Id, new CreateTeacherStudentDto
            {
                StudentName        = "Hana Mahmoud",
                StudentPhoneNumber = "01000000050",
                ParentPhoneNumber  = "01000000060"
            });
        }
    }

    // ════════════════════════════════════════════════
    // OPERATIONAL SESSIONS — ISessionService
    // ════════════════════════════════════════════════

    /// <summary>
    /// Creates one weekly session per teacher (spanning -30d to +30d from today) and
    /// assigns the first unassigned student to it. The assignment triggers:
    ///   CreateSession  → SessionOccurrences
    ///   AssignStudents → StudentSessionAssignment + StudentAbsenceCounter + PaymentPeriods
    /// AttendanceRecords are intentionally NOT seeded — they're created by taking
    /// attendance (POST /mark), ensuring all flows are exercisable end-to-end.
    /// </summary>
    private static async Task SeedOperationalSessionsAsync(
        EdvanzDbContext context,
        ISessionService sessionService)
    {
        var teacher1 = await context.Teachers.FirstOrDefaultAsync(t => t.TeacherCode == Teacher1Code);
        var teacher2 = await context.Teachers.FirstOrDefaultAsync(t => t.TeacherCode == Teacher2Code);
        if (teacher1 is null || teacher2 is null) return;

        var teacher1Student = await context.TeacherStudents
            .FirstOrDefaultAsync(ts => ts.TeacherId == teacher1.Id && !ts.IsDeleted && ts.SessionId == null);

        if (teacher1Student is not null)
            await SeedSessionWithStudentAsync(sessionService, teacher1.Id, Session1Name, teacher1Student.Id);

        var teacher2Student = await context.TeacherStudents
            .FirstOrDefaultAsync(ts => ts.TeacherId == teacher2.Id && !ts.IsDeleted && ts.SessionId == null);

        if (teacher2Student is not null)
            await SeedSessionWithStudentAsync(sessionService, teacher2.Id, Session2Name, teacher2Student.Id);
    }

    private static async Task SeedSessionWithStudentAsync(
        ISessionService sessionService,
        long teacherId,
        string sessionName,
        long teacherStudentId)
    {
        var today = DateTime.UtcNow.Date;

        var createResult = await sessionService.CreateSessionAsync(new CreateSessionDto
        {
            TeacherId       = teacherId,
            SessionName     = sessionName,
            OccurrenceType  = OccurrenceType.Weekly,
            SelectedDays    = new List<int> { 0, 1, 2, 3, 4, 5, 6 },
            PaymentType     = PaymentType.PerSession,
            SessionAmount   = 100m,
            StartDate       = today.AddDays(-30),
            EndDate         = today.AddDays(30),
            StartTime       = new TimeSpan(17, 0, 0),
            DurationMinutes = 90
        });

        if (!createResult.IsSuccess || createResult.Data is null) return;

        await sessionService.AssignStudentsAsync(new AssignStudentsToSessionDto
        {
            TeacherId  = teacherId,
            SessionId  = createResult.Data.Id,
            StudentIds = new List<long> { teacherStudentId }
        });
    }
}
