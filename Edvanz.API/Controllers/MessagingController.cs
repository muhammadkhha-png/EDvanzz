using DocumentFormat.OpenXml.Office2010.Excel;
using Edvanz.API.Attributes;
using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ChannelDtos;
using Edvanz.Application.Dtos.DispatcherDtos;
using Edvanz.Application.Dtos.MessageLogDtos;
using Edvanz.Application.Dtos.MessageTemplateComponents;
using Edvanz.Application.Dtos.TriggerDtos;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Edvanz.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [ServiceFilter(typeof(Edvanz.API.Filters.TenantScopeFilter))]
    public class MessagingController : ApiBaseController
    {
        private readonly IMessagingChannelService _channelService;
        private readonly IMessageTemplateService _templateService;
        private readonly IAutomatedTriggerService _triggerService;
        private readonly IMessageDispatcher _dispatcher;
        private readonly IMessageLogService _logService;
        private readonly ICurrentUserService _currentUser;

        public MessagingController(
            IMessagingChannelService channelService,
            IMessageTemplateService templateService,
            IAutomatedTriggerService triggerService,
            IMessageDispatcher dispatcher,
            IMessageLogService logService,
            ICurrentUserService currentUser)
        {
            _channelService = channelService;
            _templateService = templateService;
            _triggerService = triggerService;
            _dispatcher = dispatcher;
            _logService = logService;
            _currentUser = currentUser;
        }

        #region channels

        /// GET /api/messaging/channels
        /// Returns status of all configured channels 
        [HttpGet("channels/{teacherID}")]
        [ModulePermission("Messaging", "ConfigureChannels", ["Teacher", "SuperAdmin"])]

        public async Task<IActionResult> GetChannels(long teacherID)
            => ToResponse(await _channelService.GetAllChannelsAsync(teacherID));


        /// GET /api/messaging/channels/{type}
        [HttpGet("channels/get-by-type")]
        [ModulePermission("Messaging", "ConfigureChannels", ["Teacher", "SuperAdmin"])]

        public async Task<IActionResult> GetChannel([FromQuery]ChannelReqDto req)
            => ToResponse(await _channelService.GetChannelAsync(req.teacherID, req.type));


        /// POST /api/messaging/channels
        /// Connect / reconfigure a channel 
        /// Access: Teacher only 
        [HttpPost("channels/conf-reconfigure")]
        [ModulePermission("Messaging", "ConfigureChannels", ["Teacher","SuperAdmin"])]
        public async Task<IActionResult> UpsertChannel([FromBody] UpsertChannelDto dto)
            => ToResponse(await _channelService.UpsertChannelAsync(dto.teacherID, dto));


        /// PATCH /api/messaging/channels/{type}/activate
        [HttpPatch("channels/activate")]
        [ModulePermission("Messaging", "ConfigureChannels", ["Teacher", "SuperAdmin"])]
        public async Task<IActionResult> ActivateChannel(ChannelReqDto req)
            => ToResponse(await _channelService.ToggleChannelAsync(req.teacherID, req.type, true));


        /// PATCH /api/messaging/channels/{type}/deactivate
        [HttpPatch("channels/deactivate")]
        [ModulePermission("Messaging", "ConfigureChannels", ["Teacher", "SuperAdmin"])]

        public async Task<IActionResult> DeactivateChannel(ChannelReqDto req)
            => ToResponse(await _channelService.ToggleChannelAsync(req.teacherID, req.type, false));


        /// DELETE /api/messaging/channels/{type}
        [HttpDelete("channels/{type}")]
        [ModulePermission("Messaging", "ConfigureChannels", ["Teacher", "SuperAdmin"])]
        public async Task<IActionResult> DisconnectChannel(ChannelReqDto req)
            => ToResponse(await _channelService.DisconnectChannelAsync(req.teacherID, req.type));
        #endregion

        #region TEMPLATES

        /// GET /api/messaging/templates
        [HttpGet("templates")]
        [ModulePermission("Messaging", "Messaging.ManageTemplates", ["Teacher", "SuperAdmin", "Assistant"])]
        public async Task<IActionResult> GetTemplates([FromQuery]TemplatesfilterReqDto req)
            => ToResponse(await _templateService.GetAllAsync(req));


        /// GET /api/messaging/templates/{id}
        [HttpGet("templates/details")]
        [ModulePermission("Messaging", "Messaging.ManageTemplates", ["Teacher", "SuperAdmin", "Assistant"])]

        public async Task<IActionResult> GetTemplate([FromQuery]MessageTemplateReq req)
            => ToResponse(await _templateService.GetByIdAsync(req.teacherId, req.templateId));

        /// POST /api/messaging/templates
        [HttpPost("templates")]
        [ModulePermission("Messaging", "Messaging.ManageTemplates", ["Teacher", "SuperAdmin", "Assistant"])]

        public async Task<IActionResult> CreateTemplate([FromBody] CreateMessageTemplateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return ToResponse(await _templateService.CreateAsync(dto.teacherID, dto));
        }

        /// PUT /api/messaging/templates/{id}
        [HttpPut("templates")]
        [ModulePermission("Messaging", "Messaging.ManageTemplates", ["Teacher", "SuperAdmin", "Assistant"])]
        public async Task<IActionResult> UpdateTemplate( [FromBody] UpdateMessageTemplateDto dto)
        {
           
            return ToResponse(await _templateService.UpdateAsync(dto.teacherID, dto));
        }


        /// DELETE /api/messaging/templates/{id}
        [HttpDelete("templates")]
        [ModulePermission("Messaging", "Messaging.ManageTemplates", ["Teacher", "SuperAdmin", "Assistant"])]
        public async Task<IActionResult> DeleteTemplate(MessageTemplateReq req)
            => ToResponse(await _templateService.DeleteAsync(req.teacherId, req.templateId));

        /// PATCH /api/messaging/templates/{id}/activate
        [HttpPatch("templates/activate")]
        [ModulePermission("Messaging", "Messaging.ManageTemplates", ["Teacher", "SuperAdmin", "Assistant"])]
        public async Task<IActionResult> ActivateTemplate(MessageTemplateReq req)
            => ToResponse(await _templateService.ToggleActiveAsync(req.teacherId, req.templateId, true));

        /// PATCH /api/messaging/templates/{id}/deactivate
        [HttpPatch("templates/deactivate")]
        [ModulePermission("Messaging", "Messaging.ManageTemplates", ["Teacher", "SuperAdmin", "Assistant"])]
        public async Task<IActionResult> DeactivateTemplate(MessageTemplateReq req)
            => ToResponse(await _templateService.ToggleActiveAsync(req.teacherId, req.templateId, false));
        #endregion

        #region AUTOMATED TRIGGERS
        /// GET /api/messaging/triggers
        [HttpGet("triggers/all")]
        [ModulePermission("Messaging", "Messaging.ConfigureAutomatedTriggers", ["Teacher", "SuperAdmin", "Assistant"])]
        public async Task<IActionResult> GetTriggers([FromQuery]TriggerFiltetrReq req)
            => ToResponse(await _triggerService.GetPagedAsync(req));

        /// GET /api/messaging/triggers/{id}
        [HttpGet("trigger/details")]
        [ModulePermission("Messaging", "Messaging.ConfigureAutomatedTriggers", ["Teacher", "SuperAdmin", "Assistant"])]
        public async Task<IActionResult> GetTrigger(TriggerReqDto req)
            => ToResponse(await _triggerService.GetByIdAsync(req.teacherId, req.triggerId));

        /// POST /api/messaging/triggers
        [HttpPost("triggers")]
        [ModulePermission("Messaging", "Messaging.ConfigureAutomatedTriggers", ["Teacher", "SuperAdmin", "Assistant"])]
        public async Task<IActionResult> CreateTrigger([FromBody] CreateTriggerDto dto)
            => ToResponse(await _triggerService.CreateAsync(dto.teacherId, dto));

        /// PUT /api/messaging/triggers/{id}
        [HttpPut("triggers/{id:long}")]
        [ModulePermission("Messaging", "Messaging.ConfigureAutomatedTriggers", ["Teacher", "SuperAdmin", "Assistant"])]
        public async Task<IActionResult> UpdateTrigger(long id, [FromBody] UpdateTriggerDto dto)
        {
            if (dto.TriggerId != id)
            {
                return ToResponse(Result<string>.Failure("Route id does not match body id"));
            }

            dto.TriggerId = id;

            return ToResponse(await _triggerService.UpdateAsync(dto.teacherId, dto));
        }

        /// DELETE /api/messaging/triggers/{id}
        [HttpDelete("triggers/{id:long}")]
        [ModulePermission("Messaging", "Messaging.ConfigureAutomatedTriggers", ["Teacher", "SuperAdmin", "Assistant"])]
        public async Task<IActionResult> DeleteTrigger(long id,TriggerReqDto req)
        {
            if (req.triggerId != id)
            {
                return ToResponse(Result<string>.Failure("Route id does not match body id"));
            }
            return ToResponse(await _triggerService.DeleteAsync(req.teacherId, req.triggerId));

        }

        /// PATCH /api/messaging/triggers/{id}/activate
        [HttpPatch("triggers/{id:long}/activate")]
        [ModulePermission("Messaging", "Messaging.ConfigureAutomatedTriggers", ["Teacher", "SuperAdmin", "Assistant"])]

        public async Task<IActionResult> ActivateTrigger(long id ,TriggerReqDto req)
        {
            if (req.triggerId != id)
            {
                return ToResponse(Result<string>.Failure("Route id does not match body id"));
            }
            return ToResponse(await _triggerService.ToggleAsync(req.teacherId, req.triggerId, true));
        }

        /// PATCH /api/messaging/triggers/{id}/deactivate
        [HttpPatch("triggers/{id:long}/deactivate")]
        [ModulePermission("Messaging", "Messaging.ConfigureAutomatedTriggers", ["Teacher", "SuperAdmin", "Assistant"])]

        public async Task<IActionResult> DeactivateTrigger(long id ,TriggerReqDto req)
        {
            if (req.triggerId != id)
            {
                return ToResponse(Result<string>.Failure("Route id does not match body id"));
            }
            return ToResponse(await _triggerService.ToggleAsync(req.teacherId, req.triggerId, false)); 
        }
        #endregion

        #region MANUAL MESSAGING
        /// POST /api/messaging/send
        /// Sends a manual message — returns preview + recipient count for confirmation 
        [HttpPost("send")]
        [ModulePermission("Messaging", "SendManual")]
        public async Task<IActionResult> SendManual([FromBody] ManualMessageRequests request)
        {
          
            return ToResponse(await _dispatcher.DispatchManualAsync(request));
        }

       // MESSAGE HISTORY

        /// GET /api/messaging/history
        [HttpGet("history")]
        [ModulePermission("Messaging", "ViewHistory")]
        public async Task<IActionResult> GetHistory([FromQuery] MessageLogQueryDto query)
        {
            
            return ToResponse(await _logService.GetHistoryAsync(query));
        }

        /// POST /api/messaging/history/{id}/resend
        [HttpPost("history/{id:long}/resend")]
        [ModulePermission("Messaging", "SendManual")]
        public async Task<IActionResult> Resend(MessageLogRequestDto req)
            => ToResponse(await _logService.ResendAsync(req.teacherId, req.messageLogId));

    #endregion
}
}
