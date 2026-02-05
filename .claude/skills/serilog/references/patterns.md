# Serilog Patterns Reference

## Contents
- Message Template Syntax
- Log Levels
- Exception Handling
- Enrichment Patterns
- Anti-Patterns

## Message Template Syntax

Use named placeholders for structured data capture. Property names become queryable fields.

```csharp
// GOOD - Named properties for structured queries
_logger.LogInformation("Tank {TankId} level changed from {OldLevel}% to {NewLevel}%",
    tankId, oldLevel, newLevel);

// GOOD - Object destructuring with @ prefix
_logger.LogDebug("Processing control state: {@ControlState}", new { Id = control.Id, State = control.State });

// GOOD - Collection stringification with $ prefix  
_logger.LogInformation("Found {TankCount} tanks: {$TankNames}", tanks.Count, tanks.Select(t => t.Name));
```

### WARNING: String Interpolation

**The Problem:**

```csharp
// BAD - String interpolation destroys structure
_logger.LogInformation($"Tank {tankId} updated to {level}%");
```

**Why This Breaks:**
1. Properties not captured as separate fields - can't filter by `TankId`
2. Performance overhead - string built before log level check
3. No semantic meaning - just a flat string

**The Fix:**

```csharp
// GOOD - Message template with named placeholders
_logger.LogInformation("Tank {TankId} updated to {Level}%", tankId, level);
```

## Log Levels

VanDaemon uses these levels consistently:

| Level | Use Case | Example |
|-------|----------|---------|
| `Verbose` | Detailed tracing (disabled in production) | Loop iterations |
| `Debug` | Developer diagnostics | Method entry/exit |
| `Information` | Normal operations | Tank level updated |
| `Warning` | Unexpected but handled | Threshold exceeded |
| `Error` | Operation failed | Plugin connection lost |
| `Fatal` | Application crash | Startup failure |

```csharp
// Information - Successful business operations
_logger.LogInformation("Control {ControlId} state set to {State}", control.Id, state);

// Warning - Recoverable issues
_logger.LogWarning("Sensor {SensorId} returned invalid value {Value}, using last known", 
    sensorId, rawValue);

// Error - Failed operations (usually with exception)
_logger.LogError(ex, "Failed to persist tank {TankId} to storage", tank.Id);
```

## Exception Handling

Always pass exception as the first parameter:

```csharp
// GOOD - Exception captured with full stack trace
try
{
    await _mqttClient.ConnectAsync(options, ct);
}
catch (MqttCommunicationException ex)
{
    _logger.LogError(ex, "MQTT connection to {Broker}:{Port} failed", broker, port);
    throw;
}

// GOOD - Conditional rethrow with logging
catch (Exception ex) when (LogAndReturnFalse(ex))
{
    // Never reached, but logs exception
}

private bool LogAndReturnFalse(Exception ex)
{
    _logger.LogError(ex, "Unhandled exception in background service");
    return false;
}
```

### WARNING: Swallowing Exceptions

**The Problem:**

```csharp
// BAD - Silent failure
try
{
    await plugin.ReadValueAsync(sensorId, ct);
}
catch (Exception)
{
    // Nothing logged, nothing thrown
}
```

**Why This Breaks:**
1. Debugging nightmare - no trace of what failed
2. Hidden bugs propagate to production
3. VanDaemon constitution requires critical alerts for hardware failures

**The Fix:**

```csharp
// GOOD - Log and handle or rethrow
catch (Exception ex)
{
    _logger.LogError(ex, "Sensor {SensorId} read failed", sensorId);
    return lastKnownValue; // Explicit fallback
}
```

## Enrichment Patterns

Add context automatically to all log entries:

```csharp
// In Program.cs - Global enrichment
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("Application", "VanDaemon")
    .CreateLogger();

// In services - Scoped context
using (LogContext.PushProperty("TankId", tank.Id))
using (LogContext.PushProperty("Operation", "LevelCheck"))
{
    _logger.LogInformation("Starting level check");
    // All logs in this scope include TankId and Operation
    _logger.LogInformation("Level check complete");
}
```

## Anti-Patterns

### WARNING: Logging Sensitive Data

**The Problem:**

```csharp
// BAD - Credentials in logs
_logger.LogInformation("Connecting with password {Password}", mqttPassword);
```

**The Fix:**

```csharp
// GOOD - Redact sensitive values
_logger.LogInformation("Connecting to {Broker} with auth enabled", broker);
```

### WARNING: High-Volume Debug Logging

**The Problem:**

```csharp
// BAD - Logs every iteration in 5-second polling loop
foreach (var tank in tanks)
{
    _logger.LogDebug("Checking tank {TankId}", tank.Id);
}
```

**Why This Breaks:**
1. Log files grow rapidly on Raspberry Pi storage
2. Console output floods Docker logs
3. Performance overhead in tight loops

**The Fix:**

```csharp
// GOOD - Log summary at appropriate level
_logger.LogDebug("Checking {TankCount} tanks", tanks.Count);

// Or use conditional verbose logging
if (_logger.IsEnabled(LogLevel.Trace))
{
    foreach (var tank in tanks)
        _logger.LogTrace("Tank {TankId}: {Level}%", tank.Id, tank.CurrentLevel);
}