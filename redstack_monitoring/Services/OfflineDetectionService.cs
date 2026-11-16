
using Microsoft.EntityFrameworkCore;
using redstack_monitoring.Data;

namespace redstack_monitoring.Services;

public class OfflineDetectionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OfflineDetectionService> _logger;

    public OfflineDetectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<OfflineDetectionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<SiteMonitorDbContext>();

                var cutoff = DateTime.UtcNow.AddMinutes(-2);

                var offlineDevices = await db.Devices
                    .Where(d =>
                        d.CurrentStatus != "OFFLINE" &&
                        (d.LastHeartbeat == null ||
                         d.LastHeartbeat < cutoff))
                    .ToListAsync(stoppingToken);

                foreach (var device in offlineDevices)
                {
                    device.CurrentStatus = "OFFLINE";

                    _logger.LogInformation(
                        "Device {DeviceName} marked OFFLINE",
                        device.DeviceName);
                }

                if (offlineDevices.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error checking offline devices");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(30),
                stoppingToken);
        }
    }
}
