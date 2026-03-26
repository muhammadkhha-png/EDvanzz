using Edvanz.Application.Services;
using Edvanz.Domain.Interfaces;
using Edvanz.Application.ServiceContract;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using System.Globalization;

namespace Edvanz.Application.Extensions;

/// <summary>
/// Registers Application layer services into the DI container.
/// </summary>
public static class ServicesCollectionExtensions
{
    public static void AddApplication(this IServiceCollection services)
    {
        // Teacher module
        services.AddScoped<ITeacherService, TeacherService>();

        // Localization
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = new[] { "en", "ar" };
            options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en");
            options.SupportedCultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
            options.SupportedUICultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
        });
    }
}