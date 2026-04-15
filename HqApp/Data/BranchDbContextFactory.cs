using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HqApp.Data;

public class BranchDbContextFactory : IDesignTimeDbContextFactory<BranchDbContext>
{
    public BranchDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BranchDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=coffee_shop;Username=branch_admin;Password=branch123");
        return new BranchDbContext(optionsBuilder.Options);
    }
}
