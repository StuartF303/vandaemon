using System.Text.Json.Serialization;
using Serilog;
using VanDaemon.Api.Hubs;
using VanDaemon.Api.Services;
using VanDaemon.Application.Interfaces;
using VanDaemon.Application.Services;
using VanDaemon.Plugins.Abstractions;
using VanDaemon.Plugins.Simulated;

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
        policy.WithOrigins("http://localhost:8080", "http://localhost:5001")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Register plugins
builder.Services.AddSingleton<ISensorPlugin, SimulatedSensorPlugin>();
builder.Services.AddSingleton<IControlPlugin, SimulatedControlPlugin>();
builder.Services.AddSingleton<IControlPlugin, SimulatedSyncPlugin>();

// Register application services
builder.Services.AddSingleton<ITankService, TankService>();
builder.Services.AddSingleton<IControlService, ControlService>();
builder.Services.AddSingleton<IAlertService, AlertService>();
builder.Services.AddSingleton<ISettingsService, SettingsService>();

// Register background services
builder.Services.AddHostedService<TelemetryBackgroundService>();

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
    await plugin.InitializeAsync(new Dictionary<string, object>());
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

// Map SignalR hubs
app.MapHub<TelemetryHub>("/hubs/telemetry");

Log.Information("VanDaemon API starting up...");
Log.Information("SignalR hub available at /hubs/telemetry");
app.Run();
