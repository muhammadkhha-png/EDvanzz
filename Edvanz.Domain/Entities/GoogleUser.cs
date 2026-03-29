using Edvanz.Domain.Entities.ShareProp;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities
{
    public class GoogleUser:BaseEntity
    {
        public string GoogleId { get; set; }
        public string Email { get; set; }
        [ForeignKey(nameof(user))]
        public long? UserId { get; set; }
        public User user { get; set; }
        public bool IsCompleted { get; set; } =false;
    }
}
