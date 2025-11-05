using Microsoft.AspNetCore.SignalR.Client;

namespace VanDaemon.Web.Services;

public class TelemetryService : IAsyncDisposable
{
    private readonly HubConnection _hubConnection;
    private bool _disposed;

    public event Action<Guid, double, string>? TankLevelUpdated;
    public event Action<Guid, object, string>? ControlStateChanged;
    public event Action<List<object>>? AlertsUpdated;

    public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

    public TelemetryService(string hubUrl)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<Guid, double, string>("TankLevelUpdated", (tankId, level, name) =>
        {
            TankLevelUpdated?.Invoke(tankId, level, name);
        });

        _hubConnection.On<Guid, object, string>("ControlStateChanged", (controlId, state, name) =>
        {
            ControlStateChanged?.Invoke(controlId, state, name);
        });

        _hubConnection.On<List<object>>("AlertsUpdated", (alerts) =>
        {
            AlertsUpdated?.Invoke(alerts);
        });
    }

    public async Task StartAsync()
    {
        if (_hubConnection.State == HubConnectionState.Disconnected)
        {
            try
            {
                await _hubConnection.StartAsync();
                await _hubConnection.InvokeAsync("SubscribeToTanks");
                await _hubConnection.InvokeAsync("SubscribeToControls");
                await _hubConnection.InvokeAsync("SubscribeToAlerts");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting SignalR connection: {ex.Message}");
            }
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection.State == HubConnectionState.Connected)
        {
            await _hubConnection.StopAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _hubConnection.DisposeAsync();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
