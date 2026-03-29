using System.ComponentModel;
using System.Text.Json.Serialization;
using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos;

/// <summary>
/// Valid columns to sort the teacher list by.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TeacherSortBy
{
    CreatedAt,
    Name,
    Capacity,
    Code
}


/// <summary>
/// Shared input DTO for paginated list requests.
/// Reusable across all modules (Teachers, Students, Sessions, etc.).
/// </summary>
public class PaginatedRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    /// <summary>
    /// Page number (1-based). Defaults to 1.
    /// </summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>
    /// Number of records per page. Defaults to 20. Max 100.
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 20 : value > 100 ? 100 : value;
    }

    /// <summary>
    /// Optional search term. Searches across: name, username, teacher code,
    /// phone number, and subject (English and Arabic).
    /// Partial match — no need to type the full value.
    /// </summary>
    [Description("Search by: name, username, teacher code, phone number, or subject (partial match, case-insensitive)")]
    public string? Search { get; set; }

    /// <summary>
    /// Column to sort by: CreatedAt, Name, Capacity, Code.
    /// Defaults to CreatedAt.
    /// </summary>
    public TeacherSortBy SortBy { get; set; } = TeacherSortBy.CreatedAt;

    /// <summary>
    /// Sort direction: Asc or Desc. Defaults to Desc.
    /// </summary>
    public SortDirection SortDirection { get; set; } = SortDirection.Desc;

}