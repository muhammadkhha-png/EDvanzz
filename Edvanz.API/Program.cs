using Edvanz.API.Authorization;
using Edvanz.API.Filters;
using Edvanz.Application.Extensions;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Options;
using Edvanz.Application.Security;
using Edvanz.Application.Services;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure;
using Edvanz.Infrastructure.BackGroundJobs;
using Edvanz.Infrastructure.Extensions;
using Edvanz.Infrastructure.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
var x = builder.Configuration.GetConnectionString("con");
builder.Services.AddDbContext<EdvanzDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("con")));
builder.Services.AddScoped(typeof(IUnitOfWork), typeof(UnitOfWork));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddLocalization();

builder.Services.Configure<RequestLocalizationOptions>(options =>
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
builder.Services.AddHttpContextAccessor();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });


});
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Edvanz", Version = "v1" });
    c.OperationFilter<AcceptLanguageHeaderFilter>();
    c.UseInlineDefinitionsForEnums();
});
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
builder.Services.AddHangfire(config =>
{
    config.UseSqlServerStorage(
        builder.Configuration.GetConnectionString("con"));
});

// Process BOTH the default queue AND the notifications queue (§7.5).
// Worker counts: default 5 workers on each queue. Tune via configuration if needed.
builder.Services.AddHangfireServer(options =>
{
    options.Queues = new[] { "default", SubscriptionConstants.NotificationsQueue,"assignment-materialization" };
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("SUPER_SECRET_KEY_EDVanz_edvanzz_OMRANBELAL")),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization(
options =>
{
    // Existing named policies preserved here (if any).
    // ??? Subscription Management ???????????????????????
    // Apply the active-subscription requirement to ALL authenticated endpoints.
    // Endpoints opt out via [AllowExpiredSubscription].
    options.AddPolicy("ActiveSubscription", policy =>
        policy.Requirements.Add(new ActiveSubscriptionRequirement()));

    // Make it the fallback so every controller protected by [Authorize] inherits it
    // without explicitly listing the policy name.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddRequirements(new ActiveSubscriptionRequirement())
        .Build();
});
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
// ????????????????????????????????????????????????
// SUBSCRIPTION MANAGEMENT MODULE — Phase 07 (Authorization)
// ????????????????????????????????????????????????
//
// The ActiveSubscriptionHandler enforces the §8 policy: every authenticated
// request requires an Active or ExpiringSoon subscription unless the endpoint
// carries [AllowExpiredSubscription].
//
// Singleton is the right scope here — the handler is stateless; per-request
// dependencies (ICurrentUserService, IUnitOfWork) are injected fresh on each
// HandleRequirementAsync call via the request-scoped scope.
// (Note: ASP.NET Core resolves IAuthorizationHandler instances per request
// when registered as Scoped. Use Scoped to match IUnitOfWork's lifetime.)
builder.Services.AddScoped<IAuthorizationHandler, ActiveSubscriptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddHttpClient<IWhatsAppSender, WhatsAppSender>();
var app = builder.Build();
await app.SeedDatabaseAsync();

// Use localization middleware
var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);
app.UseMiddleware<ExceptionMiddleware>();

app.UseSwagger();
    app.UseSwaggerUI(
        c =>
        {
            c.SwaggerEndpoint("v1/swagger.json", "Edvanz v1");
        });
app.UseHangfireDashboard("/hangfire");
// ?? Subscription Module — Phase 08 recurring registrations ??

// Daily reminder dispatcher (§7.1). 09:00 in Africa/Cairo by default.
// CronExpression and TimeZoneId are bound from ReminderSchedulerOptions.
{
    var reminderOpts = app.Services
        .GetRequiredService<IOptions<ReminderSchedulerOptions>>().Value;

    TimeZoneInfo timeZone;
    try
    {
        timeZone = TimeZoneInfo.FindSystemTimeZoneById(reminderOpts.TimeZoneId);
    }
    catch (TimeZoneNotFoundException)
    {
        timeZone = TimeZoneInfo.Utc;
    }

    RecurringJob.AddOrUpdate<SubscriptionReminderDispatcherJob>(
        SubscriptionConstants.ReminderDispatcherJobId,
        job => job.RunAsync(),
        reminderOpts.CronExpression,
        new RecurringJobOptions { TimeZone = timeZone });
}

// Hourly pending-payment expiry sweep (EC-18). Runs at minute 0 of every hour.
RecurringJob.AddOrUpdate<PendingPaymentExpiryJob>(
    SubscriptionConstants.PendingPaymentExpiryJobId,
    job => job.RunAsync(),
    Cron.Hourly);
RecurringJob.AddOrUpdate<AssistantCleanupJob>(
    "assistant-cleanup-job",
    job => job.ExecuteAsync(),
   Cron.Daily);
// ?? Recurring Assignment Materializer (Module 6) ??
// Runs once daily at 06:00 Africa/Cairo. Earlier than the reminder dispatcher
// (09:00) so tomorrow's occurrences are visible by morning.
RecurringJob.AddOrUpdate<RecurringAssignmentDispatcherJob>(
    recurringJobId: "recurring-assignment-materializer",
    methodCall: job => job.RunAsync(),
    cronExpression: "0 6 * * *",
    options: new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"),
    }
  );
    
app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication(); 

app.UseAuthorization();

app.MapControllers();

app.Run();

using (var scope = app.Services.CreateScope())
{
    var backgroundJobClient = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
    backgroundJobClient.Schedule<RecurringAssignmentDispatcherJob>(
        job => job.RunAsync(),
        TimeSpan.FromMinutes(1));
}