using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities
{
    public class AssisstantPemisions
    {
        [ForeignKey(nameof(Assistant))]
        public long UserId { get; set; }
        public User Assistant { get; set; }
        [ForeignKey(nameof(permission))]
        public long PermissionId { get; set; }
        public Permission permission { get; set; }
    }
}
