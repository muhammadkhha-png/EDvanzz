using Edvanz.Domain.Entities.ShareProp;
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
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}