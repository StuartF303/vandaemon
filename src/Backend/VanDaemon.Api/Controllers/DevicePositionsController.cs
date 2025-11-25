using Microsoft.AspNetCore.Mvc;
using VanDaemon.Application.Interfaces;
using VanDaemon.Core.Entities;

namespace VanDaemon.Api.Controllers;

[ApiController]
[Route("api/device-positions")]
public class DevicePositionsController : ControllerBase
{
    private readonly IUnifiedConfigService _configService;
    private readonly ILogger<DevicePositionsController> _logger;

    public DevicePositionsController(
        IUnifiedConfigService configService,
        ILogger<DevicePositionsController> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<DevicePosition>>> GetAllPositions(CancellationToken cancellationToken)
    {
        var positions = await _configService.GetAllPositionsAsync(cancellationToken);
        return Ok(positions);
    }

    [HttpGet("{deviceId}")]
    public async Task<ActionResult<DevicePosition>> GetPosition(string deviceId, CancellationToken cancellationToken)
    {
        var position = await _configService.GetDevicePositionAsync(deviceId, cancellationToken);
        if (position == null)
        {
            return NotFound();
        }
        return Ok(position);
    }

    [HttpPost]
    public async Task<ActionResult<DevicePosition>> SavePosition([FromBody] DevicePosition position, CancellationToken cancellationToken)
    {
        await _configService.SaveDevicePositionAsync(position, cancellationToken);
        return Ok(position);
    }

    [HttpPut("{deviceId}")]
    public async Task<ActionResult<DevicePosition>> UpdatePosition(
        string deviceId,
        [FromBody] DevicePosition position,
        CancellationToken cancellationToken)
    {
        if (deviceId != position.DeviceId)
        {
            return BadRequest("Device ID mismatch");
        }

        await _configService.SaveDevicePositionAsync(position, cancellationToken);
        return Ok(position);
    }
}
