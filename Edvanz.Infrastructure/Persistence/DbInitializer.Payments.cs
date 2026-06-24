using Edvanz.Application.Dtos.Payment;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Persistence;

/// <summary>
/// Service-driven payment seed: collects one payment per teacher's enrolled student
/// so the payment history, collector summary, and dashboard are immediately exercisable
/// after deploy. Partial of <see cref="DbInitializer"/>.
///
/// WHY SERVICE-DRIVEN:
///   CollectPaymentAsync assigns the payment to the correct open period (BR-PAY-001),
///   increments the StudentPaymentCounter, and handles pro-rate logic (BR-PAY-005).
///   Direct inserts cannot reproduce this arithmetic correctly.
///
/// IDEMPOTENCY:
///   Guarded on PaymentTransaction existence per student. Re-runs are no-ops once
///   the first transaction row exists.
///
/// PAYMENT SHAPE:
///   ManualCode method, 100 EGP, collected by the teacher's own user account.
///   Matches the session amount seeded in SeedOperationalSessionsAsync.
/// </summary>
public partial class DbInitializer
{
    private static async Task SeedPaymentsAsync(
        EdvanzDbContext context,
        IPaymentService paymentService)
    {
        var teachers = await context.Teachers
            .Where(t => t.TeacherCode == Teacher1Code || t.TeacherCode == Teacher2Code)
            .ToListAsync();

        foreach (var teacher in teachers)
        {
            // Find the student that was assigned to a session in the session seed step
            var student = await context.TeacherStudents
                .FirstOrDefaultAsync(ts =>
                    ts.TeacherId == teacher.Id &&
                    ts.SessionId != null &&
                    !ts.IsDeleted);

            if (student?.SessionId is null) continue;

            bool alreadyPaid = await context.PaymentTransactions
                .AnyAsync(pt => pt.TeacherStudentId == student.Id && !pt.IsDeleted);
            if (alreadyPaid) continue;

            await paymentService.CollectPaymentAsync(new CollectPaymentDto
            {
                TeacherId        = teacher.Id,
                TeacherStudentId = student.Id,
                SessionId        = student.SessionId.Value,
                Amount           = 100m,
                PaymentMethod    = PaymentCollectionMethod.ManualCode,
                CollectedByUserId = teacher.UserId
            });
        }
    }
}
