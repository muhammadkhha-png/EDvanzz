using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Persistence;

public class EdvanzDbContext(DbContextOptions<EdvanzDbContext> options) : DbContext(options)
{
    
}
