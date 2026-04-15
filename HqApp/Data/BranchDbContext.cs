using HqApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HqApp.Data;

public class BranchDbContext : DbContext
{
    public DbSet<BranchMenu> Menus { get; set; }
    public DbSet<BranchStorePolicy> StorePolicies { get; set; }

    public BranchDbContext(DbContextOptions<BranchDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BranchMenu>(entity =>
        {
            entity.ToTable("menu");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100);
            entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(50);
            entity.Property(e => e.Price).HasColumnName("price").HasPrecision(10, 2);
            entity.Property(e => e.IsAvailable).HasColumnName("is_available");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<BranchStorePolicy>(entity =>
        {
            entity.ToTable("store_policy");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PolicyName).HasColumnName("policy_name").HasMaxLength(100);
            entity.Property(e => e.DiscountRate).HasColumnName("discount_rate").HasPrecision(5, 2);
            entity.Property(e => e.OpeningHour).HasColumnName("opening_hour");
            entity.Property(e => e.ClosingHour).HasColumnName("closing_hour");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
