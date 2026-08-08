using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// A child's gender, captured by the Parent during child creation (both Method A and
/// Method B — Parent Module requirements §7). Stored on <see cref="Edvanz.Domain.Entities.ParentChild"/>
/// only; neither <c>StudentUser</c> nor <c>TeacherStudent</c> carries this field today, so
/// <c>ParentChild</c> is the single source of truth for it regardless of link method.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Gender
{
    Male = 1,
    Female = 2
}
