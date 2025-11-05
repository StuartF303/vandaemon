using Microsoft.AspNetCore.SignalR;
using VanDaemon.Api.Hubs;
using VanDaemon.Application.Interfaces;

namespace VanDaemon.Api.Services;

/// <summary>
/// Background service for continuous monitoring and real-time updates
/// </summary>
public class TelemetryBackgroundService : BackgroundService
{
    private readonly ILogger<TelemetryBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly int _refreshIntervalSeconds;

    public TelemetryBackgroundService(
        ILogger<TelemetryBackgroundService> logger,
        IServiceProvider serviceProvider,
        IHubContext<TelemetryHub> hubContext,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _refreshIntervalSeconds = configuration.GetValue<int>("VanDaemon:RefreshIntervalSeconds", 5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telemetry background service starting (refresh interval: {Interval}s)", _refreshIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var tankService = scope.ServiceProvider.GetRequiredService<ITankService>();
                var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();

                // Refresh all tank levels
                await tankService.RefreshAllTankLevelsAsync(stoppingToken);

                // Get all tanks and send updates
                var tanks = await tankService.GetAllTanksAsync(stoppingToken);
                foreach (var tank in tanks)
                {
                    await _hubContext.Clients.Group("tanks").SendAsync(
                        "TankLevelUpdated",
                        tank.Id,
                        tank.CurrentLevel,
                        tank.Name,
                        stoppingToken);
                }

                // Check for alerts
                await alertService.CheckTankAlertsAsync(stoppingToken);

                // Get unacknowledged alerts and send updates
                var alerts = await alertService.GetAlertsAsync(includeAcknowledged: false, stoppingToken);
                var alertsList = alerts.ToList();
                if (alertsList.Any())
                {
                    await _hubContext.Clients.Group("alerts").SendAsync(
                        "AlertsUpdated",
                        alertsList,
                        stoppingToken);
                }

                _logger.LogDebug("Telemetry update sent: {TankCount} tanks, {AlertCount} alerts",
                    tanks.Count(), alertsList.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in telemetry background service");
            }

            await Task.Delay(TimeSpan.FromSeconds(_refreshIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Telemetry background service stopping");
    }
}
