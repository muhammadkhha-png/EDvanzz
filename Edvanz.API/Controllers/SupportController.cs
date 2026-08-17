using Edvanz.Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Edvanz.API.Controllers;

/// <summary>
/// Public support/contact details. Anonymous so the LOGIN screen can surface the team WhatsApp
/// number for prospective centers ("Are you a center? Contact us") without a token — the same
/// number already returned (authenticated) by GET /api/subscription/status.
/// </summary>
[ApiController]
[Route("api/support")]
public class SupportController : ControllerBase
{
    private readonly SupportOptions _support;

    public SupportController(IOptions<SupportOptions> support)
    {
        _support = support.Value;
    }

    /// <summary>Team contact details for the login-screen "contact us" CTA. Returns an empty number
    /// (never null) when not configured, so the client can hide the CTA gracefully.</summary>
    [HttpGet("contact")]
    [AllowAnonymous]
    public IActionResult GetContact() => Ok(new
    {
        success = true,
        data = new { whatsAppNumber = _support.WhatsAppNumber ?? string.Empty }
    });
}
