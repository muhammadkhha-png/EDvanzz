using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Defines the lifecycle status of a Student-to-Teacher link.
///
/// Request/approval flow (supersedes the original AAM-FR-05.5 credential flow):
/// the student sends a link request (Pending) using the teacher's 8-digit code;
/// the teacher accepts (Active) or rejects (Rejected) it. Either side can later
/// end an Active link (Unlinked / RemovedByTeacher), and the student can cancel
/// a still-Pending request (CancelledByStudent).
///
/// Terminal rows (Rejected, Unlinked, RemovedByTeacher, CancelledByStudent) are
/// preserved for audit; the DB enforces at most ONE live row (Pending or Active)
/// per (StudentUserId, TeacherId) via a filtered unique index — keep the filter
/// in EdvanzDbContext in sync with these numeric values.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LinkStatus
{
    /// <summary>Link is live — the student can see this teacher's data.</summary>
    Active = 1,

    /// <summary>The student removed the teacher from their dashboard.</summary>
    Unlinked = 2,

    /// <summary>Request sent by the student, awaiting the teacher's decision.</summary>
    Pending = 3,

    /// <summary>The teacher rejected the student's link request.</summary>
    Rejected = 4,

    /// <summary>The teacher removed the student from their linked-students list.</summary>
    RemovedByTeacher = 5,

    /// <summary>The student cancelled their own request before the teacher responded.</summary>
    CancelledByStudent = 6
}