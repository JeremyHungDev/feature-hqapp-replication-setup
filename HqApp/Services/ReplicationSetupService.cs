using HqApp.Data;
using Microsoft.EntityFrameworkCore;

namespace HqApp.Services;

public class ReplicationSetupService
{
    private readonly HqDbContext _hqDb;
    private readonly BranchDbContext _branchDb;

    public ReplicationSetupService(HqDbContext hqDb, BranchDbContext branchDb)
    {
        _hqDb = hqDb;
        _branchDb = branchDb;
    }

    public async Task SetupAsync()
    {
        await CreatePublicationAsync();
        await CreateSubscriptionAsync();
        await CreateBranchTriggerAsync();
        await SetBranchReadOnlyAsync();
    }

    private async Task CreatePublicationAsync()
    {
        Console.WriteLine("[Replication] 建立 Publication...");
        await _hqDb.Database.ExecuteSqlRawAsync(@"
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

    private async Task CreateSubscriptionAsync()
    {
        Console.WriteLine("[Replication] 建立 Subscription...");

        // CHECK: Cannot use DO $$ block for CREATE SUBSCRIPTION (illegal in transaction block)
        // Must check existence separately and issue CREATE outside any block
        var count = await _branchDb.Database
            .SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM pg_subscription WHERE subname = 'sub_branch_from_hq'")
            .FirstAsync();

        if (count == 0)
        {
            await _branchDb.Database.ExecuteSqlRawAsync(
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

    private async Task SetBranchReadOnlyAsync()
    {
        Console.WriteLine("[Replication] 設定 Branch read-only...");
        await _branchDb.Database.ExecuteSqlRawAsync(
            "ALTER DATABASE coffee_shop SET default_transaction_read_only = on;"
        );
        Console.WriteLine("[Replication] Branch read-only OK");
    }

    private async Task CreateBranchTriggerAsync()
    {
        Console.WriteLine("[Replication] 建立 Branch NOTIFY Trigger...");
        await _branchDb.Database.ExecuteSqlRawAsync(@"
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
}
