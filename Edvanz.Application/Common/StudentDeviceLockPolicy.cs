using System;
using Edvanz.Domain.Entities;

namespace Edvanz.Application.Common;

/// <summary>
/// Outcome of evaluating a student's current device against a teacher's device lock.
/// </summary>
public enum DeviceLockDecision
{
    /// <summary>Access allowed — the lock is off, or the presented device matches the bound one.</summary>
    Allowed,

    /// <summary>Lock is on but no device is bound yet — the student must confirm and register this device.</summary>
    RegistrationRequired,

    /// <summary>Lock is on and a DIFFERENT device is bound — access denied until a teacher/assistant resets it.</summary>
    Mismatch
}

/// <summary>
/// Central, side-effect-free policy for the "lock student to one device (per teacher)" feature.
///
/// The teacher toggles <see cref="TeacherConfiguration.IsDeviceLockEnabled"/>; the per-student
/// binding lives on <see cref="StudentTeacherLink.LockedDeviceId"/> (so it is naturally isolated
/// per teacher — a student can be on a different device for each teacher). The client sends its
/// device id in the <see cref="HeaderName"/> request header (same shape as
/// <c>VideoWatchEvent.DeviceId</c> — a client-generated id, never an authentication credential;
/// it only gates teacher access to a single phone).
///
/// This must be evaluated at EVERY teacher-scoped student entry point (the aggregated home read
/// plus all per-module content reads) so the lock cannot be bypassed by hitting a different screen.
/// </summary>
public static class StudentDeviceLockPolicy
{
    /// <summary>Request header carrying the client-generated device id.</summary>
    public const string HeaderName = "X-Device-Id";

    /// <summary>
    /// Stable failure code: lock on, no device registered yet (returned with 409 Conflict).
    /// The app reacts by showing the "register this device" consent sheet.
    /// </summary>
    public const string RegistrationRequiredCode = "DeviceRegistrationRequired";

    /// <summary>
    /// Stable failure code: lock on, bound to a different device (returned with 403 Forbidden).
    /// The app reacts by showing the "you're on a different phone — ask your teacher to reset" dialog.
    /// </summary>
    public const string MismatchCode = "DeviceMismatch";

    /// <summary>
    /// Evaluates whether <paramref name="deviceId"/> may open the teacher behind
    /// <paramref name="link"/>. Pure (no I/O). <paramref name="config"/> may be null — the lock
    /// then fails open to "allowed" (a missing config row means the teacher never configured a
    /// lock). Comparison is ordinal. A missing/blank device id on an already-bound link is treated
    /// as a mismatch (fail closed), so an out-of-date client cannot slip past the lock.
    /// </summary>
    public static DeviceLockDecision Evaluate(
        StudentTeacherLink link, TeacherConfiguration? config, string? deviceId)
    {
        if (config is null || !config.IsDeviceLockEnabled)
            return DeviceLockDecision.Allowed;

        var bound = link.LockedDeviceId;
        if (string.IsNullOrWhiteSpace(bound))
            return DeviceLockDecision.RegistrationRequired;

        var presented = deviceId?.Trim();
        if (!string.IsNullOrEmpty(presented) &&
            string.Equals(bound, presented, StringComparison.Ordinal))
            return DeviceLockDecision.Allowed;

        return DeviceLockDecision.Mismatch;
    }
}
