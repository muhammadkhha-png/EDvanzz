using System.Net;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers;

/// <summary>
/// Shared base for the three Module 6 (Exams &amp; Homework) controllers. Centralizes the
/// JWT-to-teacher resolution helper so the same logic does not get copy-pasted three
/// times. Mirrors the pattern documented in <c>SubscriptionController</c> — see the
/// catalog §1.3 decision: "<c>teacherId</c> always derived from JWT, never from the
/// request body or route".
///
/// Inherits <see cref="ApiBaseController"/> so the standard <c>ToResponse&lt;T&gt;</c>
/// helper and the inherited <c>[ApiController] / [Route("api/[controller]")]</c>
/// attributes still apply.
/// </summary>
public abstract class ModuleSixApiBaseController : ApiBaseController
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    protected ModuleSixApiBaseController(
        ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Resolves the calling user to a teacher id:
    ///   - Teacher role: lookup by user id.
    ///   - Assistant role: load assistant data, return owning tutor id (BR-EXH-NFR-004).
    /// Returns null when no mapping exists (e.g., a SuperAdmin should never hit
    /// these endpoints — Module 6 is teacher-scoped).
    /// </summary>
    protected async Task<long?> ResolveTeacherIdAsync()
    {
        long? userId = _currentUser.UserId;
        if (userId is null) return null;

        // Center tier: resolve the acting teacher from the X-Acting-Teacher-Id header, validated
        // fail-closed against center membership (shared helper — honors §3.3 / BUG-12).
        if (string.Equals(_currentUser.Role, UserRoles.Center, StringComparison.Ordinal)
            || string.Equals(_currentUser.Role, UserRoles.CenterAssistant, StringComparison.Ordinal))
            return await _currentUser.ResolveActingTeacherIdAsync();

        if (string.Equals(_currentUser.Role, "Assistant", StringComparison.Ordinal))
        {
            var assistant = await _currentUser.GetAssistantDataAsync();
            return assistant?.TeacherAccountId;
        }

        var teacher = await _unitOfWork.Users.GetTeacherByUserIdAsync(userId.Value);
        return teacher?.Id;
    }

    /// <summary>
    /// Returns the calling user's id from the JWT. The service layer uses this as
    /// <c>actingUserId</c> for audit logging.
    /// </summary>
    protected long GetActingUserId() =>
        _currentUser.UserId
        ?? throw new UnauthorizedAccessException("Acting user id missing from JWT");

    /// <summary>
    /// True when the authenticated caller is an <c>Assistant</c> (as opposed to the owning
    /// Teacher or a SuperAdmin). Read endpoints use this to SCOPE the response to the
    /// assistant's OWN data (their own wallet / their own collections) instead of the
    /// full teacher view.
    ///
    /// TODO(assistant-dashboard): first-cut role flag. A dedicated, correctly-designed
    /// assistant dashboard (own collections, own wallet, own targets) is still to be built
    /// end-to-end by BOTH frontend and backend — this flag only powers the interim scoping.
    /// </summary>
    protected bool IsAssistantCaller() =>
        string.Equals(_currentUser.Role, "Assistant", StringComparison.Ordinal)
        // A CenterAssistant gets the SAME scoped-down payment experience as a teacher assistant —
        // their own wallet/collections + Collect and Student-Leaving only (never full revenue / edits).
        // Scoping keys on their own userId (GetActingUserId), so their wallet is per acting-teacher.
        || string.Equals(_currentUser.Role, UserRoles.CenterAssistant, StringComparison.Ordinal);

    /// <summary>
    /// Convenience for the interim assistant-scoping: the caller's own user id when they are an
    /// assistant, otherwise <c>null</c> (Teacher/SuperAdmin → full, unscoped view). Passed to
    /// scope-aware service methods (e.g. dashboard / wallets / collector summary).
    /// </summary>
    protected long? AssistantScopeUserId() =>
        IsAssistantCaller() ? GetActingUserId() : (long?)null;

    /// <summary>
    /// Standardized response when the teacher row cannot be resolved from the
    /// authenticated user. Returns 404 — consistent with <c>SubscriptionController</c>.
    /// </summary>
    protected IActionResult TeacherNotResolved() =>
        new ObjectResult(new { success = false, message = "Teacher not found" })
        {
            StatusCode = (int)HttpStatusCode.NotFound
        };
}
