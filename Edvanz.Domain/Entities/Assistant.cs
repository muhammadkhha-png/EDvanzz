using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

public class Assistant: BaseEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    [ForeignKey(nameof(Tutor))]
    public long TutorAccountId { get; set; }
    public Tutor Tutor { get; set; } 


    public bool IsActive { get; set; } = true;
}
