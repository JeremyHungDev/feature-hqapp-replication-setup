using HqApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HqApp.Data;

public class HqDbContext : DbContext
{
    public DbSet<Menu> Menus { get; set; }
    public DbSet<StorePolicy> StorePolicies { get; set; }
    public DbSet<EmployeeSalary> EmployeeSalaries { get; set; }

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

        modelBuilder.Entity<StorePolicy>(entity =>
        {
            entity.ToTable("store_policy");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PolicyName).HasColumnName("policy_name").HasMaxLength(100);
            entity.Property(e => e.DiscountRate).HasColumnName("discount_rate").HasPrecision(5, 2);
            entity.Property(e => e.OpeningHour).HasColumnName("opening_hour");
            entity.Property(e => e.ClosingHour).HasColumnName("closing_hour");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<EmployeeSalary>(entity =>
        {
            entity.ToTable("employee_salary");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EmployeeName).HasColumnName("employee_name").HasMaxLength(100);
            entity.Property(e => e.Position).HasColumnName("position").HasMaxLength(50);
            entity.Property(e => e.MonthlySalary).HasColumnName("monthly_salary").HasPrecision(10, 2);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // Seed data
        modelBuilder.Entity<Menu>().HasData(
            new Menu { Id = 1, Name = "Americano",  Category = "coffee", Price = 120, Cost = 25, IsAvailable = true, UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Menu { Id = 2, Name = "Latte",      Category = "coffee", Price = 150, Cost = 35, IsAvailable = true, UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Menu { Id = 3, Name = "Green Tea",  Category = "tea",    Price = 100, Cost = 20, IsAvailable = true, UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Menu { Id = 4, Name = "Cheesecake", Category = "food",   Price = 180, Cost = 60, IsAvailable = true, UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Menu { Id = 5, Name = "Croissant",  Category = "food",   Price = 80,  Cost = 30, IsAvailable = true, UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        modelBuilder.Entity<StorePolicy>().HasData(
            new StorePolicy { Id = 1, PolicyName = "weekday", DiscountRate = 0,    OpeningHour = new TimeOnly(8, 0),  ClosingHour = new TimeOnly(22, 0), UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new StorePolicy { Id = 2, PolicyName = "weekend", DiscountRate = 0.1m, OpeningHour = new TimeOnly(9, 0),  ClosingHour = new TimeOnly(23, 0), UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        modelBuilder.Entity<EmployeeSalary>().HasData(
            new EmployeeSalary { Id = 1, EmployeeName = "Alice", Position = "Manager", MonthlySalary = 55000, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new EmployeeSalary { Id = 2, EmployeeName = "Bob",   Position = "Barista", MonthlySalary = 32000, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
