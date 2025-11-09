using Microsoft.AspNetCore.Mvc;
using VanDaemon.Application.Interfaces;
using VanDaemon.Core.Entities;
using System.Collections.Concurrent;

namespace VanDaemon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsController> _logger;
    private static readonly ConcurrentDictionary<Guid, OverlayPosition> _overlayPositions = new();

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

    /// <summary>
    /// Get all overlay positions
    /// </summary>
    [HttpGet("overlay-positions")]
    public ActionResult<IEnumerable<OverlayPosition>> GetOverlayPositions()
    {
        try
        {
            return Ok(_overlayPositions.Values.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overlay positions");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Save or update overlay position
    /// </summary>
    [HttpPost("overlay-positions")]
    public ActionResult<OverlayPosition> SaveOverlayPosition(OverlayPosition position)
    {
        try
        {
            _overlayPositions[position.Id] = position;
            _logger.LogInformation("Saved overlay position for {Type} {Id} at ({X}, {Y})",
                position.Type, position.Id, position.X, position.Y);
            return Ok(position);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving overlay position");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete overlay position
    /// </summary>
    [HttpDelete("overlay-positions/{id}")]
    public ActionResult DeleteOverlayPosition(Guid id)
    {
        try
        {
            _overlayPositions.TryRemove(id, out _);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting overlay position for {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}

public class OverlayPosition
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
}
