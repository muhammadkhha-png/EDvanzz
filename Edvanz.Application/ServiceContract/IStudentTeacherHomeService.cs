using System.Threading.Tasks;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.StudentUser;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Aggregates the student "teacher home" screen (Figma 232:7033) into ONE call:
/// after a student selects a linked teacher, this composes attendance, payment,
/// videos, online/offline exams and homework — each behind its own teacher
/// visibility toggle — so the frontend renders the whole page from a single read.
/// </summary>
public interface IStudentTeacherHomeService
{
    /// <summary>
    /// Builds the aggregated teacher-home payload for the authenticated student.
    /// The caller must be ACTIVELY linked to and bound under <paramref name="teacherId"/>
    /// (otherwise 403, mirroring every other student read). Each module section is
    /// filled only when the teacher exposes it; hidden sections carry their flag with
    /// empty data. A single module failing degrades that section, never the whole call.
    /// </summary>
    /// <param name="studentUserId">The resolved StudentUser id (from the JWT — never a route/body id).</param>
    /// <param name="teacherId">The selected teacher (route segment).</param>
    /// <param name="year">Optional scoping year (with <paramref name="month"/>); defaults to teacher-local current month.</param>
    /// <param name="month">Optional scoping month 1-12 (with <paramref name="year"/>).</param>
    Task<Result<StudentTeacherHomeDto>> GetTeacherHomeAsync(
        long studentUserId, long teacherId, int? year, int? month);
}
