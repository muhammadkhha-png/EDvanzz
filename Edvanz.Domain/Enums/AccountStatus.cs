using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccountStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3
}
