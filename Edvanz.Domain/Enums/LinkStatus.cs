using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Defines the status of a Student-to-Teacher link.
/// AAM-FR-05.5: Link is created when student provides valid credentials.
/// Active = link is live, student can see teacher's data.
/// Unlinked = student removed the teacher from their dashboard.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LinkStatus
{
    Active = 1,
    Unlinked = 2
}