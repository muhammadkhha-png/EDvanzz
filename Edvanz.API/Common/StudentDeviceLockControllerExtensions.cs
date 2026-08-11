using System.Net;
using System.Threading.Tasks;
using Edvanz.Application.Common;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Edvanz.API.Common;

/// <summary>
/// Device-lock gate shared by the student teacher-scoped controllers. Each of them already
/// resolves the active <see cref="StudentTeacherLink"/> for (student, teacher); this adds the
/// one-device check on top, reusing <see cref="StudentDeviceLockPolicy"/> so the rule lives in
/// exactly one place. Returns a localized error <see cref="IActionResult"/> when the caller's
/// device is not allowed (409 registration-required / 403 mismatch), or null when access may proceed.
///
/// Response body matches the controllers' existing <c>{ success, code, message }</c> shape so the
/// frontend branches on the stable <c>code</c> exactly as it does for the other resolution failures.
/// </summary>
public static class StudentDeviceLockControllerExtensions
{
    /// <summary>Reads the client device id from the request header (empty string when absent).</summary>
    public static string ReadDeviceId(this ControllerBase controller) =>
        controller.Request.Headers[StudentDeviceLockPolicy.HeaderName].ToString();

    /// <summary>
    /// Loads the teacher configuration, evaluates the device lock for <paramref name="link"/>,
    /// and returns a localized error response when blocked, or null when allowed.
    /// </summary>
    public static async Task<IActionResult?> CheckDeviceLockAsync(
        this ControllerBase controller,
        IUnitOfWork unitOfWork,
        IStringLocalizer localizer,
        long teacherId,
        StudentTeacherLink link)
    {
        var config = await unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        var decision = StudentDeviceLockPolicy.Evaluate(link, config, controller.ReadDeviceId());
        return decision switch
        {
            DeviceLockDecision.RegistrationRequired =>
                DeviceError(localizer, StudentDeviceLockPolicy.RegistrationRequiredCode, HttpStatusCode.Conflict),
            DeviceLockDecision.Mismatch =>
                DeviceError(localizer, StudentDeviceLockPolicy.MismatchCode, HttpStatusCode.Forbidden),
            _ => null
        };
    }

    private static IActionResult DeviceError(IStringLocalizer localizer, string code, HttpStatusCode status) =>
        new ObjectResult(new { success = false, code, message = localizer[code].Value })
        {
            StatusCode = (int)status
        };
}
