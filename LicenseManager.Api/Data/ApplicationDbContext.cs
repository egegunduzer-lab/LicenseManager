using LicenseManager.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LicenseManager.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Project> Projects { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<License> Licenses { get; set; }
    public DbSet<ProjectKey> ProjectKeys =>
    Set<ProjectKey>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aynı isim + sürüm kombinasyonu tekrar edemez.
        modelBuilder.Entity<Project>()
            .HasIndex(p => new { p.Name, p.Version })
            .IsUnique();
       
        modelBuilder.Entity<Project>()
            .Property(p => p.KeyVersion)
            .HasDefaultValue(1);

        // Aynı license key tekrar edemez.
        modelBuilder.Entity<License>()
            .HasIndex(l => l.LicenseKey)
            .IsUnique();

        // Modules PostgreSQL'de JSONB olarak saklanacak.
        modelBuilder.Entity<License>()
            .Property(l => l.Modules)
            .HasColumnType("jsonb");

        // License -> Company ilişkisi
        modelBuilder.Entity<License>()
            .HasOne(l => l.Company)
            .WithMany(c => c.Licenses)
            .HasForeignKey(l => l.CompanyId);

        // License -> Project ilişkisi
        modelBuilder.Entity<License>()
            .HasOne(l => l.Project)
            .WithMany(p => p.Licenses)
            .HasForeignKey(l => l.ProjectId);

        modelBuilder.Entity<License>()
    .Property(l => l.Modules)
    .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<string>>(
            v,
            (JsonSerializerOptions?)null) ?? new List<string>())
    .HasColumnType("jsonb");

        modelBuilder.Entity<ProjectKey>()
            .HasIndex(k => new
    {
        k.ProjectId,
        k.KeyVersion
    })
            .IsUnique();

        modelBuilder.Entity<ProjectKey>()
            .HasIndex(x => x.ProjectId)
            .IsUnique()
            .HasFilter("\"IsActive\" = TRUE");

        modelBuilder.Entity<ProjectKey>()
            .HasOne(k => k.Project)
            .WithMany(p => p.Keys)
            .HasForeignKey(k => k.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
