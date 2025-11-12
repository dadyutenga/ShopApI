// using Microsoft.EntityFrameworkCore;
// using ShopApI.Models;

// namespace ShopApI.Data;

// public class ApplicationDbContext : DbContext
// {
//     public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
//         : base(options)
//     {
//     }

//     public DbSet<Product> Products { get; set; }

//     protected override void OnModelCreating(ModelBuilder modelBuilder)
//     {
//         base.OnModelCreating(modelBuilder);

//         // Configure entity relationships and constraints here
//         modelBuilder.Entity<Product>(entity =>
//         {
//             entity.HasKey(e => e.Id);
//             entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
//             entity.Property(e => e.Price).HasPrecision(18, 2);
//         });
//     }
// }
