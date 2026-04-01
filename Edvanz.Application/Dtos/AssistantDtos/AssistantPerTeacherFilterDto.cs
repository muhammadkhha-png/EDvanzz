using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Serialization;

namespace Edvanz.Application.Dtos.AssistantDtos
{
  

    public class AssistantPerTeacherFilterDto
    {
        public long teacherId { get; set; }
        public bool?  isAcitve { get; set; }
        public string? fullName { get; set; }
        public string? username { get; set; }
        public AssistantSortBy sortBy { get; set; } = AssistantSortBy.CreatedAt;
        public SortDirection sortDirection { get; set; } = SortDirection.Desc;
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

    }
}
