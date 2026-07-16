using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

public class User: BaseEntity
{
   
    public UserType UserType { get; set; }
    public string FullName { get; set; }
    public string Username { get; set; }
    [EmailAddress]
    public string? Email { get; set; }
    public string PasswordHashed { get; set; }
    public string  SecurityStamp { get; set; }= Guid.NewGuid().ToString();
    // Phone is optional (nullable). Uniqueness is enforced by a FILTERED unique index
    // (UX_Users_PhoneNumber WHERE PhoneNumber IS NOT NULL) so multiple phone-less users are allowed.
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// FK to the national-ID image's <see cref="FileObject"/> in the central file registry, or
    /// null. References <c>FileObject.Id</c>; served through the gated endpoint (owner + SuperAdmin
    /// only). Replaces the former inline <c>IdImage</c> varbinary column.
    /// </summary>
    public long? IdImageFileId { get; set; }
    public bool? IsActive { get; set; } = true;
    [ForeignKey(nameof(CreateByUser))]
    public long? CreateByUserId { get; set; }
    public User? CreateByUser { get; set; }
    public DateTime CreateAt { get; set; }
    public bool? IsVerified { get; set; } = false;
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiry { get; set; }
    public virtual ICollection<UsersPermission> Permissions { get; set; } = new List<UsersPermission>();

    /// <summary>
    /// In-app notifications delivered to this user (push notifications only per D-05).
    /// WhatsApp messages are NOT stored here — they use the Messaging module's MessageLog.
    /// </summary>
    public ICollection<UserNotification> Notifications { get; set; }
        = new List<UserNotification>();

    /// <summary>
    /// Firebase FCM device tokens registered for this user.
    /// Supports multiple devices per user (phone + tablet). EC-12.
    /// </summary>
    public ICollection<UserDeviceToken> DeviceTokens { get; set; }
        = new List<UserDeviceToken>();
}
