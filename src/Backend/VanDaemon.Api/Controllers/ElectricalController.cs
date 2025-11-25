using Microsoft.AspNetCore.Mvc;
using VanDaemon.Application.Interfaces;
using VanDaemon.Core.Entities;

namespace VanDaemon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ElectricalController : ControllerBase
{
    private readonly IElectricalService _electricalService;
    private readonly ILogger<ElectricalController> _logger;

    public ElectricalController(IElectricalService electricalService, ILogger<ElectricalController> logger)
    {
        _electricalService = electricalService;
        _logger = logger;
    }

    /// <summary>
    /// Get electrical system data (battery, solar, AC power)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ElectricalSystem>> Get(CancellationToken cancellationToken)
    {
        try
        {
            var system = await _electricalService.GetElectricalSystemAsync(cancellationToken);
            if (system == null)
            {
                return NotFound();
            }

            return Ok(system);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting electrical system");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update electrical system configuration
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<ElectricalSystem>> Update(ElectricalSystem system, CancellationToken cancellationToken)
    {
        try
        {
            var updatedSystem = await _electricalService.UpdateElectricalSystemAsync(system, cancellationToken);
            return Ok(updatedSystem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating electrical system");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Refresh electrical system data from sensors
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult> Refresh(CancellationToken cancellationToken)
    {
        try
        {
            await _electricalService.RefreshElectricalDataAsync(cancellationToken);
            return Ok(new { message = "Electrical system data refreshed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing electrical system data");
            return StatusCode(500, "Internal server error");
        }
    }
}
