using Edvanz.Application.Services;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.ServiceContract;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.AspNetCore.Builder;
using System.Globalization;
using Edvanz.Application.IservicesContract;              // for RequestLocalizationOptions

namespace Edvanz.Application.Extensions;

public static class ServicesCollectionExtensions
{
    public static void AddApplication(this IServiceCollection services)
    {
        #region Services
        services.AddScoped<ITutorService, TutorService>();
        services.AddScoped<ISmsService, SmsService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IuserService, UserService>();

        #endregion
        #region Localization
        // Add Localization
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        // Configure supported cultures
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = new[] { "en", "ar" };
            options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en");
            options.SupportedCultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
            options.SupportedUICultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
        });
        #endregion
    }
}
