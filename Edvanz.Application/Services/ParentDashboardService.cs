using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Attendance;
using Edvanz.Application.Dtos.ParentUser;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Edvanz.Application.Services;

/// <inheritdoc cref="IParentDashboardService"/>
public sealed class ParentDashboardService : IParentDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttendanceService _attendanceService;
    private readonly IPaymentService _paymentService;
    private readonly IStudentOnlineExamService _onlineExamService;
    private readonly IExamHomeworkService _examHomeworkService;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;
    private readonly ILogger<ParentDashboardService> _logger;

    /// <summary>Page size used to pull the offline-exam list for the percentage aggregate (bounded per student·teacher, same value StudentTeacherHomeService uses).</summary>
    private const int OfflineExamFetchPageSize = 200;

    public ParentDashboardService(
        IUnitOfWork unitOfWork,
        IAttendanceService attendanceService,
        IPaymentService paymentService,
        IStudentOnlineExamService onlineExamService,
        IExamHomeworkService examHomeworkService,
        IStringLocalizer<Domain.Resources.Messages> localizer,
        ILogger<ParentDashboardService> logger)
    {
        _unitOfWork = unitOfWork;
        _attendanceService = attendanceService;
        _paymentService = paymentService;
        _onlineExamService = onlineExamService;
        _examHomeworkService = examHomeworkService;
        _localizer = localizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ParentChildTeacherDashboardDto>> GetTeacherDashboardAsync(
        long parentUserId, string teacherCode, string studentCode)
    {
        if (string.IsNullOrWhiteSpace(teacherCode) || teacherCode.Length != 8)
            return Result<ParentChildTeacherDashboardDto>.Failure(_localizer, "InvalidTeacherCode", HttpStatusCode.BadRequest);

        if (string.IsNullOrWhiteSpace(studentCode))
            return Result<ParentChildTeacherDashboardDto>.Failure(_localizer, "StudentCodeRequired", HttpStatusCode.BadRequest);

        // ── Code → entity resolution (pure address resolution — NOT an authorization decision) ──
        var teacher = await _unitOfWork.Users.GetActiveTeacherByCodeAsync(teacherCode);
        if (teacher is null)
            return Result<ParentChildTeacherDashboardDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        var teacherStudent = await _unitOfWork.Users.GetActiveTeacherStudentByCodeAsync(teacher.Id, studentCode);
        if (teacherStudent is null)
            return Result<ParentChildTeacherDashboardDto>.Failure(_localizer, "StudentNotFound", HttpStatusCode.NotFound);

        // ── Ownership gate — the actual authorization check. Codes never bypass this. ──
        long? childId = await _unitOfWork.Users.ResolveOwnedChildIdByTeacherStudentAsync(
            parentUserId, teacher.Id, teacherStudent.Id);
        if (childId is null)
            return Result<ParentChildTeacherDashboardDto>.Failure(_localizer, "TeacherLinkNotFound", HttpStatusCode.Forbidden);

        var child = await _unitOfWork.Users.GetActiveChildAsync(parentUserId, childId.Value);
        if (child is null)
            return Result<ParentChildTeacherDashboardDto>.Failure(_localizer, "ChildNotFound", HttpStatusCode.NotFound);

        long teacherStudentId = teacherStudent.Id;

        // ── Teacher header + visibility flags (same batch call as the existing all-children dashboard) ──
        var batch = await _unitOfWork.Users.GetTeacherDashboardDataAsync(new List<long> { teacher.Id });
        batch.Teachers.TryGetValue(teacher.Id, out var teacherEntity);
        batch.Configurations.TryGetValue(teacher.Id, out var config);

        string teacherName = string.Empty;
        if (teacherEntity is not null && batch.Users.TryGetValue(teacherEntity.UserId, out var teacherUser))
            teacherName = teacherUser.FullName;

        var parentUser = await _unitOfWork.Users.GetActiveParentUserByIdAsync(parentUserId);
        string? language = parentUser?.LanguagePreference;

        string subjectName = teacherEntity?.CustomSubject ?? string.Empty;
        if (teacherEntity is not null &&
            batch.TeacherSubjects.TryGetValue(teacher.Id, out var teacherSubjects) &&
            teacherSubjects.Any() &&
            batch.Subjects.TryGetValue(teacherSubjects.First().SubjectId, out var subject))
        {
            // Respect the parent's language preference (AAM-FR-02.2) — same rule as the all-children dashboard.
            subjectName = language == "ar" ? subject.NameAr : subject.NameEn;
        }

        bool vVideo = config?.ParentVisibilityVideo ?? true;
        bool vOnlineExam = config?.ParentVisibilityOnlineExamDefault ?? false;
        bool vOfflineExam = config?.ParentVisibilityExamDefault ?? false;
        bool vHomework = config?.ParentVisibilityHomework ?? true;

        var dashboard = new ParentChildTeacherDashboardDto
        {
            TeacherId = teacher.Id,
            TeacherCode = teacherEntity?.TeacherCode ?? teacherCode,
            TeacherName = teacherName,
            SubjectName = subjectName,
            ChildId = child.Id,
            ChildName = child.ChildName,
            Videos = await BuildVideosAsync(teacher.Id, teacherStudentId, vVideo),
            OnlineExams = await BuildOnlineExamsAsync(teacher.Id, teacherStudentId, language, vOnlineExam),
            OfflineExams = await BuildOfflineExamsAsync(teacher.Id, teacherStudentId, language, vOfflineExam),
            Homework = await BuildHomeworkAsync(teacher.Id, teacherStudentId, vHomework),
            Attendance = await BuildAttendanceAsync(teacher.Id, teacherStudentId),
            Payments = await BuildPaymentsAsync(teacher.Id, teacherStudentId)
        };

        return Result<ParentChildTeacherDashboardDto>.Success(dashboard, _localizer, "Success", HttpStatusCode.OK);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SECTION BUILDERS (each fail-soft: a module error → visible flag preserved, empty data)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// §9.1. No existing service exposes a per-student seen/unseen rollup, so this is the one
    /// section with no prior "viewer type" gate to reuse — ParentVisibilityVideo is checked here,
    /// the sole enforcement point for this flag.
    /// </summary>
    private async Task<ParentDashboardVideosDto> BuildVideosAsync(
        long teacherId, long teacherStudentId, bool visible)
    {
        var section = new ParentDashboardVideosDto { Visible = visible };
        if (!visible) return section;
        try
        {
            var (total, seen) = await _unitOfWork.VideoAssetsRepo
                .GetStudentVideoSeenCountsAsync(teacherId, teacherStudentId);
            section.TotalVideos = total;
            section.TotalSeenVideos = seen;
            section.TotalUnseenVideos = Math.Max(0, total - seen);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parent dashboard: videos failed (teacher {TeacherId}, roster {RosterId})", teacherId, teacherStudentId);
        }
        return section;
    }

    /// <summary>
    /// §9.2 online report. <see cref="IStudentOnlineExamService.GetMyExamsAsync"/> does no internal
    /// visibility gating (confirmed against source) — ParentVisibilityOnlineExamDefault is checked
    /// here. Percentage = StudentDegree / ExamDegree × 100, computed per exam and aggregated;
    /// exams with no attempt (StudentDegree null) or no valid denominator are excluded, matching
    /// "exclude exams that cannot produce a valid percentage" (Parent Module requirements §9.2/§6).
    /// </summary>
    private async Task<ParentDashboardExamReportDto> BuildOnlineExamsAsync(
        long teacherId, long teacherStudentId, string? language, bool visible)
    {
        var section = new ParentDashboardExamReportDto { Visible = visible };
        if (!visible) return section;
        try
        {
            var result = await _onlineExamService.GetMyExamsAsync(teacherId, teacherStudentId, language);
            if (result.IsSuccess && result.Data is { } data)
            {
                var percentages = data.Past
                    .Where(e => e.StudentDegree.HasValue && e.ExamDegree > 0)
                    .Select(e => e.StudentDegree!.Value / e.ExamDegree * 100m)
                    .ToList();

                ApplyStats(section, percentages);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parent dashboard: online exams failed (teacher {TeacherId}, roster {RosterId})", teacherId, teacherStudentId);
        }
        return section;
    }

    /// <summary>
    /// §9.2 offline report. <see cref="IExamHomeworkService.GetMyOfflineExamsAsync"/> already
    /// computes <c>ScorePercentage</c> per row (null when ungradeable) — reused as-is, no
    /// Grade/MaxGrade math duplicated here. ParentVisibilityExamDefault gates this section (the
    /// existing "offline exam" flag, unchanged from before this feature).
    /// </summary>
    private async Task<ParentDashboardExamReportDto> BuildOfflineExamsAsync(
        long teacherId, long teacherStudentId, string? language, bool visible)
    {
        var section = new ParentDashboardExamReportDto { Visible = visible };
        if (!visible) return section;
        try
        {
            var result = await _examHomeworkService.GetMyOfflineExamsAsync(
                teacherId, teacherStudentId, language, page: 1, pageSize: OfflineExamFetchPageSize);
            if (result.IsSuccess && result.Data?.data is { } rows)
            {
                var percentages = rows
                    .Where(r => r.ScorePercentage.HasValue)
                    .Select(r => r.ScorePercentage!.Value)
                    .ToList();

                ApplyStats(section, percentages);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parent dashboard: offline exams failed (teacher {TeacherId}, roster {RosterId})", teacherId, teacherStudentId);
        }
        return section;
    }

    /// <summary>§9.3. No existing homework read surface exists for students or parents — ParentVisibilityHomework is checked here, the sole enforcement point for this section.</summary>
    private async Task<ParentDashboardHomeworkDto> BuildHomeworkAsync(
        long teacherId, long teacherStudentId, bool visible)
    {
        var section = new ParentDashboardHomeworkDto { Visible = visible };
        if (!visible) return section;
        try
        {
            var (total, pending, submitted, notSubmitted) = await _unitOfWork.ExamHomeworkRepo
                .GetHomeworkStatusBreakdownAsync(teacherId, teacherStudentId);
            section.TotalHomework = total;
            section.PendingHomework = pending;
            section.SubmittedHomework = submitted;
            section.NotSubmittedHomework = notSubmitted;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parent dashboard: homework failed (teacher {TeacherId}, roster {RosterId})", teacherId, teacherStudentId);
        }
        return section;
    }

    /// <summary>
    /// §9.4, current month only. <see cref="IAttendanceService.GetStudentViewAttendanceAsync"/>
    /// already self-gates on ParentVisibilityAttendance via <see cref="AttendanceViewerType.Parent"/>
    /// — NOT re-checked here (Parent Module requirements §9: reuse existing services, don't
    /// duplicate authorization/visibility logic). A Forbidden result from the visibility gate is
    /// treated as "hidden" for this section rather than surfaced as a dashboard-wide error.
    /// Passing an empty request defaults to the teacher-local CURRENT month (§9.4/§10's
    /// "up to today, never future days" rule already lives inside that method via
    /// ITimeZoneService.GetTeacherLocalDate — not re-derived here).
    /// </summary>
    private async Task<ParentDashboardAttendanceDto> BuildAttendanceAsync(long teacherId, long teacherStudentId)
    {
        var section = new ParentDashboardAttendanceDto();
        try
        {
            var request = new StudentTimelineMonthRequest();
            var result = await _attendanceService.GetStudentViewAttendanceAsync(
                teacherId, teacherStudentId, request, AttendanceViewerType.Parent);
            section.Visible = result.IsSuccess;
            if (result.IsSuccess)
                section.Data = result.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parent dashboard: attendance failed (teacher {TeacherId}, roster {RosterId})", teacherId, teacherStudentId);
        }
        return section;
    }

    /// <summary>
    /// §9.5. <see cref="IPaymentService.GetStudentPaymentTrackingAsync"/> already self-gates on
    /// ParentVisibilityPayment via <see cref="PaymentViewerType.Parent"/> — same reuse rationale as
    /// <see cref="BuildAttendanceAsync"/>. Custom-price resolution and arrears-through-cutoff are
    /// already correct inside that method; nothing about pricing is re-derived here.
    /// </summary>
    private async Task<ParentDashboardPaymentDto> BuildPaymentsAsync(long teacherId, long teacherStudentId)
    {
        var section = new ParentDashboardPaymentDto();
        try
        {
            var result = await _paymentService.GetStudentPaymentTrackingAsync(
                teacherId, teacherStudentId, PaymentViewerType.Parent);
            section.Visible = result.IsSuccess;
            if (result.IsSuccess)
                section.Data = result.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parent dashboard: payments failed (teacher {TeacherId}, roster {RosterId})", teacherId, teacherStudentId);
        }
        return section;
    }

    /// <summary>Shared Max/Min/Avg aggregation for the two exam-report builders. Rounds the average to 2 decimal places; leaves Max/Min at full precision (already a clean 0–100 percentage).</summary>
    private static void ApplyStats(ParentDashboardExamReportDto section, List<decimal> percentages)
    {
        section.CompletedExamsCount = percentages.Count;
        if (percentages.Count == 0)
            return;

        section.AverageGrade = Math.Round(percentages.Average(), 2);
        section.HighestPerformance = percentages.Max();
        section.LowestPerformance = percentages.Min();
    }
}
