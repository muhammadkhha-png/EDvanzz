using System.ComponentModel.DataAnnotations;

namespace Edvanz.Application.Dtos.AssistantDtos
{
    public class UpdateAssistantRequest
    {
        public long assistantId { get; set; }
        [MaxLength(150)]
        public string? fullName { get; set; }

        [MaxLength(50)]
        public string? username { get; set; }

        public string? newPassword { get; set; }
        public string? phoneNumber { get; set; }
        [EmailAddress]
        public string? email { get; set; }


        public List<long>? permissionProfileIds { get; set; }

        public List<long>? permissionIds { get; set; } = new List<long>();
    }
}
