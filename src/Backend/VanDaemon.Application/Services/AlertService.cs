using Microsoft.Extensions.Logging;
using VanDaemon.Application.Interfaces;
using VanDaemon.Core.Entities;
using VanDaemon.Core.Enums;

namespace VanDaemon.Application.Services;

/// <summary>
/// Service implementation for alert management and monitoring
/// </summary>
public class AlertService : IAlertService
{
    private readonly ILogger<AlertService> _logger;
    private readonly ITankService _tankService;
    private readonly List<Alert> _alerts;

    public AlertService(ILogger<AlertService> logger, ITankService tankService)
    {
        _logger = logger;
        _tankService = tankService;
        _alerts = new List<Alert>();
    }

    public Task<IEnumerable<Alert>> GetAlertsAsync(bool includeAcknowledged = false, CancellationToken cancellationToken = default)
    {
        var alerts = includeAcknowledged
            ? _alerts.AsEnumerable()
            : _alerts.Where(a => !a.Acknowledged);

        return Task.FromResult<IEnumerable<Alert>>(alerts.OrderByDescending(a => a.Timestamp));
    }

    public Task<Alert?> GetAlertByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var alert = _alerts.FirstOrDefault(a => a.Id == id);
        return Task.FromResult(alert);
    }

    public Task<Alert> CreateAlertAsync(AlertSeverity severity, string source, string message, CancellationToken cancellationToken = default)
    {
        // Check if similar alert already exists (avoid duplicates)
        var existingAlert = _alerts.FirstOrDefault(a =>
            !a.Acknowledged &&
            a.Source == source &&
            a.Message == message);

        if (existingAlert != null)
        {
            _logger.LogDebug("Alert already exists for {Source}: {Message}", source, message);
            return Task.FromResult(existingAlert);
        }

        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Severity = severity,
            Source = source,
            Message = message,
            Acknowledged = false
        };

        _alerts.Add(alert);
        _logger.LogInformation("Created {Severity} alert from {Source}: {Message}", severity, source, message);

        return Task.FromResult(alert);
    }

    public async Task AcknowledgeAlertAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var alert = await GetAlertByIdAsync(id, cancellationToken);
        if (alert != null)
        {
            alert.Acknowledged = true;
            alert.AcknowledgedAt = DateTime.UtcNow;
            _logger.LogInformation("Acknowledged alert {AlertId}", id);
        }
    }

    public Task DeleteAlertAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var alert = _alerts.FirstOrDefault(a => a.Id == id);
        if (alert != null)
        {
            _alerts.Remove(alert);
            _logger.LogInformation("Deleted alert {AlertId}", id);
        }

        return Task.CompletedTask;
    }

    public async Task CheckTankAlertsAsync(CancellationToken cancellationToken = default)
    {
        var tanks = await _tankService.GetAllTanksAsync(cancellationToken);

        foreach (var tank in tanks)
        {
            var currentLevel = tank.CurrentLevel;

            // Check for low level alerts (for fresh water, LPG, fuel)
            if (tank.Type is TankType.FreshWater or TankType.LPG or TankType.Fuel or TankType.Battery)
            {
                if (currentLevel <= tank.LowLevelThreshold)
                {
                    var severity = currentLevel <= tank.LowLevelThreshold / 2
                        ? AlertSeverity.Critical
                        : AlertSeverity.Warning;

                    await CreateAlertAsync(
                        severity,
                        tank.Id.ToString(),
                        $"{tank.Name} level is low: {currentLevel:F1}%",
                        cancellationToken);
                }
            }

            // Check for high level alerts (for waste water)
            if (tank.Type == TankType.WasteWater)
            {
                if (currentLevel >= tank.HighLevelThreshold)
                {
                    var severity = currentLevel >= 95
                        ? AlertSeverity.Critical
                        : AlertSeverity.Warning;

                    await CreateAlertAsync(
                        severity,
                        tank.Id.ToString(),
                        $"{tank.Name} level is high: {currentLevel:F1}%",
                        cancellationToken);
                }
            }
        }

        _logger.LogDebug("Checked alerts for {Count} tanks", tanks.Count());
    }
}
