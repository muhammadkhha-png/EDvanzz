using Edvanz.Application.Excel;
using Edvanz.Application.Reporting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.AuditTrial
{
    public class AuditTrialExcelDto
    {
        public long Id { get; set; }

        [ExcelColumn("Assistant name", 1)]
        [ReportColumn("AuditCol_Assistant", 1)]
        public string AssistantName { get; set; }

        [ExcelColumn("Module", 2)]
        [ReportColumn("AuditCol_Module", 2)]
        public string ModuleName { get; set; }

        [ExcelColumn("Action", 3)]
        [ReportColumn("AuditCol_Action", 3)]
        public string ActionType { get; set; }

        [ExcelColumn("Description", 4)]
        [ReportColumn("AuditCol_Description", 4)]
        public string Desc { get; set; }

        [ExcelColumn("Created At", 5)]
        [ReportColumn("AuditCol_CreatedAt", 5)]
        public DateTime CreatedAt { get; set; }
    }
}
