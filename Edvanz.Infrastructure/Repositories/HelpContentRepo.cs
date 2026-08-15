using Edvanz.Domain.Entities.Help;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// EF implementation of <see cref="IHelpContentRepo"/>. Reads are AsNoTracking and
/// eager-load the module → tour/articles → sections graph in DisplayOrder.
/// </summary>
public class HelpContentRepo : GenericRepo<HelpModule, long>, IHelpContentRepo
{
    public HelpContentRepo(EdvanzDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<HelpModule>> GetModulesWithContentAsync(HelpPersona? persona)
    {
        var query = _context.Set<HelpModule>()
            .AsNoTracking()
            .Where(m => m.IsActive);

        if (persona is not null)
            query = query.Where(m => m.Persona == persona);

        return await query
            .OrderBy(m => m.Persona).ThenBy(m => m.DisplayOrder)
            .Include(m => m.Tour.OrderBy(s => s.DisplayOrder))
            .Include(m => m.Articles.OrderBy(a => a.DisplayOrder))
                .ThenInclude(a => a.Sections.OrderBy(s => s.DisplayOrder))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<HelpTourStep>> GetTourStepsAsync(HelpPersona persona, string moduleKey)
    {
        return await _context.Set<HelpTourStep>()
            .AsNoTracking()
            .Where(s => s.HelpModule.Persona == persona
                        && s.HelpModule.Key == moduleKey
                        && s.HelpModule.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<HelpArticle>> GetArticlesAsync(HelpPersona persona, string moduleKey)
    {
        return await _context.Set<HelpArticle>()
            .AsNoTracking()
            .Where(a => a.HelpModule.Persona == persona
                        && a.HelpModule.Key == moduleKey
                        && a.HelpModule.IsActive)
            .OrderBy(a => a.DisplayOrder)
            .Include(a => a.Sections.OrderBy(s => s.DisplayOrder))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<HelpFaqItem>> GetFaqsAsync(HelpPersona? persona)
    {
        var query = _context.Set<HelpFaqItem>()
            .AsNoTracking()
            .Where(f => f.IsActive);

        if (persona is not null)
            query = query.Where(f => f.Persona == persona);

        return await query
            .OrderBy(f => f.Persona).ThenBy(f => f.DisplayOrder)
            .ToListAsync();
    }
}
