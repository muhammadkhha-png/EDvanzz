using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos
{
    public class ToggleAccountStatus
    {
        public long accountId { get; set; }
        public AccountStatus targetStatus { get; set; }
    }
}
