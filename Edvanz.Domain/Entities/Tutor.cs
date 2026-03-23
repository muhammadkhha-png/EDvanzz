using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Entities;

public class Tutor
{
    public long UserId { get; set; }
    public User User { get; set; }
    public int StudentCapacity { get; set; }
    public string? LanguagePreference { get; set; }
    public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;
    public DateTime? DeactivatedAt { get; set; }
    public long? CreatedBySuperAdminId { get; set; }
    public User? CreatedBySuperAdmin { get; set; }
}
