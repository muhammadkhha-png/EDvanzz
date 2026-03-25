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
    public string Email { get; set; }
    public string PasswordHashed { get; set; }
    public string  SecurityStamp { get; set; }

    public string PhoneNumber { get; set; }

    public bool IsActive { get; set; } = true;
    [ForeignKey(nameof(CreateByUser))]
    public long? CreateByUserId { get; set; }
    public User? CreateByUser { get; set; }

}
