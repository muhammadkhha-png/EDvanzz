using DocumentFormat.OpenXml.Wordprocessing;
using Edvanz.API.Authorization;
using Edvanz.API.Controllers;
using Edvanz.API.Filters;
using Edvanz.API.Middleware;
using Edvanz.Application.Extensions;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.Options;
using Edvanz.Application.Security;
using Edvanz.Application.ServiceContract;
using Edvanz.Application.Services;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure;
using Edvanz.Infrastructure.BackGroundJobs;
using Edvanz.Infrastructure.Extensions;
using Edvanz.Infrastructure.Persistence;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using HangfireDashboardAuthFilter = Edvanz.API.Filters.HangfireDashboardAuthFilter;

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
builder.Services.AddDbContext<EdvanzDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("con"),
        sqlOpts => sqlOpts
            .EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null)   // null = use EF's default transient-error list
            .CommandTimeout(30)));
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
    options.AddPolicy("Edvanz", policy =>
    {
        // Origins are configured per-environment in appsettings.Production.json
        // (Cors:Origins array). Flutter mobile clients are not browser-bound
        // so they don't need CORS — this list is for any web admin surface only.
        var origins = builder.Configuration
            .GetSection("Cors:Origins")
            .Get<string[]>() ?? Array.Empty<string>();

        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddSwaggerGen(c =>
{
    // JWT Bearer Authentication
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token. Example: \"Bearer 12345abcdef\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    //c.AddSecurityRequirement(new OpenApiSecurityRequirement
    //{
    //    {
    //        new OpenApiSecurityScheme
    //        {
    //            Reference = new OpenApiReference // <-- Use fully qualified name if needed
    //            {
    //                Type = ReferenceType.SecurityScheme,
    //                Id = "Bearer"
    //            }
    //        },
    //        new List<string>()
    //    }
    //});
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Edvanz", Version = "v1" });
    c.OperationFilter<AcceptLanguageHeaderFilter>();
    c.OperationFilter<SwaggerExamplesFilter>();
    c.UseInlineDefinitionsForEnums();

    // --- 👇 Add these lines ---
    var basePath = AppContext.BaseDirectory;

    // 1) XML from the API project (controllers)
    var apiXml = $"{typeof(AttendanceController).Assembly.GetName().Name}.xml";
    var apiPath = Path.Combine(basePath, apiXml);
    if (File.Exists(apiPath))
        c.IncludeXmlComments(apiPath);

    // 2) XML from the Application project (DTOs) – adjust assembly name if needed
    var appXml = "Edvanz.Application.xml";
    var appPath = Path.Combine(basePath, appXml);
    if (File.Exists(appPath))
        c.IncludeXmlComments(appPath);
});
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
// ── Hangfire — production-tuned for Azure SQL Basic (5 DTU) ──────────────
// QueuePollInterval at 15s: the default is already 15s in Hangfire 1.8;
// we pin it explicitly so a library upgrade never silently lowers it.
// DisableGlobalLocks: reduces lock contention on Azure SQL — recommended
// for cloud-hosted SQL where distributed locks incur extra round-trips.
// WorkerCount = 4: on a 1-vCPU B1 the default (ProcessorCount × 5 ≈ 5-10)
// competes with the API for the same core and the same 5 DTU.
builder.Services.AddHangfire(config =>
    config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(
            builder.Configuration.GetConnectionString("con"),
            new SqlServerStorageOptions
            {
                QueuePollInterval = TimeSpan.FromSeconds(15),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                JobExpirationCheckInterval = TimeSpan.FromHours(1),
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true,
                SchemaName = "HangFire"
            }));

builder.Services.AddHangfireServer(options =>
{
    options.Queues = new[]
    {
        "default",
        SubscriptionConstants.NotificationsQueue,
        "assignment-materialization"
    };
    options.WorkerCount = 4;
    options.ServerName = $"edvanz-{Environment.MachineName}";
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,
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
builder.Services.AddSingleton<IVideoUrlParser, VideoUrlParser>();
builder.Services.AddScoped<IVideoScopeResolver, VideoScopeResolver>();
builder.Services.AddScoped<IVideoService, VideoService>();
builder.Services.AddHttpContextAccessor();
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddHttpClient<IWhatsAppSender, WhatsAppSender>();

// ── Rate limiting (built-in, no Redis needed for single instance) ─────────
// "auth" policy: 10 login/register attempts per IP per minute.
// Applied via [EnableRateLimiting("auth")] on AuthController.
builder.Services.AddRateLimiter(o =>
{
    // General auth surface: login, sign-up, OTP, Google sign-up, change-password.
    o.AddFixedWindowLimiter("auth", opts =>
    {
        opts.Window = TimeSpan.FromMinutes(1);
        opts.PermitLimit = 10;
        opts.QueueLimit = 0;
        opts.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
    });

    // Admin login: stricter — admin credentials are the highest-value target.
    o.AddFixedWindowLimiter("admin-auth", opts =>
    {
        opts.Window = TimeSpan.FromMinutes(1);
        opts.PermitLimit = 5;
        opts.QueueLimit = 0;
        opts.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
    });

    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── Health checks ─────────────────────────────────────────────────────────
// /health/live  — always 200 if the process is up (no dependencies checked)
// /health/ready — checks SQL Server connectivity; tagged "ready"
builder.Services.AddHealthChecks()
    .AddAsyncCheck("sqldb", async ct =>
    {
        try
        {
            var cs = builder.Configuration.GetConnectionString("con")!;
            await using var conn = new Microsoft.Data.SqlClient.SqlConnection(cs);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(ct);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server unreachable", ex);
        }
    }, tags: new[] { "ready" })
    .AddCheck("hangfire", () =>
        JobStorage.Current?.GetMonitoringApi().Servers().Count > 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Hangfire server not running"),
        tags: new[] { "ready" });
   
var app = builder.Build();


await app.SeedDatabaseAsync();

// Use localization middleware
var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);
app.UseMiddleware<ExceptionMiddleware>();
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Production: only serve the raw JSON, or add authentication
    app.UseSwagger();   // still serves /swagger/v1/swagger.json
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Edvanz v1");
        // Protect the UI with a custom middleware if needed
    });
}
// ?? Subscription Module — Phase 08 recurring registrations ??

// Daily reminder dispatcher (§7.1). 09:00 in Africa/Cairo by default.
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
// assistant-cleanup runs at 01:00 Africa/Cairo — off-peak, avoids DTU
// contention with the 06:00 materializer and 09:00 reminder dispatcher.
RecurringJob.AddOrUpdate<AssistantCleanupJob>(
    "assistant-cleanup-job",
    job => job.ExecuteAsync(),
    "0 1 * * *",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo")
    });
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

// HSTS — tells browsers to only use HTTPS for the next year.
// Excluded in Development so localhost still works over HTTP.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Security headers — defence-in-depth for any browser-based surface.
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    ctx.Response.Headers.Append("X-Frame-Options", "DENY");
    ctx.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    ctx.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

app.UseCors("Edvanz");

// Rate limiting — applied before auth so unauthenticated callers are
// throttled on the login/register surface. Policy "auth" is registered
// in AddRateLimiter above and applied via [EnableRateLimiting("auth")]
// on AuthController endpoints.
app.UseRateLimiter();

app.UseAuthentication();

// Live permission / module-revocation enforcement (REQ-USR-013 / REQ-USR-027 /
// REQ-USR-008 / BR-ADM-010). Runs after UseAuthentication so HttpContext.User
// is populated, and before UseAuthorization so PermissionHandler and
// ActiveSubscriptionHandler see the resolved snapshot on HttpContext.Items.
app.UseMiddleware<SecurityStampValidationMiddleware>();
app.UseAuthorization();

// ── Hangfire dashboard — MUST be after UseAuthentication + UseAuthorization ──
// Placing it here ensures HttpContext.User is fully populated by the time
// HangfireDashboardAuthFilter.Authorize() runs. A SuperAdmin-role JWT is
// required; an IP restriction on /hangfire in App Service Access Restrictions
// adds a belt-and-braces defence at the network layer.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthFilter() },
    IsReadOnlyFunc = _ => false
});

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false   // process-up only — no dependency checks
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                error = e.Value.Exception?.Message
            })
        });
        await ctx.Response.WriteAsync(result);
    }
});

app.Run();

