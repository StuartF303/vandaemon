using Microsoft.AspNetCore.Mvc;
using VanDaemon.Application.Interfaces;
using VanDaemon.Core.Entities;

namespace VanDaemon.Api.Controllers;

[ApiController]
[Route("api/electrical-devices")]
public class ElectricalDevicesController : ControllerBase
{
    private readonly IElectricalDeviceService _deviceService;
    private readonly ILogger<ElectricalDevicesController> _logger;

    public ElectricalDevicesController(
        IElectricalDeviceService deviceService,
        ILogger<ElectricalDevicesController> logger)
    {
        _deviceService = deviceService;
        _logger = logger;
    }

    // Device endpoints
    [HttpGet]
    public async Task<ActionResult<List<ElectricalDevice>>> GetAllDevices(CancellationToken cancellationToken)
    {
        var devices = await _deviceService.GetAllDevicesAsync(cancellationToken);
        return Ok(devices);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ElectricalDevice>> GetDeviceById(Guid id, CancellationToken cancellationToken)
    {
        var device = await _deviceService.GetDeviceByIdAsync(id, cancellationToken);
        if (device == null)
        {
            return NotFound();
        }
        return Ok(device);
    }

    [HttpPost]
    public async Task<ActionResult<ElectricalDevice>> CreateDevice([FromBody] ElectricalDevice device, CancellationToken cancellationToken)
    {
        var created = await _deviceService.CreateDeviceAsync(device, cancellationToken);
        return CreatedAtAction(nameof(GetDeviceById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ElectricalDevice>> UpdateDevice(Guid id, [FromBody] ElectricalDevice device, CancellationToken cancellationToken)
    {
        if (id != device.Id)
        {
            return BadRequest("ID mismatch");
        }

        try
        {
            var updated = await _deviceService.UpdateDeviceAsync(device, cancellationToken);
            return Ok(updated);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteDevice(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deviceService.DeleteDeviceAsync(id, cancellationToken);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    // Connection endpoints
    [HttpGet("connections")]
    public async Task<ActionResult<List<ElectricalConnection>>> GetAllConnections(CancellationToken cancellationToken)
    {
        var connections = await _deviceService.GetAllConnectionsAsync(cancellationToken);
        return Ok(connections);
    }

    [HttpGet("connections/{id}")]
    public async Task<ActionResult<ElectricalConnection>> GetConnectionById(Guid id, CancellationToken cancellationToken)
    {
        var connection = await _deviceService.GetConnectionByIdAsync(id, cancellationToken);
        if (connection == null)
        {
            return NotFound();
        }
        return Ok(connection);
    }

    [HttpPost("connections")]
    public async Task<ActionResult<ElectricalConnection>> CreateConnection([FromBody] ElectricalConnection connection, CancellationToken cancellationToken)
    {
        var created = await _deviceService.CreateConnectionAsync(connection, cancellationToken);
        return CreatedAtAction(nameof(GetConnectionById), new { id = created.Id }, created);
    }

    [HttpPut("connections/{id}")]
    public async Task<ActionResult<ElectricalConnection>> UpdateConnection(Guid id, [FromBody] ElectricalConnection connection, CancellationToken cancellationToken)
    {
        if (id != connection.Id)
        {
            return BadRequest("ID mismatch");
        }

        try
        {
            var updated = await _deviceService.UpdateConnectionAsync(connection, cancellationToken);
            return Ok(updated);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpDelete("connections/{id}")]
    public async Task<ActionResult> DeleteConnection(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deviceService.DeleteConnectionAsync(id, cancellationToken);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    // Metrics endpoints
    [HttpGet("metrics")]
    public async Task<ActionResult<Dictionary<Guid, Dictionary<string, double>>>> GetAllDeviceMetrics(CancellationToken cancellationToken)
    {
        var metrics = await _deviceService.GetAllDeviceMetricsAsync(cancellationToken);
        return Ok(metrics);
    }

    [HttpGet("connections/flows")]
    public async Task<ActionResult<Dictionary<Guid, double>>> GetAllConnectionFlows(CancellationToken cancellationToken)
    {
        var flows = await _deviceService.GetAllConnectionFlowsAsync(cancellationToken);
        return Ok(flows);
    }

    // Discovery endpoint
    [HttpPost("discover")]
    public async Task<ActionResult<List<ElectricalDevice>>> DiscoverDevices(
        [FromBody] DiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        var devices = await _deviceService.DiscoverDevicesAsync(
            request.PluginName,
            request.Configuration,
            cancellationToken);
        return Ok(devices);
    }

    public class DiscoveryRequest
    {
        public string PluginName { get; set; } = string.Empty;
        public Dictionary<string, object> Configuration { get; set; } = new();
    }
}
