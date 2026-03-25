using Edvanz.Application.Dtos.exceptions;
using System.Text.Json;
using Microsoft.Extensions.Localization;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IStringLocalizer<Edvanz.Domain.Resources.Messages> _localizer;

    public ExceptionMiddleware(RequestDelegate next, IStringLocalizer<Edvanz.Domain.Resources.Messages> localizer)
    {
        _next = next;
        _localizer = localizer;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var statusCode = ex switch
        {
            NotFoundException => 404,
            UnauthorizedAccessException => 401,
            ArgumentException => 400,
            _ => 500
        };

        string messageKey = ex switch
        {
            NotFoundException => "NotFound",
            UnauthorizedAccessException => "Unauthorized",
            ArgumentException => "BadRequest",
            _ => "ServerError"
        };

        var result = new
        {
            success = false,
            message = _localizer[messageKey], 
            #if DEBUG
            details = ex.StackTrace 
            #endif
        };

        response.StatusCode = statusCode;

        await response.WriteAsync(JsonSerializer.Serialize(result));
    }
}