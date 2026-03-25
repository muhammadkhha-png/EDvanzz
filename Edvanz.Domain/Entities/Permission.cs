using Edvanz.Domain.Entities.ShareProp;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Entities
{
    public class Permission:BaseEntity
    {
        public string Name { get; set; }
        public string Module { get; set; }
    }
}
