using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Entities;

public class User: BaseEntity
{
   
    public UserType UserType { get; set; }
    public string FullName { get; set; }
    public string Username { get; set; }
    public string PasswordHashed { get; set; }
    public string PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
  
}
