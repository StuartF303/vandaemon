using Microsoft.AspNetCore.Mvc;
using VanDaemon.Application.Interfaces;
using VanDaemon.Core.Entities;

namespace VanDaemon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TanksController : ControllerBase
{
    private readonly ITankService _tankService;
    private readonly ILogger<TanksController> _logger;

    public TanksController(ITankService tankService, ILogger<TanksController> logger)
    {
        _tankService = tankService;
        _logger = logger;
    }

    /// <summary>
    /// Get all tanks
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Tank>>> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var tanks = await _tankService.GetAllTanksAsync(cancellationToken);
            return Ok(tanks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all tanks");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get tank by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Tank>> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tank = await _tankService.GetTankByIdAsync(id, cancellationToken);
            if (tank == null)
            {
                return NotFound();
            }

            return Ok(tank);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tank {TankId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get current tank level
    /// </summary>
    [HttpGet("{id}/level")]
    public async Task<ActionResult<double>> GetLevel(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var level = await _tankService.GetTankLevelAsync(id, cancellationToken);
            return Ok(new { tankId = id, level });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tank level for {TankId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update tank configuration
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<Tank>> Update(Guid id, Tank tank, CancellationToken cancellationToken)
    {
        try
        {
            if (id != tank.Id)
            {
                return BadRequest("ID mismatch");
            }

            var existingTank = await _tankService.GetTankByIdAsync(id, cancellationToken);
            if (existingTank == null)
            {
                return NotFound();
            }

            var updatedTank = await _tankService.UpdateTankAsync(tank, cancellationToken);
            return Ok(updatedTank);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tank {TankId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create a new tank
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Tank>> Create(Tank tank, CancellationToken cancellationToken)
    {
        try
        {
            var createdTank = await _tankService.CreateTankAsync(tank, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = createdTank.Id }, createdTank);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tank");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete a tank
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tank = await _tankService.GetTankByIdAsync(id, cancellationToken);
            if (tank == null)
            {
                return NotFound();
            }

            await _tankService.DeleteTankAsync(id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tank {TankId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Refresh all tank levels
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult> RefreshAll(CancellationToken cancellationToken)
    {
        try
        {
            await _tankService.RefreshAllTankLevelsAsync(cancellationToken);
            return Ok(new { message = "All tank levels refreshed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing tank levels");
            return StatusCode(500, "Internal server error");
        }
    }
}
