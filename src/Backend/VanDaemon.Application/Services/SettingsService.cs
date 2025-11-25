using Microsoft.Extensions.Logging;
using VanDaemon.Application.Interfaces;
using VanDaemon.Core.Entities;

namespace VanDaemon.Application.Services;

/// <summary>
/// Service implementation for system configuration management
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private SystemConfiguration _configuration;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        _configuration = InitializeDefaultConfiguration();
    }

    private SystemConfiguration InitializeDefaultConfiguration()
    {
        var config = new SystemConfiguration
        {
            Id = Guid.NewGuid(),
            VanModel = "Mercedes Sprinter LWB",
            VanDiagramPath = "/images/Mercedes_Sprinter_LWB_Camper.png",
            ToolbarPosition = ToolbarPosition.Left,
            AlertSettings = new AlertSettings
            {
                EnableAudioAlerts = true,
                EnablePushNotifications = false
            },
            PluginConfigurations = new Dictionary<string, object>(),
            LastUpdated = DateTime.UtcNow
        };

        _logger.LogInformation("Initialized default system configuration for {VanModel}", config.VanModel);
        return config;
    }

    public Task<SystemConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_configuration);
    }

    public Task<SystemConfiguration> UpdateConfigurationAsync(SystemConfiguration configuration, CancellationToken cancellationToken = default)
    {
        configuration.LastUpdated = DateTime.UtcNow;
        _configuration = configuration;
        _logger.LogInformation("Updated system configuration");
        return Task.FromResult(_configuration);
    }

    public Task<IEnumerable<string>> GetAvailableVanDiagramsAsync(CancellationToken cancellationToken = default)
    {
        // Scan the wwwroot/images directory for available diagram images
        // In production, this would be the actual wwwroot/images path
        // For now, return the available image paths
        var diagrams = new[]
        {
            "/images/Mercedes_Sprinter_LWB_Camper.png"
        };

        _logger.LogInformation("Found {Count} available van diagram(s)", diagrams.Length);
        return Task.FromResult<IEnumerable<string>>(diagrams);
    }
}
