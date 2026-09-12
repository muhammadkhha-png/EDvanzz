using Edvanz.API.Attributes;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.AdminInsights;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// One search box for the whole admin console.
///
/// WHY IT EXISTS: support calls do not arrive scoped to a screen. Someone rings about "Mohamed" and
/// the admin has no idea yet whether that is a teacher, a student on somebody's roster, a student's
/// own app account, or an assistant — and until now, finding out meant guessing a screen and using
/// its local filter, four times over. This answers all four at once.
///
/// ARABIC VARIANTS ARE FOLDED ON EVERY GROUP (مصطفي ≡ مصطفى) through the dbo.ArabicNormalize UDF —
/// the same fold the teacher grid already uses, applied to both the column and the typed term.
/// Phone numbers are matched raw; they carry no Arabic letters and normalising them would only
/// cost an index.
///
/// AUTHORIZATION: class-level [Authorize] plus [ModulePermission(roles: ["SuperAdmin"],
/// roleOnly: true)] — this reads across every tenant on the platform, so it is SuperAdmin only and
/// no narrower gate would be safe (BUG-13: an admin-only endpoint without a role gate is an
/// enumeration hole).
/// </summary>
[Route("api/admin/search")]
[Authorize]
public class AdminSearchController : ApiBaseController
{
    private readonly IAdminInsightsService _insights;
    private readonly ICurrentUserService _currentUser;

    public AdminSearchController(IAdminInsightsService insights, ICurrentUserService currentUser)
    {
        _insights = insights;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Grouped top-few across teachers, roster students, student app accounts and assistants.
    ///
    /// A query shorter than two characters returns empty groups rather than most of the platform —
    /// a single letter matches nearly everything and the dropdown becomes noise.
    ///
    /// The two student groups are DIFFERENT THINGS and are never merged: a roster student is the
    /// record a teacher created (its code is unique only within that teacher, so the owning
    /// teacher's name is carried as the disambiguator), while a student account is the student's
    /// own login on the platform.
    ///
    /// SAMPLE: GET /api/admin/search?q=mostafa
    ///         GET /api/admin/search?q=01001234567&amp;take=10
    /// </summary>
    [HttpGet]
    [ModulePermission(roles: new[] { "SuperAdmin" }, roleOnly: true)]
    [ProducesResponseType(typeof(Result<AdminSearchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int take = 5)
    {
        if (_currentUser.UserId is null) return UserNotResolved();
        return ToResponse(await _insights.SearchAsync(q, take));
    }
}
