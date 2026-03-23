using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Edvanz.Domain.Entities.ShareProp
{
    public class BaseEntity
    {
        [Key]
        public long Id { get; set; }
        public DateTime CreateAt { get; set; }
        [ForeignKey(nameof(CreateByUser))]
        public long? CreateByUserId { get; set; }
        public User? CreateByUser { get; set; }
    }
}
