using BranchApi.Data;
using BranchApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connStr = Environment.GetEnvironmentVariable("BRANCH_CONNECTION_STRING")
    ?? "Host=localhost;Port=5433;Database=coffee_shop;Username=branch_admin;Password=branch123";

builder.Services.AddDbContext<BranchDbContext>(opt => opt.UseNpgsql(connStr));
builder.Services.AddSingleton<NotificationStore>();
builder.Services.AddHostedService<MenuNotificationListener>();
builder.Services.AddControllers();

var app = builder.Build();

// DB 就緒 retry + Migration + Subscription 設定
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BranchDbContext>();
    var retries = 0;
    while (retries < 10)
    {
        try
        {
            await db.Database.OpenConnectionAsync();
            await db.Database.CloseConnectionAsync();
            Console.WriteLine("Branch DB 已就緒");
            break;
        }
        catch
        {
            retries++;
            Console.WriteLine($"Branch DB 尚未就緒，{retries}/10 重試中...");
            await Task.Delay(3000);
        }
    }

    if (retries >= 10)
    {
        Console.Error.WriteLine("Branch DB 無法連線，終止程式");
        Environment.Exit(1);
    }

    await db.Database.MigrateAsync();
    Console.WriteLine("Branch Migration 完成");

    var replication = new ReplicationSubscriberService(db);
    await replication.SetupAsync();
}

app.MapControllers();
app.Run();
