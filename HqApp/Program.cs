using HqApp.Data;
using HqApp.Services;
using Microsoft.EntityFrameworkCore;

var hqConnStr = Environment.GetEnvironmentVariable("HQ_CONNECTION_STRING")
    ?? "Host=localhost;Port=5432;Database=coffee_shop;Username=hq_admin;Password=hq123";

var branchConnStr = Environment.GetEnvironmentVariable("BRANCH_CONNECTION_STRING")
    ?? "Host=localhost;Port=5433;Database=coffee_shop;Username=branch_admin;Password=branch123";

var hqOptions = new DbContextOptionsBuilder<HqDbContext>()
    .UseNpgsql(hqConnStr)
    .Options;

var branchOptions = new DbContextOptionsBuilder<BranchDbContext>()
    .UseNpgsql(branchConnStr)
    .Options;

await using var hqDb = new HqDbContext(hqOptions);
await using var branchDb = new BranchDbContext(branchOptions);

// 等待 DB 就緒
Console.WriteLine("等待 DB 就緒...");
var retries = 0;
while (retries < 10)
{
    try
    {
        await hqDb.Database.OpenConnectionAsync();
        await hqDb.Database.CloseConnectionAsync();
        Console.WriteLine("HQ DB 已就緒");
        break;
    }
    catch
    {
        retries++;
        Console.WriteLine($"HQ DB 尚未就緒，{retries}/10 重試中...");
        await Task.Delay(3000);
    }
}

// 等待 Branch DB 就緒
Console.WriteLine("等待 Branch DB 就緒...");
var branchRetries = 0;
while (branchRetries < 10)
{
    try
    {
        await branchDb.Database.OpenConnectionAsync();
        await branchDb.Database.CloseConnectionAsync();
        Console.WriteLine("Branch DB 已就緒");
        break;
    }
    catch
    {
        branchRetries++;
        Console.WriteLine($"Branch DB 尚未就緒，{branchRetries}/10 重試中...");
        await Task.Delay(3000);
    }
}

Console.WriteLine("=== Step 1: HQ Migration ===");
await hqDb.Database.MigrateAsync();
Console.WriteLine("HQ Migration 完成");

Console.WriteLine("\n=== Step 2: Branch Migration ===");
await branchDb.Database.MigrateAsync();
Console.WriteLine("Branch Migration 完成");

Console.WriteLine("\n=== Step 3: Replication 設定 ===");
var replicationSetup = new ReplicationSetupService(hqDb, branchDb);
await replicationSetup.SetupAsync();

Console.WriteLine("\n=== Step 4: HQ 模擬操作 ===");
var simulation = new HqSimulationService(hqDb, branchDb);
await simulation.RunAsync();
