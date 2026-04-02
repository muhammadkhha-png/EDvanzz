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

    public string? PhoneNumber { get; set; }
    public byte[]? IdImage { get; set; }
    public bool? IsActive { get; set; } = true;
    [ForeignKey(nameof(CreateByUser))]
    public long? CreateByUserId { get; set; }
    public User? CreateByUser { get; set; }
    public DateTime CreateAt { get; set; }
    public bool? IsVerified { get; set; } = false;
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiry { get; set; }
    public virtual ICollection<UsersPermission> Permissions { get; set; } = new List<UsersPermission>();
}
