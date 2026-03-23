using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Entities;

public class User
{
    public long Id { get; set; }
    public UserType UserType { get; set; }
    public string FullName { get; set; }
    public string Username { get; set; }
    public string PasswordHashed { get; set; }
    public string PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
