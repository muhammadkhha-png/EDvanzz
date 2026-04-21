using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// The channel through which a subscription payment was processed.
/// REQ-SUB-017: Paymob-integrated payments vs manually-submitted payments.
/// Distinct from PaymentMethod: a VodafoneCash payment can arrive via Paymob
/// (auto-confirmed) or Manual (super admin approves).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentChannel : byte
{
    /// <summary>
    /// Payment processed end-to-end by Paymob. Auto-confirmed via webhook.
    /// Stub in v1 (D-04): always falls back to Manual until Paymob credentials are provisioned.
    /// </summary>
    Paymob = 1,

    /// <summary>
    /// Tutor submitted a transaction reference manually; super admin approved.
    /// Default real path for v1.
    /// </summary>
    Manual = 2,

    /// <summary>
    /// Super admin created the subscription directly (REQ-ADM-012, REQ-ADM-015, REQ-ADM-016).
    /// No tutor-initiated payment flow involved.
    /// </summary>
    SuperAdminOverride = 3
}
