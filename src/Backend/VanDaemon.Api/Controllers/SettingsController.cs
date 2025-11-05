using Microsoft.AspNetCore.Mvc;
using VanDaemon.Application.Interfaces;
using VanDaemon.Core.Entities;

namespace VanDaemon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(ISettingsService settingsService, ILogger<SettingsController> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// Get system configuration
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SystemConfiguration>> Get(CancellationToken cancellationToken)
    {
        try
        {
            var config = await _settingsService.GetConfigurationAsync(cancellationToken);
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system configuration");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update system configuration
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<SystemConfiguration>> Update(
        SystemConfiguration configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            var updatedConfig = await _settingsService.UpdateConfigurationAsync(configuration, cancellationToken);
            return Ok(updatedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating system configuration");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get available van diagrams
    /// </summary>
    [HttpGet("van-diagrams")]
    public async Task<ActionResult<IEnumerable<string>>> GetVanDiagrams(CancellationToken cancellationToken)
    {
        try
        {
            var diagrams = await _settingsService.GetAvailableVanDiagramsAsync(cancellationToken);
            return Ok(diagrams);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available van diagrams");
            return StatusCode(500, "Internal server error");
        }
    }
}
