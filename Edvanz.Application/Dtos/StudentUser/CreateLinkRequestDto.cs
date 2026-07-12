using System.ComponentModel.DataAnnotations;

namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// Input DTO for the student's "add teacher" request (request/approval flow —
/// supersedes the 3-credential LinkTeacherDto).
///
/// The student supplies the teacher's public 8-digit code plus their own name;
/// the teacher-assigned student code is OPTIONAL and only used to help the
/// teacher auto-match the request to a roster record at accept time.
/// </summary>
public class CreateLinkRequestDto
{
    /// <summary>
    /// The Teacher's unique 8-digit Teacher Code (AAM-FR-03.3).
    /// The teacher reads it from GET /api/teacher/student-links/my-code and
    /// shares it with their students.
    /// </summary>
    [Required]
    public string TeacherCode { get; set; } = null!;

    /// <summary>
    /// The name the student types so the teacher can identify who is asking.
    /// Shown verbatim on the teacher's pending-requests screen.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string StudentName { get; set; } = null!;

    /// <summary>
    /// Optional: the student code this teacher assigned to the student
    /// (TeacherStudent.StudentCode, e.g. "A5"). When provided and it matches a
    /// roster record, the teacher's accept screen pre-suggests that record.
    /// </summary>
    [MaxLength(20)]
    public string? StudentCode { get; set; }
}