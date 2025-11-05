using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using VanDaemon.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HTTP client for API calls
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000")
});

// Add MudBlazor services
builder.Services.AddMudServices();

await builder.Build().RunAsync();
