using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HqApp.Data;

public class HqDbContextFactory : IDesignTimeDbContextFactory<HqDbContext>
{
    public HqDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HqDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=hq_db;Username=postgres;Password=postgres");
        return new HqDbContext(optionsBuilder.Options);
    }
}
