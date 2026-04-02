using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents an Assistant account operating under a Teacher's account.
/// AAM-BR-01: Assistants can only be created by a Teacher.
/// BR-USR-005: An assistant can only interact with data belonging to their owning teacher.
/// </summary>
public class Assistant : BaseEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    [ForeignKey(nameof(Teacher))]
    public long TeacherAccountId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public string? LanguagePreference { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;


    /// <summary>
    /// Soft-delete timestamp. Null if account is not deleted.
    /// REQ-ADM-020 through 024: Data preservation period before permanent removal.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
    public virtual ICollection<TemplatePermissionsUsers> PermissionProfiles { get; set; } = new List<TemplatePermissionsUsers>();


}