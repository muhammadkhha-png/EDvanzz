//using Microsoft.AspNetCore.Builder;
//using Microsoft.Extensions.DependencyInjection;
//using System;
//using System.Collections.Generic;
//using System.Text;

//namespace Edvanz.Infrastructure.Persistence
//{
//    public static class ApplicationBuilderExtensions
//    {
//        public static async Task SeedDatabaseAsync(this IApplicationBuilder app)
//        {
//            using var scope = app.ApplicationServices.CreateScope();

//            var context = scope.ServiceProvider.GetRequiredService<EdvanzDbContext>();

//            await DbInitializer.SeedAsync(context);
//        }
//    }
//}
using Edvanz.Domain.ServiceContract;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Edvanz.Infrastructure.Persistence
{
    /// <summary>
    /// Database-bootstrap extensions invoked during application startup.
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Resolves the DbContext and the password-hashing service from the DI scope
        /// and runs <see cref="DbInitializer.SeedAsync"/>. Idempotent — safe to invoke
        /// on every startup.
        /// </summary>
        public static async Task SeedDatabaseAsync(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<EdvanzDbContext>();
            var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

            await context.Database.MigrateAsync();   // create/upgrade schema before any query

            await DbInitializer.SeedAsync(context, passwordService);
        }
    }
}