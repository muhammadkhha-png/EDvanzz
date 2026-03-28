using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Edvanz.Infrastructure.Persistence;

public class EdvanzDbContext(DbContextOptions<EdvanzDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Tutor> Teachers { get; set; }
    public DbSet<Assistant> Assistants { get; set; }
    public DbSet<UsersTutor> UserTutor { get; set; }
    public DbSet<AssisstantPemisions> AssistantPermissions { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Module> Models { get; set; }
    public DbSet<Template> Templates { get; set; }
    public DbSet<TemplateAssistant> TemplatesOfAssistants { get; set; }
    public DbSet<TemplatePermisions> TemplatesPermisions { get; set; }



    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(warnings =>
               warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        #region keys
        modelBuilder.Entity<AssisstantPemisions>()
            .HasKey(ur => new { ur.UserId, ur.PermissionId });
        modelBuilder.Entity<UsersTutor>()
            .HasKey(ur => new { ur.userId, ur.TutorId });
        modelBuilder.Entity<TemplateAssistant>()
           .HasKey(ur => new { ur.AssistantId, ur.TemplateId });
        modelBuilder.Entity<TemplatePermisions>()
           .HasKey(ur => new { ur.PermisionId, ur.TemplateId });
        #endregion
        #region constrains
        modelBuilder.Entity<User>()
          .HasIndex(u => u.Username)
          .IsUnique();
            modelBuilder.Entity<User>()
        .HasIndex(u => u.PhoneNumber)
        .IsUnique();

        #endregion
    }
}
