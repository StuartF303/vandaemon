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

// Use the current host as API base URL (nginx proxies /api to backend)
// In production (Fly.io), this will be https://vandaemon.fly.dev
// In local development, this will be http://localhost:8080 or wherever the app is hosted
var apiBaseUrl = builder.HostEnvironment.BaseAddress.TrimEnd('/');

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

await builder.Build().RunAsync();
