using Edvanz.Application.ServiceContract;   // ← add (ISessionService)
using Edvanz.Domain.ServiceContract;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Edvanz.Infrastructure.Persistence
{
    public static class ApplicationBuilderExtensions
    {
        public static async Task SeedDatabaseAsync(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<EdvanzDbContext>();
            var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
            var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();

            await context.Database.MigrateAsync();

            await DbInitializer.SeedAsync(context, passwordService, sessionService);
        }
    }
}