using Edvanz.Application.Dtos.MessageResolver;
using Edvanz.Application.IservicesContract;
using Edvanz.Domain.Entities.Messaging;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    public class BlockResolver : IBlockResolver
    {
        public string Resolve(IEnumerable<MessageBlock> blocks, MessageResolveContext context)
        {
            var parts = blocks
                .OrderBy(b => b.SortOrder)
                .Select(b => ResolveBlock(b, context));

            return string.Join(" ", parts).Trim();
        }

        private static string ResolveBlock(MessageBlock block, MessageResolveContext ctx)
        {
            if (block.BlockType == BlockType.CustomText)
                return block.CustomText ?? string.Empty;

            return block.DynamicKey switch
            {
                DynamicBlockKey.StudentName => ctx.StudentName,
                DynamicBlockKey.StudentCode => ctx.StudentCode,
                DynamicBlockKey.SessionName => ctx.SessionName ?? string.Empty,
                DynamicBlockKey.Date => ctx.Date.ToString("dd/MM/yyyy"),
                DynamicBlockKey.AttendanceStatus => ctx.AttendanceStatus ?? string.Empty,
                DynamicBlockKey.AbsenceCount => ctx.AbsenceCount?.ToString() ?? string.Empty,
                DynamicBlockKey.PaymentStatus => ctx.PaymentStatus ?? string.Empty,
                DynamicBlockKey.PaymentAmount => ctx.PaymentAmount.HasValue
                                                        ? ctx.PaymentAmount.Value.ToString("N0")
                                                        : string.Empty,
                DynamicBlockKey.PaymentPeriod => ctx.PaymentPeriod ?? string.Empty,
                DynamicBlockKey.ExamName => ctx.ExamName ?? string.Empty,
                DynamicBlockKey.ExamGrade => ctx.ExamGrade?.ToString() ?? string.Empty,
                DynamicBlockKey.ExamMaxGrade => ctx.ExamMaxGrade?.ToString() ?? string.Empty,
                DynamicBlockKey.HomeworkName => ctx.HomeworkName ?? string.Empty,
                DynamicBlockKey.HomeworkStatus => ctx.HomeworkStatus ?? string.Empty,
                _ => string.Empty,
            };
        }
    }

}
