using Edvanz.Application.ServiceContract;
using Edvanz.Domain.ServiceContract;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Edvanz.Infrastructure.Persistence;

public static class ApplicationBuilderExtensions
{
    public static async Task SeedDatabaseAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<EdvanzDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var teacherStudentService = scope.ServiceProvider.GetRequiredService<ITeacherStudentService>();
        var adminSubscriptionService = scope.ServiceProvider.GetRequiredService<IAdminSubscriptionService>();
        var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

        // REMOVED: context.Database.MigrateAsync()
        // Migrations are applied by the CI/CD pipeline (dotnet ef database update)
        // BEFORE the new app version starts. Running MigrateAsync at boot on
        // Azure SQL Basic (5 DTU) creates a cold-start race: every instance restart
        // hits 5 DTU with a schema-diff check, and any future multi-instance
        // scenario produces a migration race condition.
        // The seeder guards on row-existence — it is safe to call on every boot
        // without the migrate step.

        await DbInitializer.SeedAsync(
            context,
            passwordService,
            teacherStudentService,
            adminSubscriptionService,
            sessionService,
            paymentService);
    }
}
