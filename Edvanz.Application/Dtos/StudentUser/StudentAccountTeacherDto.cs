namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// One ACTIVE teacher link on a <see cref="StudentAccountListItemDto"/> row —
/// the teacher's identity plus the per-teacher code this student was assigned
/// under that teacher's roster.
/// </summary>
public class StudentAccountTeacherDto
{
    public long TeacherId { get; set; }

    /// <summary>Teacher's unique, immutable 8-digit code.</summary>
    public string TeacherCode { get; set; } = null!;

    /// <summary>
    /// The per-teacher code (<c>TeacherStudent.StudentCode</c>) this account was
    /// assigned under this teacher's roster. Null when the Active link is not
    /// (or no longer) bound to a roster record — see
    /// <see cref="Edvanz.Domain.Entities.StudentTeacherLink.TeacherStudentId"/>.
    /// </summary>
    public string? StudentCode { get; set; }

    /// <summary>Teacher's full name from the User table.</summary>
    public string TeacherName { get; set; } = null!;
}
