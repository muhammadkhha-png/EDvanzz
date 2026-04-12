using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.MessageTemplateComponents
{
    public class TemplatesfilterReqDto
    {
        public long teacherID { get; set; }
        public string? templateName { get; set; }
        public int? page { get; set; }
        public int? pageSize { get; set; }
    }
}
