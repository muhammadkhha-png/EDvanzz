using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Junction table linking an Assistant to their granted Permissions.
/// REQ-USR-010: No default permissions; each must be explicitly granted.
/// </summary>
public class UsersPermission
{
    [ForeignKey(nameof(User))]
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    [ForeignKey(nameof(Permission))]
    public long PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}