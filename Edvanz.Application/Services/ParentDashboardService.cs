using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ParentUser;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;

namespace Edvanz.Application.Services;

/// <inheritdoc cref="IParentDashboardService"/>
/// <remarks>
/// The per-module section builders were EXTRACTED to <see cref="IParentSectionComposer"/> so the
/// public parent portal composes the same data through the same code path. This service keeps
/// exactly what is specific to the parent MOBILE dashboard: the ownership gate, the teacher
/// header, and the Max/Min/Avg aggregation over the exam percentages. Response shape unchanged.
/// </remarks>
public sealed class ParentDashboardService : IParentDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IParentSectionComposer _sections;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public ParentDashboardService(
        IUnitOfWork unitOfWork,
        IParentSectionComposer sections,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _sections = sections;
        _localizer = localizer;
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
            Videos = await _sections.BuildVideosAsync(teacher.Id, teacherStudentId, vVideo),
            OnlineExams = ToExamReport(
                await _sections.BuildOnlineGradesAsync(teacher.Id, teacherStudentId, language, vOnlineExam)),
            OfflineExams = ToExamReport(
                await _sections.BuildOfflineGradesAsync(teacher.Id, teacherStudentId, language, vOfflineExam)),
            Homework = await _sections.BuildHomeworkAsync(teacher.Id, teacherStudentId, vHomework),
            Attendance = await _sections.BuildAttendanceAsync(teacher.Id, teacherStudentId),
            Payments = await _sections.BuildPaymentsAsync(teacher.Id, teacherStudentId)
        };

        return Result<ParentChildTeacherDashboardDto>.Success(dashboard, _localizer, "Success", HttpStatusCode.OK);
    }

    /// <summary>
    /// §9.2 report shape: Max/Min/Avg over the already-computed percentages of one exam channel.
    /// Rows with no valid percentage (no attempt, no denominator, not graded) are excluded — the
    /// exact filter this service applied before the row-fetching moved into the composer, so the
    /// mobile dashboard's numbers are unchanged. Rounds the average to 2 decimals; leaves Max/Min
    /// at full precision (already a clean 0–100 percentage).
    /// </summary>
    private static ParentDashboardExamReportDto ToExamReport(ParentGradeSectionDto section)
    {
        var report = new ParentDashboardExamReportDto { Visible = section.Visible };

        var percentages = section.Rows
            .Where(r => r.ScorePercentage.HasValue)
            .Select(r => r.ScorePercentage!.Value)
            .ToList();

        report.CompletedExamsCount = percentages.Count;
        if (percentages.Count == 0)
            return report;

        report.AverageGrade = Math.Round(percentages.Average(), 2);
        report.HighestPerformance = percentages.Max();
        report.LowestPerformance = percentages.Min();
        return report;
    }
}
