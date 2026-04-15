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
