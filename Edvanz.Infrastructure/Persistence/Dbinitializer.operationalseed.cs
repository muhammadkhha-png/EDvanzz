using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Persistence;

/// <summary>
/// Operational-baseline seed: master + relationship rows that make the seeded
/// teacher / student / parent accounts usable end-to-end. This is a <c>partial</c>
/// of <see cref="DbInitializer"/> in DbInitializer.cs — it does not replace the
/// existing lookup/identity seed, it extends it.
///
/// SCOPE (deliberate):
///   Seeds MASTER + RELATIONSHIP data only — subscriptions, teacher-scoped student
///   records, and the account links between them. It does NOT fabricate
///   transactional / audit / log rows (payment transactions, attendance records,
///   audit trails, watch events, notifications). Those are produced by exercising
///   the API at runtime; seeding synthetic rows would create referentially
///   inconsistent state (e.g., a PaymentTransaction with no real Paymob order,
///   an AuditTrail row for an action that never happened, counters that must be
///   derived from real events).
///
/// IDEMPOTENCY:
///   Every block guards on <c>.Any(...)</c>; safe to run on every startup. Each
///   seeder re-resolves the accounts created by <c>SeedUsersAsync</c> via their
///   stable natural keys (username / teacher code), so it remains self-contained
///   and does not depend on the user-seed returning entities.
///
/// ONION COMPLIANCE:
///   Infrastructure layer; uses only Domain entities/enums and the EF DbContext.
/// </summary>
public partial class DbInitializer
{
    // Stable natural keys of the baseline accounts created in SeedUsersAsync.
    private const string Teacher1Code = "T0000001";
    private const string Student1Username = "student1";
    private const string Parent1Username = "parent1";

    // ════════════════════════════════════════════════
    // TEACHER SUBSCRIPTIONS (Subscription Management Module)
    // ════════════════════════════════════════════════

    /// <summary>
    /// Gives every seeded teacher a CURRENT 30-day subscription via the super-admin
    /// manual-activation path (REQ-ADM-012 / REQ-SUB-017 — a real, supported flow
    /// that requires no payment gateway). Without a row where <c>IsCurrent = true</c>
    /// and <c>EndDate</c> in the future, the global ActiveSubscriptionRequirement
    /// (§8.1) blocks every authenticated teacher request — so this is mandatory for
    /// the seeded teachers to be usable.
    ///
    /// Status is DERIVED at read time (SubscriptionStatusCalculator), not stored;
    /// a 30-day window from "now" resolves to Active.
    /// BR-SUB-006: exactly one IsCurrent row per teacher — enforced by the filtered
    /// unique index, and respected here because this runs once on an empty table.
    /// </summary>
    private static async Task SeedTeacherSubscriptionsAsync(EdvanzDbContext context)
    {
        if (await context.TeacherSubscriptions.AnyAsync()) return;

        var superAdmin = await context.Users
            .FirstOrDefaultAsync(u => u.UserType == UserType.SuperAdmin);
        if (superAdmin is null) return; // user seed has not run yet

        var teachers = await context.Teachers
            .Where(t => t.AccountStatus == AccountStatus.Active)
            .ToListAsync();
        if (teachers.Count == 0) return;

        var now = DateTime.UtcNow;

        foreach (var teacher in teachers)
        {
            context.TeacherSubscriptions.Add(new TeacherSubscription
            {
                TeacherId = teacher.Id,
                StartDate = now,
                EndDate = now.AddDays(30),                          // D-01: 30 fixed days
                IsCurrent = true,                                   // BR-SUB-006
                PaymentMethod = PaymentMethod.SuperAdminManual,     // see NAME NOTE in chat
                PaymentChannel = PaymentChannel.SuperAdminOverride, // see NAME NOTE in chat
                AmountPaidEGP = 0m,                                 // zero for manual activation
                PaymentConfirmedAt = now,                           // BR-SUB-013: single UtcNow, no drift
                CreatedByUserId = superAdmin.Id,
                CreateAt = now
                // RowVersion is store-generated ([Timestamp]) — never set on insert.
            });
        }

        await context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════
    // TEACHER-SCOPED STUDENT RECORD + ACCOUNT LINKS
    // ════════════════════════════════════════════════

    /// <summary>
    /// Creates a teacher-scoped <see cref="TeacherStudent"/> record under teacher1,
    /// links the seeded <c>student1</c> account to it (<see cref="StudentTeacherLink"/>,
    /// AAM-FR-05.5 / 05.6), and attaches the seeded <c>parent1</c> to that same student
    /// via a Method-A <see cref="ParentChild"/> (AAM-FR-06.3).
    ///
    /// Fixes the gap in <c>SeedUsersAsync</c>: student1 and parent1 are created with
    /// no links, leaving them as orphaned accounts whose dashboards render empty.
    /// </summary>
    private static async Task SeedTeacherStudentAndLinksAsync(EdvanzDbContext context)
    {
        // ── Resolve the accounts created by SeedUsersAsync ──
        var teacher1 = await context.Teachers
            .FirstOrDefaultAsync(t => t.TeacherCode == Teacher1Code);

        var student1User = await context.Users
            .FirstOrDefaultAsync(u => u.Username == Student1Username && u.UserType == UserType.Student);
        var student1 = student1User is null
            ? null
            : await context.StudentUsers.FirstOrDefaultAsync(s => s.UserId == student1User.Id);

        var parent1User = await context.Users
            .FirstOrDefaultAsync(u => u.Username == Parent1Username && u.UserType == UserType.Parent);
        var parent1 = parent1User is null
            ? null
            : await context.ParentUsers.FirstOrDefaultAsync(p => p.UserId == parent1User.Id);

        if (teacher1 is null || student1 is null) return; // nothing to link

        var now = DateTime.UtcNow;

        // ── 1. Teacher-scoped student record (Module 1: Students) ──
        // StudentCode + HashedToken are the credentials a Student User presents to
        // link (AAM-FR-05.5). Seeded deterministically so the link below resolves.
        var teacherStudent = await context.TeacherStudents
            .FirstOrDefaultAsync(ts => ts.TeacherId == teacher1.Id && ts.StudentCode == "A1");

        if (teacherStudent is null)
        {
            teacherStudent = new TeacherStudent
            {
                TeacherId = teacher1.Id,
                StudentName = "Youssef Tarek",
                StudentCode = "A1",                  // REQ-STU-CODE-003: stored uppercase
                HashedToken = "SEED-TS-TOKEN-0001",  // placeholder; runtime uses a crypto token
                StudentPhoneNumber = "01000000030",
                ParentPhoneNumber = "01000000040",
                CreateAt = now
            };
            context.TeacherStudents.Add(teacherStudent);
            await context.SaveChangesAsync();
        }

        // ── 2. Link student1's ACCOUNT to the teacher-scoped record ──
        bool linkExists = await context.StudentTeacherLinks
            .AnyAsync(l => l.StudentUserId == student1.Id && l.TeacherId == teacher1.Id);

        if (!linkExists)
        {
            context.StudentTeacherLinks.Add(new StudentTeacherLink
            {
                StudentUserId = student1.Id,
                TeacherId = teacher1.Id,
                TeacherStudentId = teacherStudent.Id,
                LinkStatus = LinkStatus.Active,
                LinkedAt = now,                       // see NAME NOTE in chat
                CreateAt = now
            });

            // AAM-FR-05.4: the first successful link flips IsFirstLogin off.
            student1.IsFirstLogin = false;
            context.StudentUsers.Update(student1);

            await context.SaveChangesAsync();
        }

        // ── 3. Attach parent1 to student1 (Method A — child has an account) ──
        if (parent1 is not null && student1User is not null)
        {
            bool childExists = await context.ParentChildren
                .AnyAsync(c => c.ParentUserId == parent1.Id && c.StudentUserId == student1.Id);

            if (!childExists)
            {
                context.ParentChildren.Add(new ParentChild
                {
                    ParentUserId = parent1.Id,
                    LinkMethod = ChildLinkMethod.StudentAccount, // see NAME NOTE in chat
                    StudentUserId = student1.Id,
                    ChildName = student1User.FullName,
                    IsActive = true,
                    CreateAt = now
                });
                await context.SaveChangesAsync();
            }
        }
    }
}