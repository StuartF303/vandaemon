using MQTTnet;
using System.Text;
using System.Text.Json;

namespace CerboGXTest;

class Program
{
    private static readonly Dictionary<string, DateTime> _lastSeen = new();
    private static readonly Dictionary<string, string> _topicValues = new();
    private static int _messageCount = 0;
    private static DateTime _startTime = DateTime.Now;

    static async Task Main(string[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("   Victron Cerbo GX MQTT Test Tool");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        if (args.Length < 1)
        {
            Console.WriteLine("Usage: CerboGXTest <cerbo-ip-address> [mqtt-port] [vrm-portal-id]");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  CerboGXTest 192.168.1.100");
            Console.WriteLine("  CerboGXTest 192.168.1.100 1883");
            Console.WriteLine("  CerboGXTest 192.168.1.100 1883 c0619ab22c89");
            Console.WriteLine();
            Console.WriteLine("Default MQTT port: 1883");
            Console.WriteLine("If VRM Portal ID is not provided, will subscribe to all topics (N/#)");
            return;
        }

        string cerboIp = args[0];
        int mqttPort = args.Length > 1 && int.TryParse(args[1], out var port) ? port : 1883;
        string? portalId = args.Length > 2 ? args[2] : null;

        Console.WriteLine($"Cerbo GX IP:      {cerboIp}");
        Console.WriteLine($"MQTT Port:        {mqttPort}");
        Console.WriteLine($"VRM Portal ID:    {portalId ?? "(auto-discover)"}");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to exit");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        // Create MQTT client
        var factory = new MqttClientFactory();
        using var mqttClient = factory.CreateMqttClient();

        // Configure MQTT connection
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(cerboIp, mqttPort)
            .WithClientId($"CerboGXTest-{Guid.NewGuid()}")
            .WithCleanSession()
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
            .Build();

        // Handle received messages
        mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;

            _messageCount++;
            _lastSeen[topic] = DateTime.Now;
            _topicValues[topic] = payload;

            // Parse topic to extract meaning
            var parts = topic.Split('/');
            var displayTopic = topic;
            var description = GetTopicDescription(parts);

            // Color code based on topic type
            var color = GetTopicColor(topic);
            Console.ForegroundColor = color;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {displayTopic}");
            Console.ResetColor();

            if (!string.IsNullOrEmpty(description))
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"  Description: {description}");
                Console.ResetColor();
            }

            Console.WriteLine($"  Value: {FormatValue(payload, topic)}");
            Console.WriteLine();

            await Task.CompletedTask;
        };

        // Handle connection status
        mqttClient.ConnectedAsync += async e =>
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[CONNECTED] Successfully connected to Cerbo GX at {cerboIp}:{mqttPort}");
            Console.ResetColor();
            Console.WriteLine();

            // Subscribe to all Victron topics
            var subscribeOptions = new MqttClientSubscribeOptionsBuilder();

            if (!string.IsNullOrEmpty(portalId))
            {
                subscribeOptions.WithTopicFilter($"N/{portalId}/#");
                Console.WriteLine($"[SUBSCRIBE] Subscribing to N/{portalId}/#");
            }
            else
            {
                subscribeOptions.WithTopicFilter("N/#");
                Console.WriteLine("[SUBSCRIBE] Subscribing to N/# (all devices)");
            }

            await mqttClient.SubscribeAsync(subscribeOptions.Build());
            Console.WriteLine();

            await Task.CompletedTask;
        };

        mqttClient.DisconnectedAsync += async e =>
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[DISCONNECTED] Connection lost: {e.Reason}");
            if (e.Exception != null)
            {
                Console.WriteLine($"  Exception: {e.Exception.Message}");
            }
            Console.ResetColor();

            await Task.CompletedTask;
        };

        // Connect to MQTT broker
        try
        {
            Console.WriteLine($"[CONNECTING] Attempting to connect to {cerboIp}:{mqttPort}...");
            await mqttClient.ConnectAsync(options);

            // Set up periodic statistics display
            var statsTimer = new System.Threading.Timer(_ =>
            {
                DisplayStatistics();
            }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            // Keep the application running
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            await Task.Delay(Timeout.Infinite, cts.Token).ContinueWith(_ => { });

            statsTimer.Dispose();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Failed to connect: {ex.Message}");
            Console.ResetColor();
            return;
        }
        finally
        {
            if (mqttClient.IsConnected)
            {
                await mqttClient.DisconnectAsync();
            }
        }
    }

    private static string GetTopicDescription(string[] parts)
    {
        if (parts.Length < 4) return string.Empty;

        var parameter = parts[^1]; // Last part is the parameter name

        return parameter switch
        {
            "Dc/0/Voltage" => "Battery Voltage (V)",
            "Dc/0/Current" => "Battery Current (A)",
            "Dc/0/Power" => "Battery Power (W)",
            "Dc/0/Temperature" => "Battery Temperature (°C)",
            "Soc" => "State of Charge (%)",
            "TimeToGo" => "Time to Empty (seconds)",
            "ConsumedAmphours" => "Consumed Amp Hours (Ah)",
            "Ac/ActiveIn/L1/P" => "AC Input L1 Power (W)",
            "Ac/ActiveIn/L1/V" => "AC Input L1 Voltage (V)",
            "Ac/ActiveIn/L1/I" => "AC Input L1 Current (A)",
            "Ac/Out/L1/P" => "AC Output L1 Power (W)",
            "Ac/Out/L1/V" => "AC Output L1 Voltage (V)",
            "Ac/Out/L1/I" => "AC Output L1 Current (A)",
            "Mode" => "Operating Mode",
            "State" => "Device State",
            "Yield/Power" => "Solar Yield Power (W)",
            "Pv/V" => "Solar Panel Voltage (V)",
            "Pv/I" => "Solar Panel Current (A)",
            "Tank/0/Level" => "Fresh Water Tank Level (%)",
            "Tank/1/Level" => "Grey Water Tank Level (%)",
            "Tank/2/Level" => "Black Water Tank Level (%)",
            "Tank/0/Capacity" => "Fresh Water Tank Capacity (m³)",
            "ProductName" => "Product Name",
            "Serial" => "Serial Number",
            "FirmwareVersion" => "Firmware Version",
            _ => string.Empty
        };
    }

    private static ConsoleColor GetTopicColor(string topic)
    {
        if (topic.Contains("/Soc")) return ConsoleColor.Cyan;
        if (topic.Contains("/Voltage")) return ConsoleColor.Yellow;
        if (topic.Contains("/Current")) return ConsoleColor.Magenta;
        if (topic.Contains("/Power")) return ConsoleColor.Green;
        if (topic.Contains("/Tank/")) return ConsoleColor.Blue;
        if (topic.Contains("/Temperature")) return ConsoleColor.Red;
        if (topic.Contains("/State") || topic.Contains("/Mode")) return ConsoleColor.DarkYellow;
        return ConsoleColor.White;
    }

    private static string FormatValue(string payload, string topic)
    {
        // Try to parse as JSON first
        try
        {
            var jsonDoc = JsonDocument.Parse(payload);
            if (jsonDoc.RootElement.TryGetProperty("value", out var valueElement))
            {
                var value = valueElement.GetRawText();
                return $"{value} (from JSON envelope)";
            }
        }
        catch
        {
            // Not JSON, continue with raw value
        }

        // Format based on topic
        if (double.TryParse(payload, out var numValue))
        {
            if (topic.Contains("/Voltage")) return $"{numValue:F2} V";
            if (topic.Contains("/Current")) return $"{numValue:F2} A";
            if (topic.Contains("/Power")) return $"{numValue:F2} W";
            if (topic.Contains("/Soc")) return $"{numValue:F1} %";
            if (topic.Contains("/Temperature")) return $"{numValue:F1} °C";
            if (topic.Contains("/Level")) return $"{numValue:F1} %";
            if (topic.Contains("/TimeToGo")) return $"{TimeSpan.FromSeconds(numValue):hh\\:mm\\:ss}";
        }

        return payload;
    }

    private static void DisplayStatistics()
    {
        var uptime = DateTime.Now - _startTime;
        var messagesPerSecond = _messageCount / uptime.TotalSeconds;

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("=== STATISTICS ===");
        Console.WriteLine($"Uptime:              {uptime:hh\\:mm\\:ss}");
        Console.WriteLine($"Total Messages:      {_messageCount:N0}");
        Console.WriteLine($"Messages/Second:     {messagesPerSecond:F2}");
        Console.WriteLine($"Unique Topics:       {_topicValues.Count:N0}");
        Console.ResetColor();
        Console.WriteLine();

        if (_topicValues.Any())
        {
            Console.WriteLine("Recent Topics:");
            var recentTopics = _lastSeen
                .OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .Select(kvp => $"  {kvp.Key} = {_topicValues[kvp.Key]}");

            foreach (var line in recentTopics)
            {
                Console.WriteLine(line);
            }
            Console.WriteLine();
        }
    }
}
