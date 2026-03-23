namespace Edvanz.Domain.Entities;

public class Assistant
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid TutorAccountId { get; set; }
    public Tutor Tutor { get; set; } = null!;

    //public long PermissionProfileId { get; set; }
    //public PermissionProfile PermissionProfile { get; set; } = null!;

    // Who created this assistant
    public long? CreatedByTutorId { get; set; }
    public User? CreatedByTutor { get; set; }

    public bool IsActive { get; set; } = true;
}
