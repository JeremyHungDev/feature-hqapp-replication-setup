using HqApi.Models;
using Microsoft.EntityFrameworkCore;

namespace HqApi.Data;

public class HqDbContext : DbContext
{
    public DbSet<Menu> Menus { get; set; }

    public HqDbContext(DbContextOptions<HqDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Menu>(entity =>
        {
            entity.ToTable("menu");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100);
            entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(50);
            entity.Property(e => e.Price).HasColumnName("price").HasPrecision(10, 2);
            entity.Property(e => e.Cost).HasColumnName("cost").HasPrecision(10, 2);
            entity.Property(e => e.IsAvailable).HasColumnName("is_available");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Menu>().HasData(
            new Menu { Id = 1, Name = "Americano",  Category = "coffee", Price = 120, Cost = 25, IsAvailable = true, UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Menu { Id = 2, Name = "Latte",      Category = "coffee", Price = 150, Cost = 35, IsAvailable = true, UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Menu { Id = 3, Name = "Green Tea",  Category = "tea",    Price = 100, Cost = 20, IsAvailable = true, UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Menu { Id = 4, Name = "Cheesecake", Category = "food",   Price = 180, Cost = 60, IsAvailable = true, UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Menu { Id = 5, Name = "Croissant",  Category = "food",   Price = 80,  Cost = 30, IsAvailable = true, UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
