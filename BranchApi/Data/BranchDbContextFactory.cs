using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BranchApi.Data;

public class BranchDbContextFactory : IDesignTimeDbContextFactory<BranchDbContext>
{
    public BranchDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BranchDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=coffee_shop;Username=branch_admin;Password=branch123")
            .Options;
        return new BranchDbContext(options);
    }
}
