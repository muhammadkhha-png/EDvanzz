using System.ComponentModel;

namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// Paginated request for the SuperAdmin "Student Accounts" list
/// (<c>GET /api/studentuser/list</c>). Deliberately its own request type rather
/// than the shared <see cref="PaginatedRequest"/> — that type's <c>SortBy</c> is
/// typed to the Teacher-specific sort enum and doesn't apply here, same reason
/// the Student Module's own <c>StudentListRequest</c> (TeacherStudentDtos.cs)
/// doesn't reuse it either.
/// </summary>
public class StudentAccountListRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    /// <summary>Page number (1-based). Defaults to 1.</summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Records per page. Defaults to 20. Max 100.</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 20 : value > 100 ? 100 : value;
    }

    /// <summary>
    /// Search term matched against the account's full name and, for each of its
    /// ACTIVE teacher links, the per-teacher student code assigned by that
    /// teacher. Partial match, case-insensitive.
    /// </summary>
    [Description("Search by: student name, or per-teacher student code (partial match)")]
    public string? Search { get; set; }

    /// <summary>
    /// Optional filter: only return accounts holding an ACTIVE link to this
    /// Teacher.Id.
    /// </summary>
    public long? TeacherId { get; set; }
}
