using Microsoft.EntityFrameworkCore;
using ShopApI.Models;

namespace ShopApI.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Role).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Provider).HasMaxLength(32);
            entity.Property(e => e.ProviderId).HasMaxLength(128);
            entity.Property(e => e.PhoneNumber).HasMaxLength(32);
            entity.Property(e => e.AuditTrail).HasColumnType("json");
            entity.HasMany(e => e.AuditLogs)
                  .WithOne(a => a.User)
                  .HasForeignKey(a => a.UserId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Metadata).HasColumnType("json");
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(2048);
        });

        // Configure RefreshToken entity
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
