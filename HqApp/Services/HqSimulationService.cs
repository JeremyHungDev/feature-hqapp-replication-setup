using HqApp.Data;
using HqApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HqApp.Services;

public class HqSimulationService
{
    private readonly HqDbContext _hqDb;
    private readonly BranchDbContext _branchDb;

    public HqSimulationService(HqDbContext hqDb, BranchDbContext branchDb)
    {
        _hqDb = hqDb;
        _branchDb = branchDb;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("\n========== HQ 模擬操作開始 ==========\n");

        // 1. 上架新飲品
        var matchaLatte = new Menu
        {
            Name = "Matcha Latte", Category = "tea",
            Price = 160, Cost = 40, IsAvailable = true,
            UpdatedAt = DateTime.UtcNow
        };
        _hqDb.Menus.Add(matchaLatte);
        await _hqDb.SaveChangesAsync();
        Console.WriteLine("[HQ] 上架新品：Matcha Latte $160");

        // 2. 修改價格
        var americano = await _hqDb.Menus.FirstAsync(m => m.Name == "Americano");
        americano.Price = 130;
        americano.UpdatedAt = DateTime.UtcNow;
        await _hqDb.SaveChangesAsync();
        Console.WriteLine("[HQ] 調整價格：Americano $120 → $130");

        // 3. 下架商品
        var croissant = await _hqDb.Menus.FirstAsync(m => m.Name == "Croissant");
        croissant.IsAvailable = false;
        croissant.UpdatedAt = DateTime.UtcNow;
        await _hqDb.SaveChangesAsync();
        Console.WriteLine("[HQ] 下架：Croissant");

        // 4. 等待 replication 同步
        Console.WriteLine("\n[等待] Replication 同步中...");
        await Task.Delay(2000);

        // 5. 查詢 Branch 結果
        Console.WriteLine("\n[Branch] 同步後菜單：");
        var branchMenus = await _branchDb.Menus.OrderBy(m => m.Id).ToListAsync();
        foreach (var m in branchMenus)
            Console.WriteLine($"  {m.Name,-20} ${m.Price,7:F2}  available={m.IsAvailable}");

        Console.WriteLine("\n========== 模擬完成 ==========");
    }
}
