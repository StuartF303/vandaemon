# Serilog Workflows Reference

## Contents
- Configuration Setup
- Adding New Sinks
- Debugging with Logs
- Production Configuration
- Testing with Logging

## Configuration Setup

VanDaemon configures Serilog in `src/Backend/VanDaemon.Api/Program.cs`:

```csharp
// Minimal API setup with Serilog
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: 
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/vandaemon-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

builder.Host.UseSerilog();
```

### Configuration Checklist

Copy this checklist and track progress:
- [ ] Add Serilog packages to API project
- [ ] Configure Log.Logger in Program.cs before builder.Build()
- [ ] Call builder.Host.UseSerilog()
- [ ] Override Microsoft log levels to reduce noise
- [ ] Add file sink with rolling interval
- [ ] Set retained file count for disk space management

## Adding New Sinks

### Seq (Development)

```csharp
// Add NuGet: Serilog.Sinks.Seq
.WriteTo.Seq("http://localhost:5341")
```

### JSON File (Production)

```csharp
// Structured JSON for log aggregation
.WriteTo.File(new JsonFormatter(), "logs/vandaemon-.json",
    rollingInterval: RollingInterval.Day)
```

### Sink Configuration Workflow

1. Add sink NuGet package to VanDaemon.Api
2. Configure sink in LoggerConfiguration chain
3. Verify: `dotnet run` and check output destination
4. If sink fails silently, enable Serilog self-logging:
   ```csharp
   Serilog.Debugging.SelfLog.Enable(Console.Error);
   ```
5. Only proceed when sink writes successfully

## Debugging with Logs

### Find Logs by Property

```bash
# Search for specific tank ID in logs
grep "TankId.*12345" logs/vandaemon-*.txt

# Find all errors
grep "\[ERR\]" logs/vandaemon-*.txt

# Find plugin initialization issues
grep -i "initializing\|failed" logs/vandaemon-*.txt
```

### Docker Container Logs

See the **docker** skill for container log management.

```bash
# Follow API logs
docker compose logs -f api

# Filter by log level (structured output)
docker compose logs api | grep "ERR\|WRN"
```

### Correlating SignalR Events

Add correlation ID for request tracing:

```csharp
// In TelemetryHub
public override async Task OnConnectedAsync()
{
    using (LogContext.PushProperty("ConnectionId", Context.ConnectionId))
    {
        _logger.LogInformation("Client connected from {RemoteIp}", 
            Context.GetHttpContext()?.Connection.RemoteIpAddress);
        await base.OnConnectedAsync();
    }
}
```

## Production Configuration

### appsettings.Production.json

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/vandaemon-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "fileSizeLimitBytes": 10485760
        }
      }
    ]
  }
}
```

### Loading from Configuration

```csharp
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
```

### Production Checklist

Copy this checklist and track progress:
- [ ] Set minimum level to Information (not Debug)
- [ ] Override Microsoft namespaces to Warning
- [ ] Limit retained log files (7 days default)
- [ ] Set file size limit to prevent disk exhaustion
- [ ] Test log rotation on target hardware

## Testing with Logging

### Capturing Logs in Tests

```csharp
// Use NullLogger for unit tests (no output)
var logger = NullLogger<TankService>.Instance;
var service = new TankService(logger, mockFileStore.Object);

// Or capture logs for assertions
var loggerMock = new Mock<ILogger<TankService>>();
var service = new TankService(loggerMock.Object, mockFileStore.Object);

// Verify logging occurred
loggerMock.Verify(
    x => x.Log(
        LogLevel.Error,
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("failed")),
        It.IsAny<Exception>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
    Times.Once);
```

See the **moq** skill for advanced mock verification patterns.

### Integration Test Logging

```csharp
// In test setup - output to test console
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.TestOutput(testOutputHelper)
    .CreateLogger();