using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// The mobile platform a Firebase FCM token belongs to.
/// Used by UserDeviceToken records so the push sender can apply
/// platform-specific payload formatting if required.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DevicePlatform : byte
{
    Android = 1,
    iOS = 2
}
