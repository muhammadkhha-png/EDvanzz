using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents a Parent user account in the system. Links to a User record via UserId.
/// AAM-FR-06: Parents register via Google, Apple, or username/password.
/// AAM-FR-06.2: Phone number is mandatory during registration.
/// 
/// A Parent manages one or more children via ParentChild records.
/// AAM-FR-06.4: A Parent can manage multiple children within the same account.
/// AAM-BR-07: A single Parent account can manage multiple children,
///            and each child can be linked to multiple Teachers.
/// </summary>
public class ParentUser : BaseEntity
{
    /// <summary>
    /// Foreign key to the User table. Establishes 1:1 identity link.
    /// The User record holds shared fields: FullName, Username, Email,
    /// PasswordHashed, PhoneNumber, SecurityStamp, UserType, IsActive.
    /// </summary>
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>
    /// Parent's preferred UI language. "en" or "ar".
    /// AAM-FR-02.1: Selected during registration.
    /// AAM-FR-02.3: Changeable from account settings at any time.
    /// </summary>
    public string? LanguagePreference { get; set; }

    /// <summary>
    /// Current account status: Active, Inactive, or Suspended.
    /// Reuses the existing AccountStatus enum shared with Teacher and StudentUser.
    /// </summary>
    public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;

    /// <summary>
    /// Timestamp of account deactivation. Null if account is active.
    /// </summary>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>
    /// Soft-delete timestamp. Null if account is not deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Collection of children managed by this parent.
    /// AAM-FR-06.4: A Parent can manage multiple children.
    /// </summary>
    public ICollection<ParentChild> Children { get; set; } = new List<ParentChild>();
}