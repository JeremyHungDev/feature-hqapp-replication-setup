using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HqApp.Data;

public class BranchDbContextFactory : IDesignTimeDbContextFactory<BranchDbContext>
{
    public BranchDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BranchDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=branch_db;Username=postgres;Password=postgres");
        return new BranchDbContext(optionsBuilder.Options);
    }
}
