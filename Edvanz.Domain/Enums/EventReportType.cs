using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies the type of event payment report to generate.
/// REQ-EVT-023 through REQ-EVT-026: Event report types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventReportType : byte
{
    /// <summary>
    /// REQ-EVT-023: Single Event Payment Report.
    /// </summary>
    SingleEvent = 1,

    /// <summary>
    /// REQ-EVT-024: All Events Summary Report.
    /// </summary>
    AllEventsSummary = 2
}