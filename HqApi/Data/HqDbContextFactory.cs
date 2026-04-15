using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HqApi.Data;

public class HqDbContextFactory : IDesignTimeDbContextFactory<HqDbContext>
{
    public HqDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HqDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=coffee_shop;Username=hq_admin;Password=hq123")
            .Options;
        return new HqDbContext(options);
    }
}
