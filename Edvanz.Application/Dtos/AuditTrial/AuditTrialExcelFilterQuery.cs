using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.AuditTrial
{
    public class AuditTrialExcelFilterQuery
    {
        public long teacherID { get; set; }
        public string? AssistantName { get; set; }
        public string? ActionType { get; set; }
        public string? Module { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        /// <summary>
        /// IANA timezone id supplied by the client (e.g. "Africa/Cairo") so the exported
        /// file renders timestamps and interprets the From/To window in the user's local
        /// zone. Falls back to Africa/Cairo when null or unrecognized. (REQ-USR-030)
        /// </summary>
        public string? TimeZoneId { get; set; }
    }
}
