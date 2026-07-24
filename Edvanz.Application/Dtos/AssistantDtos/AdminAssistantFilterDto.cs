using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.AssistantDtos
{
    /// <summary>
    /// Filter/paging request for GET /api/assistant/all (Admin Portal — SuperAdmin only).
    /// </summary>
    public class AdminAssistantFilterDto
    {
        /// <summary>Optional — restrict to a single teacher's assistants.</summary>
        public long? teacherId { get; set; }

        /// <summary>Optional — matches assistant full name OR phone number.</summary>
        public string? search { get; set; }

        public AssistantSortBy sortBy { get; set; } = AssistantSortBy.CreatedAt;
        public SortDirection sortDirection { get; set; } = SortDirection.Desc;

        private int _page = 1;
        private int _pageSize = 20;

        public int Page
        {
            get => _page;
            set => _page = value < 1 ? 1 : value;
        }

        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value < 1 ? 20 : value > 100 ? 100 : value;
        }
    }
}