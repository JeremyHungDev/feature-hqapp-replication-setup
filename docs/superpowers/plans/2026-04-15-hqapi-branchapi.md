# HqApi + BranchApi Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立兩個獨立的 ASP.NET Core Web API：HqApi（寫入 hq-db，管理菜單）與 BranchApi（訂閱 branch-db，LISTEN 通知），取代現有的 HqApp console app。

**Architecture:** HqApi 啟動時設定 Publication 並提供菜單 CRUD；BranchApi 啟動時設定 Subscription + Trigger，並以 BackgroundService 持續 LISTEN 'menu_update'，收到的通知存入記憶體 Singleton，由 `/api/notifications` 提供查詢。

**Tech Stack:** .NET 8、ASP.NET Core Web API、Entity Framework Core 8、Npgsql.EntityFrameworkCore.PostgreSQL 8.0.0、Npgsql 8.0.0（BranchApi 直連 LISTEN 用）

---

## File Structure

```
lab-postgres-replication/
├── docker-compose.yml                          ← 移除 hq-app，新增 hq-api + branch-api
├── HqApi/
│   ├── HqApi.csproj
│   ├── Program.cs                              ← DI 設定、DB 就緒 retry、Migration、Publication
│   ├── Dockerfile
│   ├── Controllers/
│   │   └── MenuController.cs                  ← GET/POST/PUT /api/menu
│   ├── Data/
│   │   ├── HqDbContext.cs                      ← Menu、StorePolicy、EmployeeSalary（含 seed）
│   │   ├── HqDbContextFactory.cs              ← IDesignTimeDbContextFactory（migration 用）
│   │   └── Migrations/                        ← dotnet ef 自動產生
│   ├── Models/
│   │   └── Menu.cs
│   └── Services/
│       └── ReplicationPublisherService.cs     ← 冪等設定 Publication
└── BranchApi/
    ├── BranchApi.csproj
    ├── Program.cs                              ← DI 設定、DB 就緒 retry、Migration、Subscription
    ├── Dockerfile
    ├── Controllers/
    │   ├── MenuController.cs                  ← GET /api/menu
    │   └── NotificationsController.cs         ← GET /api/notifications
    ├── Data/
    │   ├── BranchDbContext.cs                 ← BranchMenu、BranchStorePolicy（不含 seed）
    │   ├── BranchDbContextFactory.cs          ← IDesignTimeDbContextFactory（migration 用）
    │   └── Migrations/                        ← dotnet ef 自動產生
    ├── Models/
    │   └── BranchMenu.cs
    └── Services/
        ├── NotificationStore.cs               ← Singleton，ConcurrentQueue<NotificationMessage>
        ├── NotificationMessage.cs             ← record
        ├── MenuNotificationListener.cs        ← BackgroundService，LISTEN 'menu_update'
        └── ReplicationSubscriberService.cs    ← 冪等設定 Subscription + Trigger + read-only
```

---

## Task 1：建立 HqApi 專案

**Files:**
- Create: `HqApi/HqApi.csproj`

- [ ] **Step 1：建立 ASP.NET Core Web API 專案**

```bash
cd c:/Users/User/Downloads/lab-postgres-replication
dotnet new webapi -n HqApi -f net8.0 --no-openapi
```

- [ ] **Step 2：安裝 NuGet 套件**

```bash
cd HqApi
dotnet add package Microsoft.EntityFrameworkCore --version 8.0.0
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.0
```

- [ ] **Step 3：確認建置成功**

```bash
dotnet build
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4：Commit**

```bash
cd ..
git add HqApi/HqApi.csproj HqApi/Program.cs HqApi/appsettings*.json HqApi/Properties/
git commit -m "feat: init HqApi webapi project"
```

---

## Task 2：HqApi Models + HqDbContext + DesignTimeFactory

**Files:**
- Create: `HqApi/Models/Menu.cs`
- Create: `HqApi/Data/HqDbContext.cs`
- Create: `HqApi/Data/HqDbContextFactory.cs`

- [ ] **Step 1：建立 Menu model**

`HqApi/Models/Menu.cs`
```csharp
namespace HqApi.Models;

public class Menu
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2：建立 HqDbContext（含 seed data）**

`HqApi/Data/HqDbContext.cs`
```csharp
using HqApi.Models;
using Microsoft.EntityFrameworkCore;

namespace HqApi.Data;

public class HqDbContext : DbContext
{
    public DbSet<Menu> Menus { get; set; }

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

        modelBuilder.Entity<Menu>().HasData(
            new Menu { Id = 1, Name = "Americano",  Category = "coffee", Price = 120, Cost = 25, IsAvailable = true, UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Menu { Id = 2, Name = "Latte",      Category = "coffee", Price = 150, Cost = 35, IsAvailable = true, UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Menu { Id = 3, Name = "Green Tea",  Category = "tea",    Price = 100, Cost = 20, IsAvailable = true, UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Menu { Id = 4, Name = "Cheesecake", Category = "food",   Price = 180, Cost = 60, IsAvailable = true, UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Menu { Id = 5, Name = "Croissant",  Category = "food",   Price = 80,  Cost = 30, IsAvailable = true, UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
```

- [ ] **Step 3：建立 HqDbContextFactory（design-time migration 用）**

`HqApi/Data/HqDbContextFactory.cs`
```csharp
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
```

- [ ] **Step 4：確認建置成功**

```bash
cd HqApi && dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 5：Commit**

```bash
cd ..
git add HqApi/Models/ HqApi/Data/HqDbContext.cs HqApi/Data/HqDbContextFactory.cs
git commit -m "feat: add HqApi models, DbContext and design-time factory"
```

---

## Task 3：HqApi EF Core Migration

**Files:**
- Create: `HqApi/Data/Migrations/` (自動產生)

- [ ] **Step 1：確認 hq-db 正在執行**

```bash
docker ps | grep hq-db
```

Expected: 看到 `hq-db` container 狀態為 `Up`。若未啟動先執行 `docker-compose up -d hq-db`。

- [ ] **Step 2：產生 migration**

```bash
cd HqApi
dotnet ef migrations add InitHq --context HqDbContext --output-dir Data/Migrations
```

Expected: `Data/Migrations/` 下產生 3 個檔案（`*_InitHq.cs`、`*_InitHq.Designer.cs`、`HqDbContextModelSnapshot.cs`）。

- [ ] **Step 3：Commit**

```bash
cd ..
git add HqApi/Data/Migrations/
git commit -m "feat: add HqApi EF Core migration"
```

---

## Task 4：HqApi ReplicationPublisherService

**Files:**
- Create: `HqApi/Services/ReplicationPublisherService.cs`

- [ ] **Step 1：建立 ReplicationPublisherService**

`HqApi/Services/ReplicationPublisherService.cs`
```csharp
using HqApi.Data;
using Microsoft.EntityFrameworkCore;

namespace HqApi.Services;

public class ReplicationPublisherService
{
    private readonly HqDbContext _db;

    public ReplicationPublisherService(HqDbContext db)
    {
        _db = db;
    }

    public async Task SetupAsync()
    {
        Console.WriteLine("[Replication] 設定 Publication...");
        await _db.Database.ExecuteSqlRawAsync(@"
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_publication WHERE pubname = 'pub_hq_to_branch') THEN
                    CREATE PUBLICATION pub_hq_to_branch FOR TABLE
                        menu (id, name, category, price, is_available, updated_at),
                        store_policy;
                    RAISE NOTICE 'Publication 建立完成';
                ELSE
                    RAISE NOTICE 'Publication 已存在，跳過';
                END IF;
            END $$;
        ");
        Console.WriteLine("[Replication] Publication OK");
    }
}
```

- [ ] **Step 2：確認建置成功**

```bash
cd HqApi && dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 3：Commit**

```bash
cd ..
git add HqApi/Services/ReplicationPublisherService.cs
git commit -m "feat: add HqApi ReplicationPublisherService"
```

---

## Task 5：HqApi MenuController

**Files:**
- Create: `HqApi/Controllers/MenuController.cs`

- [ ] **Step 1：建立 MenuController**

`HqApi/Controllers/MenuController.cs`
```csharp
using HqApi.Data;
using HqApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HqApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuController : ControllerBase
{
    private readonly HqDbContext _db;

    public MenuController(HqDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _db.Menus.OrderBy(m => m.Id).ToListAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMenuRequest req)
    {
        var menu = new Menu
        {
            Name = req.Name,
            Category = req.Category,
            Price = req.Price,
            Cost = req.Cost,
            IsAvailable = true,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Menus.Add(menu);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = menu.Id }, menu);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMenuRequest req)
    {
        var menu = await _db.Menus.FindAsync(id);
        if (menu is null) return NotFound();

        if (req.Price.HasValue)       menu.Price = req.Price.Value;
        if (req.IsAvailable.HasValue) menu.IsAvailable = req.IsAvailable.Value;
        menu.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(menu);
    }
}

public record CreateMenuRequest(string Name, string Category, decimal Price, decimal Cost);
public record UpdateMenuRequest(decimal? Price, bool? IsAvailable);
```

- [ ] **Step 2：確認建置成功**

```bash
cd HqApi && dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 3：Commit**

```bash
cd ..
git add HqApi/Controllers/MenuController.cs
git commit -m "feat: add HqApi MenuController"
```

---

## Task 6：HqApi Program.cs + Dockerfile

**Files:**
- Modify: `HqApi/Program.cs`
- Create: `HqApi/Dockerfile`

- [ ] **Step 1：撰寫 Program.cs**

`HqApi/Program.cs`
```csharp
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

    await db.Database.MigrateAsync();
    Console.WriteLine("HQ Migration 完成");

    var replication = new ReplicationPublisherService(db);
    await replication.SetupAsync();
}

app.MapControllers();
app.Run();
```

- [ ] **Step 2：建立 Dockerfile**

`HqApi/Dockerfile`
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "HqApi.dll"]
```

- [ ] **Step 3：確認建置成功**

```bash
cd HqApi && dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 4：Commit**

```bash
cd ..
git add HqApi/Program.cs HqApi/Dockerfile
git commit -m "feat: add HqApi Program.cs and Dockerfile"
```

---

## Task 7：建立 BranchApi 專案

**Files:**
- Create: `BranchApi/BranchApi.csproj`

- [ ] **Step 1：建立 ASP.NET Core Web API 專案**

```bash
cd c:/Users/User/Downloads/lab-postgres-replication
dotnet new webapi -n BranchApi -f net8.0 --no-openapi
```

- [ ] **Step 2：安裝 NuGet 套件**

```bash
cd BranchApi
dotnet add package Microsoft.EntityFrameworkCore --version 8.0.0
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.0
dotnet add package Npgsql --version 8.0.0
```

- [ ] **Step 3：確認建置成功**

```bash
dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 4：Commit**

```bash
cd ..
git add BranchApi/BranchApi.csproj BranchApi/Program.cs BranchApi/appsettings*.json BranchApi/Properties/
git commit -m "feat: init BranchApi webapi project"
```

---

## Task 8：BranchApi Models + BranchDbContext + DesignTimeFactory

**Files:**
- Create: `BranchApi/Models/BranchMenu.cs`
- Create: `BranchApi/Data/BranchDbContext.cs`
- Create: `BranchApi/Data/BranchDbContextFactory.cs`

- [ ] **Step 1：建立 BranchMenu model**

`BranchApi/Models/BranchMenu.cs`
```csharp
namespace BranchApi.Models;

public class BranchMenu
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2：建立 BranchDbContext（不含 seed data、不含 cost 欄位）**

`BranchApi/Data/BranchDbContext.cs`
```csharp
using BranchApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BranchApi.Data;

public class BranchDbContext : DbContext
{
    public DbSet<BranchMenu> Menus { get; set; }

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
    }
}
```

- [ ] **Step 3：建立 BranchDbContextFactory（design-time migration 用）**

`BranchApi/Data/BranchDbContextFactory.cs`
```csharp
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
```

- [ ] **Step 4：確認建置成功**

```bash
cd BranchApi && dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 5：Commit**

```bash
cd ..
git add BranchApi/Models/ BranchApi/Data/BranchDbContext.cs BranchApi/Data/BranchDbContextFactory.cs
git commit -m "feat: add BranchApi models, DbContext and design-time factory"
```

---

## Task 9：BranchApi EF Core Migration

**Files:**
- Create: `BranchApi/Data/Migrations/` (自動產生)

- [ ] **Step 1：確認 branch-db 正在執行**

```bash
docker ps | grep branch-db
```

Expected: 看到 `branch-db` container 狀態為 `Up`。若未啟動先執行 `docker-compose up -d branch-db`。

- [ ] **Step 2：產生 migration**

```bash
cd BranchApi
dotnet ef migrations add InitBranch --context BranchDbContext --output-dir Data/Migrations
```

Expected: `Data/Migrations/` 下產生 3 個檔案。

- [ ] **Step 3：Commit**

```bash
cd ..
git add BranchApi/Data/Migrations/
git commit -m "feat: add BranchApi EF Core migration"
```

---

## Task 10：BranchApi NotificationStore + NotificationMessage + MenuNotificationListener

**Files:**
- Create: `BranchApi/Services/NotificationMessage.cs`
- Create: `BranchApi/Services/NotificationStore.cs`
- Create: `BranchApi/Services/MenuNotificationListener.cs`

- [ ] **Step 1：建立 NotificationMessage record**

`BranchApi/Services/NotificationMessage.cs`
```csharp
namespace BranchApi.Services;

public record NotificationMessage(
    DateTime ReceivedAt,
    string Channel,
    string Payload
);
```

- [ ] **Step 2：建立 NotificationStore（Singleton）**

`BranchApi/Services/NotificationStore.cs`
```csharp
using System.Collections.Concurrent;

namespace BranchApi.Services;

public class NotificationStore
{
    private readonly ConcurrentQueue<NotificationMessage> _messages = new();
    private const int MaxMessages = 50;

    public void Add(NotificationMessage message)
    {
        _messages.Enqueue(message);
        while (_messages.Count > MaxMessages)
            _messages.TryDequeue(out _);
    }

    public IReadOnlyList<NotificationMessage> GetRecent() => _messages.ToArray();
}
```

- [ ] **Step 3：建立 MenuNotificationListener（BackgroundService）**

`BranchApi/Services/MenuNotificationListener.cs`
```csharp
using Npgsql;

namespace BranchApi.Services;

public class MenuNotificationListener : BackgroundService
{
    private readonly string _connectionString;
    private readonly NotificationStore _store;
    private readonly ILogger<MenuNotificationListener> _logger;

    public MenuNotificationListener(NotificationStore store, ILogger<MenuNotificationListener> logger)
    {
        _connectionString = Environment.GetEnvironmentVariable("BRANCH_CONNECTION_STRING")
            ?? "Host=localhost;Port=5433;Database=coffee_shop;Username=branch_admin;Password=branch123";
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(stoppingToken);

                await using var cmd = new NpgsqlCommand("LISTEN menu_update", conn);
                await cmd.ExecuteNonQueryAsync(stoppingToken);

                conn.Notification += (_, args) =>
                {
                    var msg = new NotificationMessage(DateTime.UtcNow, args.Channel, args.Payload);
                    _store.Add(msg);
                    _logger.LogInformation("[NOTIFY] {Channel}: {Payload}", args.Channel, args.Payload);
                };

                _logger.LogInformation("MenuNotificationListener 已啟動，等待通知...");

                while (!stoppingToken.IsCancellationRequested)
                    await conn.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LISTEN 連線中斷，5 秒後重連...");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
```

- [ ] **Step 4：確認建置成功**

```bash
cd BranchApi && dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 5：Commit**

```bash
cd ..
git add BranchApi/Services/NotificationMessage.cs BranchApi/Services/NotificationStore.cs BranchApi/Services/MenuNotificationListener.cs
git commit -m "feat: add BranchApi NotificationStore and MenuNotificationListener"
```

---

## Task 11：BranchApi ReplicationSubscriberService

**Files:**
- Create: `BranchApi/Services/ReplicationSubscriberService.cs`

- [ ] **Step 1：建立 ReplicationSubscriberService**

`BranchApi/Services/ReplicationSubscriberService.cs`
```csharp
using BranchApi.Data;
using Microsoft.EntityFrameworkCore;

namespace BranchApi.Services;

public class ReplicationSubscriberService
{
    private readonly BranchDbContext _db;

    public ReplicationSubscriberService(BranchDbContext db)
    {
        _db = db;
    }

    public async Task SetupAsync()
    {
        await CreateSubscriptionAsync();
        await CreateBranchTriggerAsync();
        await SetBranchReadOnlyAsync();
    }

    private async Task CreateSubscriptionAsync()
    {
        Console.WriteLine("[Replication] 設定 Subscription...");
        var count = await _db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM pg_subscription WHERE subname = 'sub_branch_from_hq'")
            .FirstAsync();

        if (count == 0)
        {
            await _db.Database.ExecuteSqlRawAsync(
                "CREATE SUBSCRIPTION sub_branch_from_hq " +
                "CONNECTION 'host=hq-db port=5432 dbname=coffee_shop user=hq_admin password=hq123' " +
                "PUBLICATION pub_hq_to_branch");
            Console.WriteLine("[Replication] Subscription 建立完成");
        }
        else
        {
            Console.WriteLine("[Replication] Subscription 已存在，跳過");
        }
        Console.WriteLine("[Replication] Subscription OK");
    }

    private async Task CreateBranchTriggerAsync()
    {
        Console.WriteLine("[Replication] 設定 NOTIFY Trigger...");
        await _db.Database.ExecuteSqlRawAsync(@"
            CREATE OR REPLACE FUNCTION notify_menu_change()
            RETURNS TRIGGER AS $$
            BEGIN
                PERFORM pg_notify('menu_update', json_build_object(
                    'item', NEW.name,
                    'price', NEW.price,
                    'available', NEW.is_available,
                    'action', TG_OP
                )::text);
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            DROP TRIGGER IF EXISTS trg_menu_notify ON menu;
            CREATE TRIGGER trg_menu_notify
                AFTER INSERT OR UPDATE ON menu
                FOR EACH ROW
                EXECUTE FUNCTION notify_menu_change();

            ALTER TABLE menu ENABLE ALWAYS TRIGGER trg_menu_notify;
        ");
        Console.WriteLine("[Replication] Trigger OK");
    }

    private async Task SetBranchReadOnlyAsync()
    {
        Console.WriteLine("[Replication] 設定 Branch read-only...");
        await _db.Database.ExecuteSqlRawAsync(
            "ALTER DATABASE coffee_shop SET default_transaction_read_only = on;");
        Console.WriteLine("[Replication] Branch read-only OK");
    }
}
```

- [ ] **Step 2：確認建置成功**

```bash
cd BranchApi && dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 3：Commit**

```bash
cd ..
git add BranchApi/Services/ReplicationSubscriberService.cs
git commit -m "feat: add BranchApi ReplicationSubscriberService"
```

---

## Task 12：BranchApi Controllers

**Files:**
- Create: `BranchApi/Controllers/MenuController.cs`
- Create: `BranchApi/Controllers/NotificationsController.cs`

- [ ] **Step 1：建立 MenuController（唯讀）**

`BranchApi/Controllers/MenuController.cs`
```csharp
using BranchApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BranchApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuController : ControllerBase
{
    private readonly BranchDbContext _db;

    public MenuController(BranchDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _db.Menus.OrderBy(m => m.Id).ToListAsync();
        return Ok(items);
    }
}
```

- [ ] **Step 2：建立 NotificationsController**

`BranchApi/Controllers/NotificationsController.cs`
```csharp
using BranchApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BranchApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationStore _store;

    public NotificationsController(NotificationStore store)
    {
        _store = store;
    }

    [HttpGet]
    public IActionResult GetRecent()
    {
        return Ok(_store.GetRecent());
    }
}
```

- [ ] **Step 3：確認建置成功**

```bash
cd BranchApi && dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 4：Commit**

```bash
cd ..
git add BranchApi/Controllers/
git commit -m "feat: add BranchApi MenuController and NotificationsController"
```

---

## Task 13：BranchApi Program.cs + Dockerfile

**Files:**
- Modify: `BranchApi/Program.cs`
- Create: `BranchApi/Dockerfile`

- [ ] **Step 1：撰寫 Program.cs**

`BranchApi/Program.cs`
```csharp
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

    await db.Database.MigrateAsync();
    Console.WriteLine("Branch Migration 完成");

    var replication = new ReplicationSubscriberService(db);
    await replication.SetupAsync();
}

app.MapControllers();
app.Run();
```

- [ ] **Step 2：建立 Dockerfile**

`BranchApi/Dockerfile`
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "BranchApi.dll"]
```

- [ ] **Step 3：確認建置成功**

```bash
cd BranchApi && dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 4：Commit**

```bash
cd ..
git add BranchApi/Program.cs BranchApi/Dockerfile
git commit -m "feat: add BranchApi Program.cs and Dockerfile"
```

---

## Task 14：更新 docker-compose.yml

**Files:**
- Modify: `docker-compose.yml`

- [ ] **Step 1：更新 docker-compose.yml**

將現有 `docker-compose.yml` 的 `hq-app` service 移除，並加入 `hq-api` 和 `branch-api`：

`docker-compose.yml`
```yaml
services:
  hq-db:
    image: postgres:16
    container_name: hq-db
    environment:
      POSTGRES_USER: hq_admin
      POSTGRES_PASSWORD: hq123
      POSTGRES_DB: coffee_shop
    ports:
      - "5432:5432"
    command:
      - "postgres"
      - "-c"
      - "wal_level=logical"
      - "-c"
      - "max_replication_slots=10"
      - "-c"
      - "max_wal_senders=10"
    volumes:
      - hq_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U hq_admin -d coffee_shop"]
      interval: 5s
      timeout: 5s
      retries: 10

  branch-db:
    image: postgres:16
    container_name: branch-db
    environment:
      POSTGRES_USER: branch_admin
      POSTGRES_PASSWORD: branch123
      POSTGRES_DB: coffee_shop
    ports:
      - "5433:5432"
    command:
      - "postgres"
      - "-c"
      - "wal_level=logical"
      - "-c"
      - "max_replication_slots=10"
      - "-c"
      - "max_wal_senders=10"
    volumes:
      - branch_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U branch_admin -d coffee_shop"]
      interval: 5s
      timeout: 5s
      retries: 10

  hq-api:
    build: ./HqApi
    container_name: hq-api
    ports:
      - "5001:8080"
    environment:
      HQ_CONNECTION_STRING: "Host=hq-db;Port=5432;Database=coffee_shop;Username=hq_admin;Password=hq123"
    depends_on:
      hq-db:
        condition: service_healthy

  branch-api:
    build: ./BranchApi
    container_name: branch-api
    ports:
      - "5002:8080"
    environment:
      BRANCH_CONNECTION_STRING: "Host=branch-db;Port=5432;Database=coffee_shop;Username=branch_admin;Password=branch123"
    depends_on:
      hq-db:
        condition: service_healthy
      branch-db:
        condition: service_healthy

  pgadmin:
    image: dpage/pgadmin4
    container_name: pgadmin
    environment:
      PGADMIN_DEFAULT_EMAIL: admin@coffee.com
      PGADMIN_DEFAULT_PASSWORD: admin123
    ports:
      - "8080:80"
    depends_on:
      - hq-db
      - branch-db

volumes:
  hq_data:
  branch_data:
```

- [ ] **Step 2：Commit**

```bash
git add docker-compose.yml
git commit -m "feat: update docker-compose - replace hq-app with hq-api and branch-api"
```

---

## Task 15：整合測試

- [ ] **Step 1：清環境並啟動**

```bash
docker-compose down -v
docker-compose up --build
```

- [ ] **Step 2：確認 hq-api 啟動 log**

```bash
docker logs hq-api
```

Expected：
```
HQ DB 已就緒
HQ Migration 完成
[Replication] 設定 Publication...
[Replication] Publication OK
```

- [ ] **Step 3：確認 branch-api 啟動 log**

```bash
docker logs branch-api
```

Expected：
```
Branch DB 已就緒
Branch Migration 完成
[Replication] 設定 Subscription...
[Replication] Subscription 建立完成
[Replication] Subscription OK
[Replication] 設定 NOTIFY Trigger...
[Replication] Trigger OK
[Replication] 設定 Branch read-only...
[Replication] Branch read-only OK
MenuNotificationListener 已啟動，等待通知...
```

- [ ] **Step 4：GET HqApi 菜單（含 seed data）**

```
GET http://localhost:5001/api/menu
```

Expected：5 筆菜單（Americano、Latte、Green Tea、Cheesecake、Croissant），每筆含 `cost` 欄位。

- [ ] **Step 5：POST 新增菜單到 HqApi**

```
POST http://localhost:5001/api/menu
Content-Type: application/json

{ "name": "Oolong Tea", "category": "tea", "price": 110, "cost": 20 }
```

Expected：201 Created，回傳含 `id` 的菜單物件。

- [ ] **Step 6：等 1 秒後 GET BranchApi 菜單**

```
GET http://localhost:5002/api/menu
```

Expected：看到 `Oolong Tea`，且**沒有 `cost` 欄位**。

- [ ] **Step 7：GET BranchApi 通知**

```
GET http://localhost:5002/api/notifications
```

Expected：看到至少一筆通知，payload 包含 `"item":"Oolong Tea","action":"INSERT"`。

- [ ] **Step 8：PUT 修改 Americano 價格**

```
PUT http://localhost:5001/api/menu/1
Content-Type: application/json

{ "price": 140 }
```

等 1 秒後再次呼叫 `GET http://localhost:5002/api/notifications`，確認又多一筆 `action: UPDATE` 通知。

- [ ] **Step 9：最終 Commit**

```bash
git add .
git commit -m "feat: complete HqApi + BranchApi integration"
git push origin master
```
