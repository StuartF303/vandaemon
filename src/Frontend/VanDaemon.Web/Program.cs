using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using VanDaemon.Web;
using VanDaemon.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Determine API base URL based on environment
string apiBaseUrl;

if (builder.HostEnvironment.IsDevelopment())
{
    // Development: Load from appsettings.json
    using var httpClient = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
    var appSettings = await httpClient.GetFromJsonAsync<AppSettings>("appsettings.json");
    apiBaseUrl = appSettings?.ApiBaseUrl ?? "http://localhost:5000";

    // "localhost" only means the API on the machine running it. Served to any
    // other device -- a tablet on the LAN -- localhost is that device, so every
    // API call and the SignalR connection fail. Keep the configured scheme and
    // port, but follow the host the page actually came from. That also survives
    // the dev machine's DHCP address changing, which hardcoding an IP does not.
    var configured = new Uri(apiBaseUrl);
    var servedFrom = new Uri(builder.HostEnvironment.BaseAddress);
    if (configured.IsLoopback && !servedFrom.IsLoopback)
    {
        apiBaseUrl = new UriBuilder(configured) { Host = servedFrom.Host }
                        .Uri.ToString().TrimEnd('/');
    }
}
else
{
    // Production: Use the current host (nginx proxies /api to backend)
    apiBaseUrl = builder.HostEnvironment.BaseAddress.TrimEnd('/');
}

// Configure JSON options for enum string conversion
var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    Converters = { new JsonStringEnumConverter() }
};

// Configure HTTP client for API calls
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
});

// Add MudBlazor services
builder.Services.AddMudServices();

// Add Telemetry Service for real-time updates
builder.Services.AddSingleton(sp => new TelemetryService($"{apiBaseUrl}/hubs/telemetry"));

// Add Settings State Service for dynamic settings updates
builder.Services.AddSingleton<SettingsStateService>();

await builder.Build().RunAsync();

// Helper class for reading appsettings.json
public class AppSettings
{
    public string ApiBaseUrl { get; set; } = string.Empty;
}
