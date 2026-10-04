namespace VanDaemon.Plugins.Ui.Api;

/// <summary>
/// Minimal, mockable typed client over the VanDaemon API surface a UI plugin needs.
/// Kept as an interface so UI plugins depend on an abstraction (testable with a mock) rather than
/// <see cref="System.Net.Http.HttpClient"/> directly.
/// </summary>
public interface IVanDaemonApiClient
{
    /// <summary>Reads all tanks from <c>GET api/tanks</c>.</summary>
    Task<IReadOnlyList<TankDto>> GetTanksAsync(CancellationToken cancellationToken = default);
}
