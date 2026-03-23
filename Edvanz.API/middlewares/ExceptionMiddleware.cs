using Edvanz.Application.Dtos.exceptions;
using System.Net;
using System.Text.Json;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
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

    private static Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var response = context.Response;

        response.ContentType = "application/json";

        var result = new
        {
            success = false,
            message = ex.Message,
            //  في production شيل السطر ده           TODO
            details = ex.StackTrace
        };

        response.StatusCode = ex switch
        {
            NotFoundException => 404,
            UnauthorizedAccessException => 401,
            ArgumentException => 400,
            _ => 500
        };

        return response.WriteAsync(JsonSerializer.Serialize(result));
    }
}