using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AssistantSortBy
    {
        CreatedAt,
        fullName,

    }
}
