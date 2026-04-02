using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Application.Services;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.ServiceContract;
using Edvanz.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace Edvanz.Application.Extensions;

/// <summary>
/// Registers Application layer services into the DI container.
/// All services are registered as Scoped (one instance per HTTP request).
/// </summary>
public static class ServicesCollectionExtensions
{
    /// <summary>
    /// Adds all Application layer services to the dependency injection container.
    /// Includes: authentication services, user module services, type-specific services,
    /// student module service, and localization configuration.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    public static void AddApplication(this IServiceCollection services)
    {

        #region Services
        services.AddScoped<ISmsService, SmsService>();
        // FIX B5: AuthService now requires IPasswordService in its constructor
        // (previously it was missing, causing the plain-text vs hash comparison bug)
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // FIX I1/I3: Renamed from IuserService to IUserService
        // FIX I1: Interface moved from Edvanz.Domain.ServiceContract to Edvanz.Application.ServiceContract
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IStudentUserService, StudentUserService>();
        services.AddScoped<IParentUserService, ParentUserService>();
        services.AddScoped<ITokenService, TokenService>();
        // Student Module (Module 1: teacher-scoped student records CRUD)
        services.AddScoped<ITeacherStudentService, TeacherStudentService>();
        services.AddScoped<ITeacherService, TeacherService>();
        // Session Module (Module 2: sessions, groups, membership links)
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IAssistantService, AssistantService>();
        services.AddScoped<IUserPermissionService,UserPermissionService>();
        services.AddScoped<IMessagingIntegrationService, StubMessagingIntegrationService>();

        #endregion





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