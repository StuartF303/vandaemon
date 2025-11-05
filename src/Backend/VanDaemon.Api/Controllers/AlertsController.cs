using Microsoft.AspNetCore.Mvc;
using VanDaemon.Application.Interfaces;
using VanDaemon.Core.Entities;

namespace VanDaemon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(IAlertService alertService, ILogger<AlertsController> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }

    /// <summary>
    /// Get all alerts
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Alert>>> GetAll(
        [FromQuery] bool includeAcknowledged = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var alerts = await _alertService.GetAlertsAsync(includeAcknowledged, cancellationToken);
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all alerts");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get alert by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Alert>> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var alert = await _alertService.GetAlertByIdAsync(id, cancellationToken);
            if (alert == null)
            {
                return NotFound();
            }

            return Ok(alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alert {AlertId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Acknowledge an alert
    /// </summary>
    [HttpPost("{id}/acknowledge")]
    public async Task<ActionResult> Acknowledge(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var alert = await _alertService.GetAlertByIdAsync(id, cancellationToken);
            if (alert == null)
            {
                return NotFound();
            }

            await _alertService.AcknowledgeAlertAsync(id, cancellationToken);
            return Ok(new { alertId = id, acknowledged = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging alert {AlertId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete an alert
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var alert = await _alertService.GetAlertByIdAsync(id, cancellationToken);
            if (alert == null)
            {
                return NotFound();
            }

            await _alertService.DeleteAlertAsync(id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting alert {AlertId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Manually check tank alerts
    /// </summary>
    [HttpPost("check")]
    public async Task<ActionResult> CheckAlerts(CancellationToken cancellationToken)
    {
        try
        {
            await _alertService.CheckTankAlertsAsync(cancellationToken);
            var alerts = await _alertService.GetAlertsAsync(includeAcknowledged: false, cancellationToken);
            return Ok(new { message = "Alert check completed", alertCount = alerts.Count() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking alerts");
            return StatusCode(500, "Internal server error");
        }
    }
}
