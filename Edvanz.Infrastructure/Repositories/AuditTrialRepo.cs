using Edvanz.Application.Dtos.AuditTrial;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edvanz.Infrastructure.Repositories
{
    public class AuditTrialRepo : GenericRepo<AuditTrail, long>, IAuditTrialRepo
    {
        public AuditTrialRepo(EdvanzDbContext context) : base(context)
        {
        }

        public async Task<(List<AuditTrail>, int count)> GetTeacherAssitantsAuditTrial(
      long teacherID,
      string? actionType,
      string? assistantName,
      string? module,
      DateTime? from,
      DateTime? to,
      int page,
      int pageSize)
        {
            var query = _context.AuditTrial
                .Include(a => a.assistant).ThenInclude(a=>a.User)
                .Include(a => a.module)
                .Where(a => a.teacherId == teacherID)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(actionType) &&
                Enum.TryParse<ActionType>(actionType, true, out var parsedAction))
            {
                query = query.Where(a => a.actionType == parsedAction);
            }

            if (!string.IsNullOrWhiteSpace(assistantName))
            {
                query = query.Where(a =>
                    a.assistant != null &&
                    a.assistant.User.FullName.ToLower().Contains(assistantName.ToLower()));
            }

            if (!string.IsNullOrWhiteSpace(module))
            {
                query = query.Where(a =>
                    a.module != null &&
                    a.module.Name.ToLower().Contains(module.ToLower()));
            }

            if (from.HasValue)
            {
                query = query.Where(a => a.CreateAt >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(a => a.CreateAt <= to.Value);
            }

            var count = await query.CountAsync();

            var data = await query
                .OrderByDescending(a => a.CreateAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (data, count);
        }

        public async Task<List<AuditTrail>> GetTeacherAssitantsAuditTrialForExcel(long teacherID, string? ActionType, string? AssistantName, string? Module, DateTime? From, DateTime? To)
        {
            var query = _context.AuditTrial
                .Include(a => a.assistant)
                    .ThenInclude(a => a.User)
                .Include(a => a.module)
                .Where(a => a.teacherId == teacherID)
                .AsQueryable();

            // 🔹 Filter: ActionType
            if (!string.IsNullOrWhiteSpace(ActionType) &&
                Enum.TryParse<ActionType>(ActionType, true, out var parsedAction))
            {
                query = query.Where(a => a.actionType == parsedAction);
            }

            // 🔹 Filter: Assistant Name
            if (!string.IsNullOrWhiteSpace(AssistantName))
            {
                var name = AssistantName.ToLower();
                query = query.Where(a =>
                    a.assistant != null &&
                    a.assistant.User.FullName.ToLower().Contains(name));
            }

            // 🔹 Filter: Module
            if (!string.IsNullOrWhiteSpace(Module))
            {
                var module = Module.ToLower();
                query = query.Where(a =>
                    a.module != null &&
                    a.module.Name.ToLower().Contains(module));
            }

            // 🔹 Filter: Date From
            if (From.HasValue)
            {
                query = query.Where(a => a.CreateAt >= From.Value);
            }

            // 🔹 Filter: Date To
            if (To.HasValue)
            {
                query = query.Where(a => a.CreateAt <= To.Value);
            }

            // 🔹 Order (مهم في Excel)
            return await query
                .OrderByDescending(a => a.CreateAt)
                .ToListAsync();
        }
    }
}
