using Edvanz.Application.Dtos.Session;
using Edvanz.Application.Dtos.TeacherStudent;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Edvanz.Infrastructure.Persistence;

/// <summary>
/// DEMO / TEST DATA seed — partial of <see cref="DbInitializer"/>.
///
/// PHILOSOPHY (the "attendance way"):
///   This seeder NEVER inserts transactional rows directly. It drives the
///   Application service layer exactly as the API does, so every derived
///   artifact is produced by the same code path as production:
///     CreateStudentAsync  → TeacherStudent (+ auto code, barcode, token)
///     CreateSessionAsync  → Session       (+ SessionOccurrences via AttendanceService)
///     AssignStudentsAsync → StudentSessionAssignment + StudentAbsenceCounter
///                           + PaymentPeriod rows + StudentPaymentCounter
///   No counter is ever hand-written; all are computed by the services. This
///   guarantees referential and arithmetic consistency that direct inserts
///   cannot.
///
/// SCOPE (Phase 1):
///   Students, sessions, and assignment for the baseline teacher (T0000001).
///   Later phases (attendance taking, payments, exams, messaging, videos) extend
///   this same partial and reuse the entities created here.
///
/// IDEMPOTENCY:
///   Guards on "does the baseline teacher already have any session?". Safe to run
///   on every startup; once the demo set exists it no-ops.
///
/// GATING:
///   Intended to run in non-production environments only — see the caller in
///   ApplicationBuilderExtensions. Fabricated sessions/attendance must never land
///   in a real tenant's data.
///
/// ONION COMPLIANCE:
///   Infrastructure layer resolving Application abstractions from the DI scope —
///   Infrastructure already depends on Application, so this is legal. No raw EF
///   writes to domain tables; only read-side EF for resolution and idempotency.
/// </summary>
public partial class DbInitializer
{
   
}