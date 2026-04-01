using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.AssistantDtos
{

    public class AssistantListDto
    {
        public long id { get; set; }
        public string fullName { get; set; } = null!;
        public string username { get; set; } = null!;
        public string? email { get; set; }
        public string phoneNumber { get; set; }
        public bool isActive { get; set; }
        public long teacherId { get; set; }
        public string teacherName { get; set; }

    }
}
