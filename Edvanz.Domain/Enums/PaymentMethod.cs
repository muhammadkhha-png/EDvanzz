using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Payment methods supported for subscription renewal.
/// REQ-SUB-017: Vodafone Cash and InstaPay are the two user-facing methods.
/// SuperAdminManual represents manual activations by super admin that bypass payment
/// (REQ-ADM-012: super admin can activate subscriptions without payment).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentMethod : byte
{
    /// <summary>
    /// Tutor paid via Vodafone Cash wallet.
    /// </summary>
    VodafoneCash = 1,

    /// <summary>
    /// Tutor paid via InstaPay.
    /// </summary>
    InstaPay = 2,

    /// <summary>
    /// Super admin activated the subscription without a payment (REQ-ADM-012).
    /// Used for onboarding waivers, promotional periods, or operational overrides.
    /// </summary>
    SuperAdminManual = 3
}
