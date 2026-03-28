using Edvanz.Application.Services;
using Edvanz.Domain.Interfaces;
using Edvanz.Application.ServiceContract;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using System.Globalization;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.ServiceContract;

namespace Edvanz.Application.Extensions;

/// <summary>
/// Registers Application layer services into the DI container.
/// </summary>
public static class ServicesCollectionExtensions
{
    public static void AddApplication(this IServiceCollection services)
    {

        #region Services
        services.AddScoped<ISmsService, SmsService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IuserService, UserService>();

        #endregion
       
        // Add Localization
       

        services.AddScoped<ITeacherService, TeacherService>();

        services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = new[] { "en", "ar" };
            options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en");
            options.SupportedCultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
            options.SupportedUICultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();

            options.RequestCultureProviders = new List<Microsoft.AspNetCore.Localization.IRequestCultureProvider>
        {
            new Microsoft.AspNetCore.Localization.AcceptLanguageHeaderRequestCultureProvider()
        };
        });
    }
}