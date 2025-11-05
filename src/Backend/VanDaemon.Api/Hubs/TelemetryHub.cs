using Microsoft.AspNetCore.SignalR;

namespace VanDaemon.Api.Hubs;

/// <summary>
/// SignalR hub for real-time telemetry updates
/// </summary>
public class TelemetryHub : Hub
{
    private readonly ILogger<TelemetryHub> _logger;

    public TelemetryHub(ILogger<TelemetryHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to tank updates
    /// </summary>
    public async Task SubscribeToTanks()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "tanks");
        _logger.LogDebug("Client {ConnectionId} subscribed to tanks", Context.ConnectionId);
    }

    /// <summary>
    /// Subscribe to control updates
    /// </summary>
    public async Task SubscribeToControls()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "controls");
        _logger.LogDebug("Client {ConnectionId} subscribed to controls", Context.ConnectionId);
    }

    /// <summary>
    /// Subscribe to alert updates
    /// </summary>
    public async Task SubscribeToAlerts()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "alerts");
        _logger.LogDebug("Client {ConnectionId} subscribed to alerts", Context.ConnectionId);
    }
}
