using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using VanDaemon.Web;
using VanDaemon.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000";

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
