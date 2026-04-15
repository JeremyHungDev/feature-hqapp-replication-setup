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
                        menu (id, name, category, price, is_available, updated_at);
                    RAISE NOTICE 'Publication 建立完成';
                ELSE
                    RAISE NOTICE 'Publication 已存在，跳過';
                END IF;
            END $$;
        ");
        Console.WriteLine("[Replication] Publication OK");
    }
}
