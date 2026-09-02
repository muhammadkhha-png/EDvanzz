using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Edvanz.Application.Dtos.Attendance;
using Edvanz.Application.Dtos.ParentUser;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Edvanz.Application.Services;

/// <inheritdoc cref="IParentSectionComposer"/>
/// <remarks>
/// These builders were MOVED here verbatim from <c>ParentDashboardService</c> (which now delegates
/// to this class) so the parent mobile dashboard and the public parent portal share one
/// implementation. Nothing about the mobile dashboard's behaviour or response shape changed: the
/// dashboard still aggregates Max/Min/Avg over exactly the same percentage stream it always did —
/// that aggregation stayed in the dashboard service, only the data-fetching moved here.
/// </remarks>
public sealed class ParentSectionComposer : IParentSectionComposer
{
    /// <summary>Page size used to pull the offline-exam list (bounded per student·teacher — the same value StudentTeacherHomeService and ParentDashboardService have always used).</summary>
    private const int OfflineExamFetchPageSize = 200;

    private const string OfflineExamType = "Offline";
    private const string OnlineExamType = "Online";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttendanceService _attendanceService;
    private readonly IPaymentService _paymentService;
    private readonly IStudentOnlineExamService _onlineExamService;
    private readonly IExamHomeworkService _examHomeworkService;
    private readonly ILogger<ParentSectionComposer> _logger;

    public ParentSectionComposer(
        IUnitOfWork unitOfWork,
        IAttendanceService attendanceService,
        IPaymentService paymentService,
        IStudentOnlineExamService onlineExamService,
        IExamHomeworkService examHomeworkService,
        ILogger<ParentSectionComposer> logger)
    {
        _unitOfWork = unitOfWork;
        _attendanceService = attendanceService;
        _paymentService = paymentService;
        _onlineExamService = onlineExamService;
        _examHomeworkService = examHomeworkService;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="IAttendanceService.GetStudentViewAttendanceAsync"/> already self-gates on
    /// ParentVisibilityAttendance via <see cref="AttendanceViewerType.Parent"/> — NOT re-checked
    /// here (reuse existing services, don't duplicate authorization/visibility logic). A Forbidden
    /// result from the visibility gate is treated as "hidden" rather than surfaced as an error.
    /// An empty request defaults to the teacher-local CURRENT month ("up to today, never future
    /// days" already lives inside that method via ITimeZoneService.GetTeacherLocalDate).
    /// </remarks>
    public async Task<ParentDashboardAttendanceDto> BuildAttendanceAsync(
        long teacherId, long teacherStudentId, int? year = null, int? month = null)
    {
        var section = new ParentDashboardAttendanceDto();
        try
        {
            var request = new StudentTimelineMonthRequest { Year = year, Month = month };
            var result = await _attendanceService.GetStudentViewAttendanceAsync(
                teacherId, teacherStudentId, request, AttendanceViewerType.Parent);
            section.Visible = result.IsSuccess;
            if (result.IsSuccess)
                section.Data = result.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parent sections: attendance failed (teacher {TeacherId}, roster {RosterId})", teacherId, teacherStudentId);
        }
        return section;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="IPaymentService.GetStudentPaymentTrackingAsync"/> already self-gates on
    /// ParentVisibilityPayment via <see cref="PaymentViewerType.Parent"/> — same reuse rationale as
    /// <see cref="BuildAttendanceAsync"/>. Custom-price resolution and arrears-through-cutoff are
    /// already correct inside that method; nothing about pricing is re-derived here.
    /// </remarks>
    public async Task<ParentDashboardPaymentDto> BuildPaymentsAsync(long teacherId, long teacherStudentId)
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
            _logger.LogWarning(ex, "Parent sections: payments failed (teacher {TeacherId}, roster {RosterId})", teacherId, teacherStudentId);
        }
        return section;
    }

    /// <inheritdoc />
    /// <remarks>
    /// No existing service exposes a per-student seen/unseen rollup, so this is the one section
    /// with no prior "viewer type" gate to reuse — ParentVisibilityVideo is checked by the caller
    /// and passed in, the sole enforcement point for that flag.
    /// </remarks>
    public async Task<ParentDashboardVideosDto> BuildVideosAsync(
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
            _logger.LogWarning(ex, "Parent sections: videos failed (teacher {TeacherId}, roster {RosterId})", teacherId, teacherStudentId);
        }
        return section;
    }

    /// <inheritdoc />
    /// <remarks>No existing homework read surface exists for students or parents — ParentVisibilityHomework is the caller's call, the sole enforcement point for this section.</remarks>
    public async Task<ParentDashboardHomeworkDto> BuildHomeworkAsync(
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
            _logger.LogWarning(ex, "Parent sections: homework failed (teacher {TeacherId}, roster {RosterId})", teacherId, teacherStudentId);
        }
        return section;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="IStudentOnlineExamService.GetMyExamsAsync"/> does no internal visibility gating,
    /// so ParentVisibilityOnlineExamDefault is the caller's call. Only the PAST list is mapped:
    /// an upcoming online exam has no result to show a parent, and this keeps the percentage
    /// stream identical to the one the parent dashboard has always aggregated
    /// (StudentDegree / ExamDegree × 100, excluding rows with no attempt or no valid denominator).
    /// </remarks>
    public async Task<ParentGradeSectionDto> BuildOnlineGradesAsync(
        long teacherId, long teacherStudentId, string? language, bool visible)
    {
        var section = new ParentGradeSectionDto { Visible = visible };
        if (!visible) return section;
        try
        {
            var result = await _onlineExamService.GetMyExamsAsync(teacherId, teacherStudentId, language);
            if (result.IsSuccess && result.Data is { } data)
            {
                foreach (var exam in data.Past)
                {
                    decimal? percentage = exam.StudentDegree.HasValue && exam.ExamDegree > 0
                        ? exam.StudentDegree.Value / exam.ExamDegree * 100m
                        : null;

                    section.Rows.Add(new ParentGradeRowDto
                    {
                        ExamId = exam.ExamId,
                        ExamName = exam.ExamName,
                        Subject = string.IsNullOrWhiteSpace(exam.Subject) ? null : exam.Subject,
                        Date = exam.ExamDate,
                        ExamType = OnlineExamType,
                        Score = exam.StudentDegree,
                        MaxGrade = exam.ExamDegree,
                        ScorePercentage = percentage,
                        IsGraded = percentage.HasValue
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parent sections: online exams failed (teacher {TeacherId}, roster {RosterId})", teacherId, teacherStudentId);
        }
        return section;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="IExamHomeworkService.GetMyOfflineExamsAsync"/> already computes
    /// <c>ScorePercentage</c> per row (null when ungradeable) — reused as-is, no Grade/MaxGrade
    /// math duplicated here. ParentVisibilityExamDefault is the caller's call.
    /// </remarks>
    public async Task<ParentGradeSectionDto> BuildOfflineGradesAsync(
        long teacherId, long teacherStudentId, string? language, bool visible)
    {
        var section = new ParentGradeSectionDto { Visible = visible };
        if (!visible) return section;
        try
        {
            var result = await _examHomeworkService.GetMyOfflineExamsAsync(
                teacherId, teacherStudentId, language, page: 1, pageSize: OfflineExamFetchPageSize);
            if (result.IsSuccess && result.Data?.data is { } rows)
            {
                foreach (var row in rows)
                {
                    section.Rows.Add(new ParentGradeRowDto
                    {
                        ExamId = row.ExamId,
                        ExamName = row.ExamName,
                        Subject = row.Subject,
                        Date = row.Date,
                        ExamType = OfflineExamType,
                        Score = row.Score,
                        MaxGrade = row.MaxGrade,
                        ScorePercentage = row.ScorePercentage,
                        Rank = row.Rank,
                        GroupSize = row.GroupSize,
                        IsGraded = row.ScorePercentage.HasValue
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parent sections: offline exams failed (teacher {TeacherId}, roster {RosterId})", teacherId, teacherStudentId);
        }
        return section;
    }
}
