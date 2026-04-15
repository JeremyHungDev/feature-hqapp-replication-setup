using HqApi.Data;
using HqApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connStr = Environment.GetEnvironmentVariable("HQ_CONNECTION_STRING")
    ?? "Host=localhost;Port=5432;Database=coffee_shop;Username=hq_admin;Password=hq123";

builder.Services.AddDbContext<HqDbContext>(opt => opt.UseNpgsql(connStr));
builder.Services.AddControllers();

var app = builder.Build();

// DB 就緒 retry + Migration + Publication 設定
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HqDbContext>();
    var retries = 0;
    while (retries < 10)
    {
        try
        {
            await db.Database.OpenConnectionAsync();
            await db.Database.CloseConnectionAsync();
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

    if (retries >= 10)
    {
        Console.Error.WriteLine("HQ DB 無法連線，終止程式");
        Environment.Exit(1);
    }

    await db.Database.MigrateAsync();
    Console.WriteLine("HQ Migration 完成");

    var replication = new ReplicationPublisherService(db);
    await replication.SetupAsync();
}

app.MapControllers();
app.Run();
