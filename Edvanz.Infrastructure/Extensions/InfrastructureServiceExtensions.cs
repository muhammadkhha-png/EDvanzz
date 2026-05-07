using Edvanz.Application.IservicesContract;
using Edvanz.Application.Options;
using Edvanz.Application.ServiceContract;
using Edvanz.Application.Services;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.BackGroundJobs;
using Edvanz.Infrastructure.Services;
using FluentAssertions.Common;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using static Azure.Core.HttpHeader;
using System.Net.Http;
namespace Edvanz.Infrastructure.Extensions;

/// <summary>
/// Registers Infrastructure layer services into the DI container.
/// Called from Program.cs alongside AddApplication().
/// </summary>
public static class InfrastructureServiceExtensions
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
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
        // ════════════════════════════════════════════════
        // SUBSCRIPTION MANAGEMENT MODULE — Phase 05 (v1.2)
        // ════════════════════════════════════════════════

        // ── Bind options sections from appsettings.json ──
        services.Configure<PaymobOptions>(configuration.GetSection(PaymobOptions.Section));
        services.Configure<FirebaseOptions>(configuration.GetSection(FirebaseOptions.Section));
        services.Configure<SubscriptionCacheOptions>(configuration.GetSection(SubscriptionCacheOptions.Section));
        services.Configure<ReminderSchedulerOptions>(configuration.GetSection(ReminderSchedulerOptions.Section));
        services.Configure<SubscriptionDefaultsOptions>(configuration.GetSection(SubscriptionDefaultsOptions.Section));

        // ── Payment gateway: stub vs Paymob, selected by PaymobOptions.Enabled ──
        // FR-SUB-034 / D-04: Paymob is stubbed off in v1; the real adapter ships
        // dormant and is activated by flipping configuration only — no code change.
        var paymobEnabled = configuration
            .GetSection(PaymobOptions.Section)
            .GetValue<bool>(nameof(PaymobOptions.Enabled));

        if (paymobEnabled)
        {
            services.AddHttpClient<IPaymentGateway, PaymobPaymentGateway>(client =>
            {
                // Paymob's accept.paymob.com production base. Override per environment via DI later if needed.
                client.BaseAddress = new Uri("https://accept.paymob.com/");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        }
        else
        {
            services.AddScoped<IPaymentGateway, StubPaymentGateway>();
        }

        // ── Firebase push sender (real — there is no stub mode for push) ──
        // The class is safe to register even if FirebaseOptions.CredentialsPath is
        // unset; initialization happens lazily on first SendAsync, and a missing
        // credential surfaces as a clear InvalidOperationException at that point.
        services.AddSingleton<IPushNotificationSender, FirebasePushNotificationSender>();

        // ── Redis subscription-status cache ──
        // Uses StackExchange.Redis when a connection string is configured;
        // falls back to in-memory IDistributedCache when "Redis:Connection" is empty.
        string? redisConnection = configuration["Redis:Connection"];
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(opts =>
            {
                opts.Configuration = redisConnection;
                opts.InstanceName = "edvanz:";
            });
        }
        else
        {
            // Dev fallback. EC-19 still works — IDistributedCache exceptions are caught
            // by RedisSubscriptionCacheService regardless of the underlying provider.
            services.AddDistributedMemoryCache();
        }

        services.AddScoped<ISubscriptionCacheService, RedisSubscriptionCacheService>();
        services.AddScoped<SubscriptionReminderDispatcherJob>();
        services.AddScoped<PendingPaymentExpiryJob>();
        // Exams & Homework Module — report export (stub; replace with ClosedXML/QuestPDF)
        //  Hangfire dispatcher + worker (Phase 6) ──
        // Daily fan-out + per-template materialization workers.
        services.AddScoped<RecurringAssignmentDispatcherJob>();
        services.AddScoped<IRecurringAssignmentMaterializerJob,
                           RecurringAssignmentMaterializerJob>();
        services.AddScoped<IExamHomeworkReportService, ExamHomeworkReportService>();
    }
}