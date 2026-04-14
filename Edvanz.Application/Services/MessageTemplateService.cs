using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.MessageTemplateComponents;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    public class MessageTemplateService : IMessageTemplateService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStringLocalizer<Messages> _localizer;

        public MessageTemplateService(IUnitOfWork unitOfWork, IStringLocalizer<Messages> localizer)
        {
            _unitOfWork = unitOfWork;
            _localizer = localizer;
        }

        // ── GET ALL per teacher ───────────────────────────────────────────────────
        public async Task<Result<PaginatedResponse<List<MessageTemplateSummaryDto>>>> GetAllAsync(TemplatesfilterReqDto req)
        {
            var (templates, count) = await _unitOfWork.messageTemplateRepo
                .GetAllPerTeacherAsync(
                    req.teacherID,
                    req.templateName,
                    req.page,
                    req.pageSize);

            var data = templates.Select(t => new MessageTemplateSummaryDto
            {
                Id = t.Id,
                Name = t.Name,
                Channel = t.Channel,
                RecipientTarget = t.RecipientTarget,
                IsActive = t.IsActive,
                BlockCount = t.Blocks.Count,
                UpdatedAt = t.UpdatedAt,
            }).ToList();

            int page = req.page ?? 1;
            int pageSize = req.pageSize ?? count;

            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = count;

            int totalPages = pageSize == 0 ? 1 : (int)Math.Ceiling(count / (double)pageSize);

            var response = new PaginatedResponse<List<MessageTemplateSummaryDto>>
            {
                totalCount = count,
                page = page,
                pageSize = pageSize,
                totalPages = totalPages,
                data = data
            };

            return Result<PaginatedResponse<List<MessageTemplateSummaryDto>>>
                .Success(response, _localizer);
        }

        // ── GET BY ID ─────────────────────────────────────────────────
        public async Task<Result<MessageTemplateDetailDto>> GetByIdAsync(long teacherId, long templateId)
        {
            var template = await _unitOfWork.messageTemplateRepo.GetByIdWithBlocksAsync(templateId);

            if (template is null || template.TeacherId != teacherId)
                return Result<MessageTemplateDetailDto>.Failure(_localizer, "TemplateNotFound");

            return Result<MessageTemplateDetailDto>.Success(MapToDetail(template), _localizer);
        }

        // ── CREATE ───────────────────
        public async Task<Result<string>> CreateAsync(long teacherId, CreateMessageTemplateDto dto)
        {
            if (dto.blocks is not { Count: > 0 })
                return Result<string>.Failure(_localizer, "TemplateMustHaveAtLeastOneBlock");

            ValidateBlocks(dto.blocks, out var blockError);
            if (blockError is not null)
                return Result<string>.Failure(_localizer, blockError);

            var nameExists = await _unitOfWork.messageTemplateRepo
                .NameExistsAsync(teacherId, dto.name.Trim());
            if (nameExists)
                return Result<string>.Failure(_localizer, "TemplateNameAlreadyExists");

            var template = new MessageTemplate
            {
                TeacherId = teacherId,
                Name = dto.name.Trim(),
                Channel = dto.channel,
                RecipientTarget = dto.recipientTarget,
                IsActive = true,
                CreateAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _unitOfWork.messageTemplateRepo.AddAsync(template);
                await _unitOfWork.SaveChangesAsync();   // get template.Id

                var blocks = BuildBlocks(dto.blocks, template.Id);
                await _unitOfWork.GetRepository<MessageBlock, long>().AddRangeAsync(blocks);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return Result<string>.Success("TemplateCreated", _localizer);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw ;
            }
        }

        // ── UPDATE ────────────────────────────────────────────────────
        public async Task<Result<string>> UpdateAsync(long teacherId, UpdateMessageTemplateDto dto)
        {
            var template = await _unitOfWork.messageTemplateRepo.GetByIdWithBlocksAsync(dto.templateId);

            if (template is null || template.TeacherId != teacherId)
                return Result<string>.Failure(_localizer, "TemplateNotFound");

            // name uniqueness (excluding self)
            if (!string.IsNullOrWhiteSpace(dto.name) && dto.name.Trim() != template.Name)
            {
                var nameExists = await _unitOfWork.messageTemplateRepo
                    .NameExistsAsync(teacherId, dto.name.Trim(), excludeId: dto.templateId);
                if (nameExists)
                    return Result<string>.Failure(_localizer, "TemplateNameAlreadyExists");
            }

            if (dto.blocks is not null)
            {
                if (dto.blocks.Count == 0)
                    return Result<string>.Failure(_localizer, "TemplateMustHaveAtLeastOneBlock");

                ValidateBlocks(dto.blocks, out var blockError);
                if (blockError is not null)
                    return Result<string>.Failure(_localizer, blockError);
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                if (!string.IsNullOrWhiteSpace(dto.name)) template.Name = dto.name.Trim();
                if (dto.channel.HasValue) template.Channel = dto.channel.Value;
                if (dto.recipientTarget.HasValue) template.RecipientTarget = dto.recipientTarget.Value;
                template.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.messageTemplateRepo.UpdateAsync(template);

                // Full replace blocks if provided
                if (dto.blocks is not null)
                {
                    var existing = await _unitOfWork.GetRepository<MessageBlock, long>()
                        .GetAsync(b => b.MessageTemplateId == dto.templateId);

                    if (existing.Any())
                        await _unitOfWork.GetRepository<MessageBlock, long>().DeleteRangeAsync(existing);

                    var newBlocks = BuildBlocks(dto.blocks, dto.templateId);
                    await _unitOfWork.GetRepository<MessageBlock, long>().AddRangeAsync(newBlocks);
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return Result<string>.Success("TemplateUpdated", _localizer);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        // ── DELETE ─────
        // Cannot delete if active triggers reference this template
        public async Task<Result<string>> DeleteAsync(long teacherId, long templateId)
        {
            var template = await _unitOfWork.messageTemplateRepo.GetByIdWithBlocksAsync(templateId);

            if (template is null || template.TeacherId != teacherId)
                return Result<string>.Failure(_localizer, "TemplateNotFound");

            var hasActiveTriggers = await _unitOfWork.GetRepository<AutomatedTrigger, long>()
                .AnyAsync(t => t.MessageTemplateId == templateId && t.IsActive);

            if (hasActiveTriggers)
                return Result<string>.Failure(_localizer, "CannotDeleteTemplateWithActiveTriggers");

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Delete blocks first
                var blocks = await _unitOfWork.GetRepository<MessageBlock, long>()
                    .GetAsync(b => b.MessageTemplateId == templateId);

                if (blocks.Any())
                    await _unitOfWork.GetRepository<MessageBlock, long>().DeleteRangeAsync(blocks);

                await _unitOfWork.messageTemplateRepo.DeleteAsync(template);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return Result<string>.Success("TemplateDeleted", _localizer);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

       

        // ── TOGGLE ACTIVE ─────────────────────────────────────────────
        public async Task<Result<string>> ToggleActiveAsync(long teacherId, long templateId, bool activate)
        {
            var template = await _unitOfWork.messageTemplateRepo.GetByIdWithBlocksAsync(templateId);

            if (template is null || template.TeacherId != teacherId)
                return Result<string>.Failure(_localizer, "TemplateNotFound");

            if (template.IsActive == activate)
                return Result<string>.Failure(_localizer,
                    activate ? "TemplateAlreadyActive" : "TemplateAlreadyInactive");

            template.IsActive = activate;
            template.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.messageTemplateRepo.UpdateAsync(template);
            await _unitOfWork.SaveChangesAsync();

            return Result<string>.Success(
                activate ? "TemplateActivated" : "TemplateDeactivated", _localizer);
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static List<MessageBlock> BuildBlocks(List<MessageBlockDto> dtos, long templateId)
        {
            return dtos
                .OrderBy(b => b.SortOrder)
                .Select((b, i) => new MessageBlock
                {
                    MessageTemplateId = templateId,
                    BlockType = b.BlockType,
                    DynamicKey = b.BlockType == BlockType.Dynamic ? b.DynamicKey : null,
                    CustomText = b.BlockType == BlockType.CustomText ? b.CustomText : null,
                    SortOrder = b.SortOrder > 0 ? b.SortOrder : i + 1,
                    CreateAt = DateTime.UtcNow,
                })
                .ToList();
        }

        private static void ValidateBlocks(List<MessageBlockDto> blocks, out string? error)
        {
            error = null;

            foreach (var block in blocks)
            {
                if (block.BlockType == BlockType.Dynamic && block.DynamicKey is null)
                {
                    error = "DynamicBlockMustHaveAKey";
                    return;
                }
                if (block.BlockType == BlockType.CustomText
                    && string.IsNullOrWhiteSpace(block.CustomText))
                {
                    error = "CustomTextBlockCannotBeEmpty";
                    return;
                }
            }
        }

        private static MessageTemplateDetailDto MapToDetail(MessageTemplate t)
        {
            var orderedBlocks = t.Blocks
                .OrderBy(b => b.SortOrder)
                .Select(b => new MessageBlockResponseDto
                {
                    Id = b.Id,
                    BlockType = b.BlockType,
                    DynamicKey = b.DynamicKey,
                    CustomText = b.CustomText,
                    SortOrder = b.SortOrder,
                    PreviewLabel = b.BlockType == BlockType.CustomText
                        ? b.CustomText ?? ""
                        : $"[{b.DynamicKey}]",   // e.g. "[StudentName]"
                })
                .ToList();

            // Build preview string (REQ-MSG-013)
            var preview = string.Join(" ", orderedBlocks.Select(b => b.PreviewLabel));

            return new MessageTemplateDetailDto
            {
                Id = t.Id,
                Name = t.Name,
                Channel = t.Channel,
                RecipientTarget = t.RecipientTarget,
                IsActive = t.IsActive,
                Blocks = orderedBlocks,
                Preview = preview,
                CreatedAt = t.CreateAt,
                UpdatedAt = t.UpdatedAt,
            };
        }
    }

}
