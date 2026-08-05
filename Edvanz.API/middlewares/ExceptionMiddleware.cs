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
            DbUpdateConcurrencyException => 409,
            DbUpdateException => 409,
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
            DbUpdateException => "DatabaseConflict",
            _ => "ServerError"
        };

        var result = Result<string>.Failure(
     _localizer,
     messageKey,
     (HttpStatusCode)statusCode
 );

        response.StatusCode = statusCode;

        await response.WriteAsync(JsonSerializer.Serialize(result));
    }
}