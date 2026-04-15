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
