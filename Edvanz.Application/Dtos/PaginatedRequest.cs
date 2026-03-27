namespace Edvanz.Application.Dtos;

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
    /// Optional search term for filtering by name, code, etc.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Column to sort by. Null uses default sort.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort direction: "asc" or "desc". Defaults to "asc".
    /// </summary>
    public string SortDirection { get; set; } = "asc";

    /// <summary>
    /// Whether sort direction is descending.
    /// </summary>
    public bool IsDescending => SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
}