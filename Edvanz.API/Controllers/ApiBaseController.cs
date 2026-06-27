using Edvanz.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Base controller providing shared helper methods for all API controllers.
/// Converts the Result pattern to proper HTTP responses with correct status codes.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class ApiBaseController : ControllerBase
{
    /// <summary>
    /// Converts a Result object to an IActionResult with the appropriate HTTP status code.
    /// Success results return the data payload; failure results return the error message.
    /// </summary>
    protected IActionResult ToResponse<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return new ObjectResult(new
            {
                success = true,
                message = result.Message,
                data = result.Data
            })
            {
                StatusCode = (int)result.StatusCode
            };
        }

        return new ObjectResult(new
        {
            success = false,
            message = result.Message
        })
        {
            StatusCode = (int)result.StatusCode
        };
    }
    // <summary>
    /// Returns 401 Unauthorized when the JWT principal cannot be resolved to a User.Id.
    /// Use at the top of any action that requires a known caller identity.
    /// Promoted from per-controller private helpers to the base so every controller
    /// inherits it without duplication.
    /// </summary>
    protected IActionResult UserNotResolved() =>
        new ObjectResult(new { success = false, message = "User could not be resolved from token." })
        {
            StatusCode = StatusCodes.Status401Unauthorized
        };
}