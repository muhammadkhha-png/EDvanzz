using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Defines how a child was linked to a Parent account.
/// AAM-FR-06.3: Two methods for linking children.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChildLinkMethod
{
    /// <summary>
    /// Method A: Child has a Student User account.
    /// Parent scanned/entered the StudentAccountCode.
    /// The ParentChild record references an existing StudentUser.
    /// </summary>
    StudentAccount = 1,

    /// <summary>
    /// Method B: Child does not have a Student User account.
    /// Parent manually created a child profile by entering a name.
    /// Teachers are added directly under the child profile.
    /// </summary>
    ManualProfile = 2
}