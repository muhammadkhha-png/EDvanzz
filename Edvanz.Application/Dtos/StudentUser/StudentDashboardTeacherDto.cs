namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// Derived-only status strings the student dashboard may emit that are NOT
/// persisted <see cref="Edvanz.Domain.Enums.LinkStatus"/> values. Kept separate
/// from the stored enum (and its filtered unique index) so the enum is never
/// polluted with display-only states.
/// </summary>
public static class DashboardLinkStatus
{
    /// <summary>
    /// The teacher accepted the request but has not bound the student to a roster
    /// record yet — connected, but no access to the teacher's data yet.
    /// </summary>
    public const string AwaitingLink = "AwaitingLink";
}

/// <summary>
/// Output DTO representing a single Teacher entry on the Student's dashboard.
/// One entry per teacher — the LATEST link row for that teacher, so the student
/// sees the current state of their request (Pending / Active / Rejected / ...).
/// AAM-FR-05.6: Displays the Teacher's full name and subject name.
/// AAM-FR-05.7: Each Teacher is displayed distinctly.
/// </summary>
public class StudentDashboardTeacherDto
{
    /// <summary>
    /// The Teacher's numeric id (<c>Teacher.Id</c>). This is the value the
    /// student-facing content routes take as their <c>{teacherId}</c> route
    /// segment — e.g. <c>GET /api/videos/student/teachers/{teacherId}/units</c>,
    /// attendance, payments, exams. It is NOT the <c>User.Id</c> and NOT the
    /// <see cref="TeacherCode"/>; use this (never <see cref="LinkId"/>) when the
    /// frontend needs to select a specific linked teacher's data.
    /// </summary>
    public long TeacherId { get; set; }

    /// <summary>
    /// The StudentTeacherLink Id — used for unlink operations.
    /// </summary>
    public long LinkId { get; set; }

    /// <summary>
    /// The single AUTHORITATIVE status the student app renders — the server folds
    /// the enrollment binding into the request lifecycle so the client never has to
    /// combine fields. One of:
    ///   Pending, Active, Rejected, Unlinked, RemovedByTeacher, CancelledByStudent
    ///   (persisted <see cref="Edvanz.Domain.Enums.LinkStatus"/> names), plus the
    ///   derived <see cref="DashboardLinkStatus.AwaitingLink"/>.
    /// Notably, "Active" is returned ONLY when the student actually has access
    /// (Active AND bound to a roster record). An Active link whose binding was
    /// removed reports "RemovedByTeacher"; an accepted-but-not-yet-bound link
    /// reports "AwaitingLink".
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>UTC timestamp the student submitted the link request.</summary>
    public DateTime? RequestedAt { get; set; }

    /// <summary>UTC timestamp the teacher accepted or rejected the request.</summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>
    /// Teacher's unique 8-digit code. Displayed on the dashboard for reference.
    /// </summary>
    public string TeacherCode { get; set; } = null!;

    /// <summary>
    /// Teacher's full name from the User table.
    /// AAM-FR-05.6: Displayed upon successful linking.
    /// </summary>
    public string TeacherFullName { get; set; } = null!;

    /// <summary>
    /// Subject name taught by the teacher.
    /// AAM-FR-05.6: Displayed alongside the teacher's name.
    /// Shows the first ministry-defined subject or the custom subject.
    /// </summary>
    public string SubjectName { get; set; } = null!;

    /// <summary>
    /// When the link became Active (the teacher accepted the request).
    /// Meaningful only for Active/Unlinked/RemovedByTeacher entries.
    /// </summary>
    public DateTime LinkedAt { get; set; }

    /// <summary>
    /// Whether the teacher's student record for this student still exists.
    /// False if the teacher deleted the student record after linking.
    /// </summary>
    public bool IsEnrollmentActive { get; set; }

    /// <summary>
    /// Whether this connection is LINKED to one of the teacher's students (bound by
    /// code). Only when true — and <see cref="Status"/> is Active — can the student
    /// see this teacher's data. Active-but-not-linked = connected, no access yet
    /// ("Awaiting link"). Distinct from <see cref="Status"/> (the request lifecycle).
    /// </summary>
    public bool IsLinked { get; set; }

    // ─── Visibility flags from TeacherConfiguration (AAM-FR-04.8 / AAM-FR-05.8) ───

    /// <summary>
    /// Whether this teacher allows the student to see the Attendance Track.
    /// </summary>
    public bool VisibilityAttendance { get; set; }

    /// <summary>
    /// Whether this teacher allows the student to see the Payment Track.
    /// </summary>
    public bool VisibilityPayment { get; set; }

    /// <summary>
    /// Whether this teacher allows the student to see the Homework Track.
    /// </summary>
    public bool VisibilityHomework { get; set; }

    /// <summary>
    /// Default exam visibility for this teacher.
    /// AAM-BR-10: Per-exam visibility defaults to hidden unless explicitly enabled.
    /// </summary>
    public bool VisibilityExamDefault { get; set; }

    /// <summary>
    /// Whether this teacher allows the student to see the Videos module.
    /// </summary>
    public bool VisibilityVideo { get; set; }

    /// <summary>
    /// Default online-exam visibility for this teacher (default: visible).
    /// </summary>
    public bool VisibilityOnlineExamDefault { get; set; }
}