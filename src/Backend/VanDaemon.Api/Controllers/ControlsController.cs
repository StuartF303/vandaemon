using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VanDaemon.Api.Hubs;
using VanDaemon.Application.Interfaces;
using VanDaemon.Core.Entities;

namespace VanDaemon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ControlsController : ControllerBase
{
    private readonly IControlService _controlService;
    private readonly ILogger<ControlsController> _logger;
    private readonly IHubContext<TelemetryHub> _hubContext;

    public ControlsController(
        IControlService controlService,
        ILogger<ControlsController> logger,
        IHubContext<TelemetryHub> hubContext)
    {
        _controlService = controlService;
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Get all controls
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Control>>> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var controls = await _controlService.GetAllControlsAsync(cancellationToken);
            return Ok(controls);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all controls");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get control by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Control>> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var control = await _controlService.GetControlByIdAsync(id, cancellationToken);
            if (control == null)
            {
                return NotFound();
            }

            return Ok(control);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting control {ControlId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get current control state
    /// </summary>
    [HttpGet("{id}/state")]
    public async Task<ActionResult<object>> GetState(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var control = await _controlService.GetControlByIdAsync(id, cancellationToken);
            if (control == null)
            {
                return NotFound();
            }

            var state = await _controlService.GetControlStateAsync(id, cancellationToken);
            return Ok(new { controlId = id, state });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting control state for {ControlId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Set control state
    /// </summary>
    [HttpPost("{id}/state")]
    public async Task<ActionResult> SetState(Guid id, [FromBody] SetStateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var control = await _controlService.GetControlByIdAsync(id, cancellationToken);
            if (control == null)
            {
                return NotFound();
            }

            var success = await _controlService.SetControlStateAsync(id, request.State, cancellationToken);
            if (!success)
            {
                return BadRequest("Failed to set control state");
            }

            // Notify connected clients
            await _hubContext.Clients.Group("controls").SendAsync(
                "ControlStateChanged",
                id,
                request.State,
                control.Name,
                cancellationToken);

            return Ok(new { controlId = id, state = request.State, success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting control state for {ControlId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update control configuration
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<Control>> Update(Guid id, Control control, CancellationToken cancellationToken)
    {
        try
        {
            if (id != control.Id)
            {
                return BadRequest("ID mismatch");
            }

            var existingControl = await _controlService.GetControlByIdAsync(id, cancellationToken);
            if (existingControl == null)
            {
                return NotFound();
            }

            var updatedControl = await _controlService.UpdateControlAsync(control, cancellationToken);
            return Ok(updatedControl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating control {ControlId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create a new control
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Control>> Create(Control control, CancellationToken cancellationToken)
    {
        try
        {
            var createdControl = await _controlService.CreateControlAsync(control, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = createdControl.Id }, createdControl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating control");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete a control
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var control = await _controlService.GetControlByIdAsync(id, cancellationToken);
            if (control == null)
            {
                return NotFound();
            }

            await _controlService.DeleteControlAsync(id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting control {ControlId}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}

public class SetStateRequest
{
    public object State { get; set; } = false;
}
