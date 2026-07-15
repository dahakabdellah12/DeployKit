using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<AppRegistration> Apps => Set<AppRegistration>();
    public DbSet<UpdatePackage> Packages => Set<UpdatePackage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Token).IsUnique();
        });

        model.Entity<AppRegistration>(e =>
        {
            e.HasIndex(a => a.AppKey).IsUnique();
            e.HasOne(a => a.User)
             .WithMany()
             .HasForeignKey(a => a.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => a.UserId);
        });

        model.Entity<UpdatePackage>(e =>
        {
            e.HasOne(p => p.App)
             .WithMany(a => a.Packages)
             .HasForeignKey(p => p.AppId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.CreatedAt);
        });
    }
}
