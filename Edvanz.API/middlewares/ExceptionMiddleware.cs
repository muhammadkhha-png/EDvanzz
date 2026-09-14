using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.exceptions;
using Edvanz.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Net;
using System.Text.Json;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IStringLocalizer<Edvanz.Domain.Resources.Messages> _localizer;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, IStringLocalizer<Edvanz.Domain.Resources.Messages> localizer,
        ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}{QueryString}",
                context.Request.Method, context.Request.Path, context.Request.QueryString);
            await HandleExceptionAsync(context, ex);
        }
    }

    // ── SQL Server error numbers ────────────────────────────────────────────────────────
    //
    // A DbUpdateException says only "the write failed". WHY it failed decides whether the
    // caller may retry, and the offline clients act on that: both op transports treat a 409
    // as a TERMINAL business answer (nothing was written, stop trying), while a 5xx is
    // retried with a reconcile first. Mapping every DbUpdateException to 409 therefore turned
    // a deadlock, a command timeout or a dropped connection — none of which say anything
    // about whether the row landed — into "the server refused this, give up", and a queued
    // attendance mark or a collected payment was discarded on the strength of it.
    //
    // 2601 duplicate key (unique INDEX), 2627 unique/PK CONSTRAINT, 547 FK/CHECK constraint:
    // the database has a permanent, meaningful objection. Retrying the identical write cannot
    // succeed, and the caller is entitled to be told "conflict".
    private static readonly int[] PermanentConflictSqlErrors = [2601, 2627, 547];

    // Transient infrastructure faults. Every one of these means the write may or may not have
    // committed, so the answer must invite a retry (and the clients reconcile before resending).
    // 1205 deadlock victim · -2 command timeout · 121/233/64/10053/10054/10060/11001 transport ·
    // 1222 lock request timeout · 4060/4221/40197/40501/40613/40143/49918/49919/49920 Azure SQL
    // throttling, failover and "database not currently available".
    private static readonly int[] TransientSqlErrors =
    [
        1205, -2, 121, 233, 64, 10053, 10054, 10060, 11001, 1222,
        4060, 4221, 40197, 40501, 40613, 40143, 49918, 49919, 49920
    ];

    private static Microsoft.Data.SqlClient.SqlException? SqlErrorOf(Exception ex)
        => ex.InnerException as Microsoft.Data.SqlClient.SqlException
           ?? ex.GetBaseException() as Microsoft.Data.SqlClient.SqlException;

    private static bool IsPermanentDataConflict(DbUpdateException ex)
        => SqlErrorOf(ex) is { } sql && PermanentConflictSqlErrors.Contains(sql.Number);

    private static bool IsTransientDbFailure(DbUpdateException ex)
        => SqlErrorOf(ex) is { } sql && TransientSqlErrors.Contains(sql.Number);

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var statusCode = ex switch
        {
            BusinessException be => (int)be.StatusCode,
            NotFoundException => 404,
            UnauthorizedAccessException => 401,
            ArgumentException => 400,
            // A real optimistic-concurrency conflict: someone else changed the row. Permanent
            // for this request, and 409 is exactly what it means.
            DbUpdateConcurrencyException => 409,
            DbUpdateException due when IsPermanentDataConflict(due) => 409,
            DbUpdateException due when IsTransientDbFailure(due) => 503,
            // An unclassified write failure is NOT a conflict. 500 keeps the previous "unknown
            // server problem" semantics, and both offline transports already treat >= 500 as
            // retry-with-reconcile rather than throwing the queued write away.
            DbUpdateException => 500,
            _ => 500
        };

        string messageKey = ex switch
        {
            BusinessException be => be.MessageKey,
            UnauthorizedAccessException uae when uae.Message.Contains("Google token") => "InvalidGoogletoken",
            NotFoundException => "NotFound",
            UnauthorizedAccessException => "Unauthorized",
            ArgumentException => "BadRequest",
            // Concurrency is a DbUpdateException subclass — match it FIRST so an escaped
            // concurrency conflict gets a "someone else changed this, refresh" message
            // instead of the generic data-conflict one.
            DbUpdateConcurrencyException => "ConcurrencyConflict",
            DbUpdateException due when IsPermanentDataConflict(due) => "DatabaseConflict",
            DbUpdateException => "ServerError",
            _ => "ServerError"
        };

        var result = Result<string>.Failure(
     _localizer,
     messageKey,
     (HttpStatusCode)statusCode
 );

        response.StatusCode = statusCode;

        // Tells a well-behaved client this is worth trying again, and roughly when.
        if (statusCode == 503)
            response.Headers.RetryAfter = "3";

        await response.WriteAsync(JsonSerializer.Serialize(result));
    }
}
