using VanDaemon.Core.Entities;

namespace VanDaemon.Application.Utilities;

public static class DevicePositionHelper
{
    private const double MinDistance = 15.0; // Minimum distance between devices (percentage)
    private const double GridSize = 20.0; // Grid spacing (percentage)
    private static readonly Random _random = new();

    /// <summary>
    /// Calculates a non-overlapping position for a new device
    /// </summary>
    public static (double X, double Y) GetNonOverlappingPosition(List<DevicePosition> existingPositions, string deviceType)
    {
        // Start positions based on device type to create logical groupings
        var (startX, startY) = GetStartPositionForType(deviceType);

        // Try grid-based positioning first
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                double x = startX + (col * GridSize);
                double y = startY + (row * GridSize);

                // Ensure within bounds (10% margin from edges)
                x = Math.Max(10, Math.Min(90, x));
                y = Math.Max(10, Math.Min(90, y));

                if (IsPositionValid(x, y, existingPositions))
                {
                    return (x, y);
                }
            }
        }

        // Fallback to random positioning if grid is full
        for (int attempt = 0; attempt < 100; attempt++)
        {
            double x = 10 + (_random.NextDouble() * 80); // 10% to 90%
            double y = 10 + (_random.NextDouble() * 80); // 10% to 90%

            if (IsPositionValid(x, y, existingPositions))
            {
                return (x, y);
            }
        }

        // Ultimate fallback - use start position with small random offset
        return (startX + (_random.NextDouble() * 5), startY + (_random.NextDouble() * 5));
    }

    private static (double X, double Y) GetStartPositionForType(string deviceType)
    {
        return deviceType switch
        {
            "Tank" => (20, 20),              // Top-left area
            "Control" => (50, 20),           // Top-center area
            "ElectricalDevice" => (20, 50),  // Middle-left area
            _ => (50, 50)                    // Center
        };
    }

    private static bool IsPositionValid(double x, double y, List<DevicePosition> existingPositions)
    {
        foreach (var pos in existingPositions)
        {
            double distance = CalculateDistance(x, y, pos.X, pos.Y);
            if (distance < MinDistance)
            {
                return false;
            }
        }
        return true;
    }

    private static double CalculateDistance(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
