using Edvanz.Domain.Entities.ShareProp;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Entities
{
    public class Module:BaseEntity
    {
        public string Name { get; set; }
        public virtual ICollection<TutorModule> OpenModules { get; set; } = new List<TutorModule>();


    }
}
