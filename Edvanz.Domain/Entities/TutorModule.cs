using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities
{
    public class TutorModule
    {

        [ForeignKey(nameof(teacher))]
        public long TutorId { get; set; }
        public Teacher teacher { get; set; }
        [ForeignKey(nameof(module))]
        public long ModuleId { get; set; }
        public Module module { get; set; }

       
    }
}
