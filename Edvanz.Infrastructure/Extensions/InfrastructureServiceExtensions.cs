using Edvanz.Application.ServiceContract;
using Edvanz.Application.Services;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using static Azure.Core.HttpHeader;

namespace Edvanz.Infrastructure.Extensions;

/// <summary>
/// Registers Infrastructure layer services into the DI container.
/// Called from Program.cs alongside AddApplication().
/// </summary>
public static class InfrastructureServiceExtensions
{
    public static void AddInfrastructure(this IServiceCollection services)
    {
        // Teacher code generator (AAM-FR-03.3 / AAM-NFR-03)
        services.AddScoped<ITeacherCodeGenerator, TeacherCodeGenerator>();

        // Student account code generator (AAM-FR-05.3 — StudentUser account code)
        services.AddScoped<IStudentAccountCodeGenerator, StudentAccountCodeGenerator>();

        // Student code generator (REQ-STU-007 — teacher-scoped A1→Z999 sequential codes)
        services.AddScoped<IStudentCodeGenerator, StudentCodeGeneratorService>();

        // Session name generator(REQ-SES - 002 — teacher - scoped Session A1→Z999 sequential names)
        services.AddScoped<ISessionNameGenerator, SessionNameGeneratorService>();

        // Occurrence generator (REQ-ATT-001/002 — computes session occurrence dates from recurrence rules)
        services.AddScoped<IOccurrenceGeneratorService, OccurrenceGeneratorService>();

        // FIX 1.5: Timezone service — provides teacher-local date/time for Egyptian tutors.
        // Resolves the UTC midnight boundary bug where DateTime.UtcNow.Date returns the wrong
        // "today" between midnight and 2 AM Cairo time.
        services.AddScoped<ITimeZoneService, TimeZoneService>();

        // FIX 4.2: Report export service — generates PDF/Excel files for attendance reports.
        // REQ-ATT-041: Reports exportable as PDF or Excel.
        // REQ-ATT-081: Timeline exportable as PDF or Excel.
        // TODO: Uncomment when AttendanceReportExportService is implemented with ClosedXML/QuestPDF.
        services.AddScoped<IAttendanceReportExportService, AttendanceReportExportService>();

        // Payment Module export service — generates PDF/Excel files for payment reports.
        // REQ-PAY-050: Reports exportable as PDF or Excel.
        // REQ-EVT-025: Event reports exportable as PDF or Excel.
        // TODO: Replace stub with ClosedXML/QuestPDF implementation when packages are added.
        services.AddScoped<IPaymentReportExportService, PaymentReportExportService>();
    }
}