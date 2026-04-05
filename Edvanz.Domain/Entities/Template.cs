using Edvanz.Domain.Entities.ShareProp;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities
{
    public class Template:BaseEntity
    {
        public string Name { get; set; }


        [ForeignKey(nameof(Teacher))]
        public long TeacherId { get; set; }
        public Teacher Teacher { get; set; }
        public virtual ICollection<TemplatePermisions> Profiles { get; set; } = new List<TemplatePermisions>();

    }
}
