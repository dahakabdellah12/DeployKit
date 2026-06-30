using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AppRegistration> Apps => Set<AppRegistration>();
    public DbSet<UpdatePackage> Packages => Set<UpdatePackage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AdminUser>(e =>
        {
            e.HasIndex(a => a.Username).IsUnique();
            e.HasIndex(a => a.Token).IsUnique();
        });

        model.Entity<AppRegistration>(e =>
        {
            e.HasIndex(a => a.AppKey).IsUnique();
        });

        model.Entity<UpdatePackage>(e =>
        {
            e.HasOne(p => p.App)
             .WithMany(a => a.Packages)
             .HasForeignKey(p => p.AppId);
        });
    }
}
