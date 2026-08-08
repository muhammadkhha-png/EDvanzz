using Edvanz.Application.Dtos.Attendance;
using Edvanz.Application.Dtos.Payment;

namespace Edvanz.Application.Dtos.ParentUser;

/// <summary>
/// Consolidated per-teacher dashboard for a Parent's child (Parent Module requirements §9).
/// Resolved by (TeacherCode, StudentCode) rather than internal ids — Parent Module requirements
/// §3: codes are pure address resolution, ownership is still enforced via
/// <c>IUserRepo.ResolveOwnedChildIdByTeacherStudentAsync</c> before any section is built, so a
/// Parent can never reach another Parent's child by guessing valid codes.
///
/// Each section is independently gated by the teacher's Parent-visibility configuration
/// (AAM-FR-04.9) and degrades to <c>Visible = false</c> + empty data — never a 403 for the whole
/// call — mirroring <c>StudentTeacherHomeService</c>'s resilience pattern (a module error or a
/// hidden section never breaks the other five).
/// </summary>
public class ParentChildTeacherDashboardDto
{
    public long TeacherId { get; set; }
    public string TeacherCode { get; set; } = null!;
    public string TeacherName { get; set; } = null!;
    public string SubjectName { get; set; } = null!;

    public long ChildId { get; set; }
    public string ChildName { get; set; } = null!;

    public ParentDashboardVideosDto Videos { get; set; } = new();
    public ParentDashboardExamReportDto OnlineExams { get; set; } = new();
    public ParentDashboardExamReportDto OfflineExams { get; set; } = new();
    public ParentDashboardHomeworkDto Homework { get; set; } = new();
    public ParentDashboardAttendanceDto Attendance { get; set; } = new();
    public ParentDashboardPaymentDto Payments { get; set; } = new();
}

/// <summary>Videos section (§9.1) — gated by <c>ParentVisibilityVideo</c>.</summary>
public class ParentDashboardVideosDto
{
    public bool Visible { get; set; }
    public int TotalVideos { get; set; }
    public int TotalSeenVideos { get; set; }
    public int TotalUnseenVideos { get; set; }
}

/// <summary>
/// Shared shape for the Online and Offline exam reports (§9.2 — turn-3 decision: two SEPARATE
/// reports, never combined). Percentage = Grade / MaxGrade × 100, already computed upstream by
/// the existing online-exam report and offline-exam list services; this DTO only carries the
/// Max/Min/Avg aggregated over those already-computed percentages — no percentage math is
/// duplicated here. Gated by <c>ParentVisibilityOnlineExamDefault</c> /
/// <c>ParentVisibilityExamDefault</c> respectively.
/// </summary>
public class ParentDashboardExamReportDto
{
    public bool Visible { get; set; }

    /// <summary>Exams with a valid percentage (a grade AND a positive MaxGrade). Ungradeable/missing-grade exams are excluded.</summary>
    public int CompletedExamsCount { get; set; }
    public decimal? AverageGrade { get; set; }
    public decimal? HighestPerformance { get; set; }
    public decimal? LowestPerformance { get; set; }
}

/// <summary>Homework section (§9.3) — gated by <c>ParentVisibilityHomework</c>.</summary>
public class ParentDashboardHomeworkDto
{
    public bool Visible { get; set; }
    public int TotalHomework { get; set; }
    public int PendingHomework { get; set; }
    public int SubmittedHomework { get; set; }
    public int NotSubmittedHomework { get; set; }
}

/// <summary>Attendance section (§9.4, current month only) — gated by <c>ParentVisibilityAttendance</c>.</summary>
public class ParentDashboardAttendanceDto
{
    public bool Visible { get; set; }

    /// <summary>Null when hidden or when the month has no data. Reuses the existing student/parent monthly attendance shape as-is — no re-derivation of the percentage/day counts.</summary>
    public MonthlyAttendanceSummaryDto? Data { get; set; }
}

/// <summary>Payments section (§9.5, current + previous months + arrears) — gated by <c>ParentVisibilityPayment</c>.</summary>
public class ParentDashboardPaymentDto
{
    public bool Visible { get; set; }

    /// <summary>Null when hidden. Reuses the existing StudentPaymentTrackingDto as-is (custom-price and arrears logic already lives there — not duplicated here).</summary>
    public StudentPaymentTrackingDto? Data { get; set; }
}
