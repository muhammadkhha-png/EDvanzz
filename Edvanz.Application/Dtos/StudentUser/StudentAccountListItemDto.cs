namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// One row on the SuperAdmin "Student Accounts" list
/// (<c>GET /api/studentuser/list</c>): the account's identity plus every
/// teacher it currently holds an ACTIVE link to.
/// </summary>
public class StudentAccountListItemDto
{
    /// <summary>StudentUser.Id.</summary>
    public long StudentAccountId { get; set; }

    /// <summary>Account holder's full name, from the User table.</summary>
    public string FullName { get; set; } = null!;

    /// <summary>Account username, from the User table.</summary>
    public string UserName { get; set; } = null!;
    public string AccountCode { get; set; } = null!;


    public string? PhoneNumber { get; set; }

    /// <summary>
    /// UTC timestamp of the account's most recent successful login, or null if it has
    /// never logged in. Sourced from User.LastLoginAt.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Every teacher this account currently holds an ACTIVE
    /// <see cref="Edvanz.Domain.Entities.StudentTeacherLink"/> to. Empty when the
    /// account has never linked to a teacher, or all its links are
    /// Pending/terminal (Rejected/Unlinked/RemovedByTeacher/CancelledByStudent).
    /// </summary>
    public List<StudentAccountTeacherDto> Teachers { get; set; } = new();
}
