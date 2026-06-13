using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VanDaemon.Plugins.Ui.Api;

/// <summary>
/// <see cref="HttpClient"/>-backed <see cref="IVanDaemonApiClient"/>. Reuses the existing
/// <c>api/tanks</c> endpoint with case-insensitive, enum-as-string JSON (matching the Web host).
/// </summary>
public sealed class HttpVanDaemonApiClient : IVanDaemonApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _httpClient;

    public HttpVanDaemonApiClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <inheritdoc />
    public async Task<IReadOnlyList<TankDto>> GetTanksAsync(CancellationToken cancellationToken = default)
    {
        var tanks = await _httpClient
            .GetFromJsonAsync<List<TankDto>>("api/tanks", JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return tanks ?? new List<TankDto>();
    }
}
