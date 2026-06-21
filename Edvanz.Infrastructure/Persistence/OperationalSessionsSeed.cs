using Edvanz.Application.Dtos.Session;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Persistence;

/// <summary>
/// Operational session seed: gives the baseline teachers a ready-to-use session with
/// an enrolled student, so the deployed app is immediately exercisable end-to-end
/// (dashboard, take attendance, edit, reports, absence overview, timeline) the moment
/// it boots. Partial of <see cref="DbInitializer"/>.
///
/// WHY SERVICE-DRIVEN (not hand-inserted rows):
///   A usable session is the product of two real flows — CreateSession (which
///   materializes SessionOccurrences from the recurrence) and AssignStudents (which
///   creates the StudentSessionAssignment, inits the StudentAbsenceCounter, AND
///   generates payment periods). Calling ISessionService reuses that exact logic, so
///   occurrence / assignment / counter / payment state stay consistent and never drift
///   from runtime behavior. AttendanceRecords are intentionally NOT seeded — they're
///   created by taking attendance (POST /mark).
///
/// IDEMPOTENCY:
///   Guards on the assigned student's SessionId and on TeacherStudent code, so re-runs
///   are no-ops and never hit the unique session-name rule (BR-SES-001).
///
/// SESSION SHAPE:
///   Weekly with all 7 weekdays selected, spanning now-30d .. now+30d, so there is
///   always an occurrence for "today" whenever the app starts — which the Take-Attendance
///   / "Today's Session" flows require.
/// </summary>
public partial class DbInitializer
{
    private const string Teacher2Code = "T0000002";
    private const string Session1Name = "Session A1";
    private const string Session2Name = "Session B1";
    private const string Teacher2StudentCode = "B1";

    private static async Task SeedOperationalSessionsAsync(
        EdvanzDbContext context,
        ISessionService sessionService)
    {
        var teacher1 = await context.Teachers
            .FirstOrDefaultAsync(t => t.TeacherCode == Teacher1Code);
        var teacher2 = await context.Teachers
            .FirstOrDefaultAsync(t => t.TeacherCode == Teacher2Code);

        if (teacher1 is null || teacher2 is null) return; // user seed hasn't run yet

        // ── teacher1: reuse the seeded TeacherStudent "A1" (student1's record) ──
        var teacher1Student = await context.TeacherStudents
            .FirstOrDefaultAsync(ts => ts.TeacherId == teacher1.Id && ts.StudentCode == "A1");

        if (teacher1Student is not null && teacher1Student.SessionId is null)
        {
            await SeedSessionWithStudentAsync(
                sessionService, teacher1.Id, Session1Name, teacher1Student.Id);
        }

        // ── teacher2: needs its own teacher-scoped student (no account required) ──
        var teacher2Student = await EnsureTeacher2StudentAsync(context, teacher2.Id);
        if (teacher2Student.SessionId is null)
        {
            await SeedSessionWithStudentAsync(
                sessionService, teacher2.Id, Session2Name, teacher2Student.Id);
        }
    }

    /// <summary>
    /// Hand-seeds a teacher-scoped student under teacher2 (mirrors how "A1" is seeded
    /// for teacher1). No StudentUser account is needed — teachers take attendance against
    /// TeacherStudent rows. Idempotent on (TeacherId + StudentCode).
    /// </summary>
    private static async Task<TeacherStudent> EnsureTeacher2StudentAsync(
        EdvanzDbContext context, long teacher2Id)
    {
        var existing = await context.TeacherStudents
            .FirstOrDefaultAsync(ts => ts.TeacherId == teacher2Id && ts.StudentCode == Teacher2StudentCode);
        if (existing is not null) return existing;

        var student = new TeacherStudent
        {
            TeacherId = teacher2Id,
            StudentName = "Hana Mahmoud",
            StudentCode = Teacher2StudentCode,      // stored uppercase
            HashedToken = "SEED-TS-TOKEN-0002",     // placeholder; runtime uses a crypto token
            StudentPhoneNumber = "01000000050",
            ParentPhoneNumber = "01000000060",
            CreateAt = DateTime.UtcNow
        };
        context.TeacherStudents.Add(student);
        await context.SaveChangesAsync();
        return student;
    }

    /// <summary>
    /// Creates one operational session for a teacher and assigns the given student to it,
    /// via the real ISessionService flows (occurrences + assignment + counter + payment
    /// periods all created exactly as runtime does).
    /// </summary>
    private static async Task SeedSessionWithStudentAsync(
        ISessionService sessionService,
        long teacherId,
        string sessionName,
        long teacherStudentId)
    {
        var today = DateTime.UtcNow.Date;

        var createResult = await sessionService.CreateSessionAsync(new CreateSessionDto
        {
            TeacherId = teacherId,
            SessionName = sessionName,                              // explicit → skips auto-name generator
            OccurrenceType = OccurrenceType.Weekly,
            SelectedDays = new List<int> { 0, 1, 2, 3, 4, 5, 6 },   // all days → "today" always occurs
            PaymentType = PaymentType.PerSession,
            SessionAmount = 100m,
            StartDate = today.AddDays(-30),
            EndDate = today.AddDays(30),
            StartTime = new TimeSpan(17, 0, 0),
            DurationMinutes = 90
        });

        if (!createResult.IsSuccess || createResult.Data is null) return;

        await sessionService.AssignStudentsAsync(new AssignStudentsToSessionDto
        {
            TeacherId = teacherId,
            SessionId = createResult.Data.Id,
            StudentIds = new List<long> { teacherStudentId }
        });
    }
}