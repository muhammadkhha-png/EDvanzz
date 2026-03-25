using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities
{
    public class UsersTutor
    {
        [ForeignKey(nameof(user))]
        public long userId { get; set; }
        public User user { get; set; }
        [ForeignKey(nameof(Tutor))]
        public long TutorId { get; set; }
        public User Tutor { get; set; }
    }
}
