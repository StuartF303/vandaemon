using System.Text.Json.Serialization;
using Serilog;
using VanDaemon.Api.Hubs;
using VanDaemon.Api.Services;
using VanDaemon.Application.Interfaces;
using VanDaemon.Application.Persistence;
using VanDaemon.Application.Services;
using VanDaemon.Plugins.Abstractions;
using VanDaemon.Plugins.Simulated;
using VanDaemon.Plugins.Modbus;
using VanDaemon.Plugins.MqttLedDimmer;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/vandaemon-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add SignalR for real-time communication
builder.Services.AddSignalR();

// Add CORS (with credentials for SignalR)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Development: Allow specific origins for local development
            // Frontend runs on 5001, Docker web on 8080
            // Include both localhost and 127.0.0.1 (browsers treat them as different origins)
            policy.WithOrigins(
                      "http://localhost:5001",      // Local development frontend (localhost)
                      "http://127.0.0.1:5001",      // Local development frontend (127.0.0.1)
                      "https://localhost:5001",     // HTTPS local development (localhost)
                      "https://127.0.0.1:5001",     // HTTPS local development (127.0.0.1)
                      "http://localhost:8080",      // Docker web container
                      "http://127.0.0.1:8080"       // Docker web container (127.0.0.1)
                  )
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            // Production: Allow any origin (since we're behind nginx on same domain)
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

// Register JSON file persistence
builder.Services.AddSingleton(sp =>
    new JsonFileStore(sp.GetRequiredService<ILogger<JsonFileStore>>(), Path.Combine(AppContext.BaseDirectory, "data")));

// Register unified configuration service
builder.Services.AddSingleton<IUnifiedConfigService, UnifiedConfigService>();

// Register plugins
builder.Services.AddSingleton<ISensorPlugin, SimulatedSensorPlugin>();
builder.Services.AddSingleton<IControlPlugin, SimulatedControlPlugin>();
builder.Services.AddSingleton<IControlPlugin, SimulatedSyncPlugin>();
builder.Services.AddSingleton<IControlPlugin, ModbusControlPlugin>();
builder.Services.AddSingleton<MqttLedDimmerPlugin>();
builder.Services.AddSingleton<IControlPlugin>(sp => sp.GetRequiredService<MqttLedDimmerPlugin>());

// Register application services
builder.Services.AddSingleton<ITankService, TankService>();
builder.Services.AddSingleton<IControlService, ControlService>();
builder.Services.AddSingleton<IAlertService, AlertService>();
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IElectricalService, ElectricalService>();
builder.Services.AddSingleton<IElectricalDeviceService, ElectricalDeviceService>();

// Register background services
builder.Services.AddHostedService<TelemetryBackgroundService>();
builder.Services.AddHostedService<MqttLedDimmerService>();

// Initialize plugins
var app = builder.Build();

// Initialize sensor plugins
var sensorPlugins = app.Services.GetServices<ISensorPlugin>();
foreach (var plugin in sensorPlugins)
{
    await plugin.InitializeAsync(new Dictionary<string, object>());
}

// Initialize control plugins
var controlPlugins = app.Services.GetServices<IControlPlugin>();
foreach (var plugin in controlPlugins)
{
    // Load plugin-specific configuration if available
    var config = new Dictionary<string, object>();

    if (plugin is MqttLedDimmerPlugin)
    {
        // Load MQTT LED Dimmer configuration from appsettings.json
        var section = app.Configuration.GetSection("MqttLedDimmer");
        if (section.Exists())
        {
            config["MqttBroker"] = section["MqttBroker"] ?? "localhost";
            config["MqttPort"] = int.Parse(section["MqttPort"] ?? "1883");
            config["MqttUsername"] = section["MqttUsername"] ?? "";
            config["MqttPassword"] = section["MqttPassword"] ?? "";
            config["BaseTopic"] = section["BaseTopic"] ?? "vandaemon/leddimmer";
            config["AutoDiscovery"] = bool.Parse(section["AutoDiscovery"] ?? "true");
            config["Devices"] = new List<object>(); // Empty list for now, populated via auto-discovery
        }
    }

    await plugin.InitializeAsync(config);
}

// Configure the HTTP request pipeline
// Enable Swagger in all environments for API documentation access
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "VanDaemon API v1");
    c.RoutePrefix = "swagger"; // Explicitly set the route prefix
});

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Map health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Map SignalR hubs
app.MapHub<TelemetryHub>("/hubs/telemetry");

Log.Information("VanDaemon API starting up...");
Log.Information("SignalR hub available at /hubs/telemetry");
app.Run();
